using System;
using System.Collections.Generic;
using System.IO;
using MadeYellow.WAVES.Jumps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Creates, renames, reorders, and deletes fall types.</summary>
    public sealed class FallTypeSettingsWindow : EditorWindow
    {
        readonly List<ActorFallType> _types = new List<ActorFallType>();
        ReorderableList _list;
        Vector2 _scroll;

        public static void Open()
        {
            var window = GetWindow<FallTypeSettingsWindow>();
            window.titleContent = new GUIContent("Fall Types");
            window.minSize = new Vector2(320f, 280f);
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Fall Types");
            Reload();
            EditorApplication.projectChanged += Reload;
            Undo.undoRedoPerformed += Reload;
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= Reload;
            Undo.undoRedoPerformed -= Reload;
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EnsureList();
            _list.DoLayoutList();
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Create new Fall type"))
                FallTypePrompt.ShowCreate(_ => Reload());
            EditorGUILayout.EndScrollView();
        }

        void EnsureList()
        {
            if (_list != null && _list.list == _types)
                return;

            _list = new ReorderableList(_types, typeof(ActorFallType), true, false, false, false)
            {
                elementHeight = 24f,
                headerHeight = 0f,
                footerHeight = 0f
            };
            _list.drawElementCallback = DrawElement;
            _list.onReorderCallback = _ => ApplyOrder();
        }

        void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            if (index < 0 || index >= _types.Count)
                return;

            ActorFallType type = _types[index];
            if (type == null)
                return;

            const float button = 18f;
            var delete = new Rect(rect.xMax - button, rect.y + 3f, button, button);
            var edit = new Rect(delete.x - 4f - button, delete.y, button, button);
            float textWidth = Mathf.Max(0f, edit.x - rect.x - 6f);
            var title = new Rect(rect.x + 4f, rect.y + 2f, textWidth, 18f);

            EditorGUI.LabelField(title, type.name, EditorStyles.label);

            if (GUI.Button(edit, EditContent(), EditorStyles.iconButton))
            {
                FallTypePrompt.ShowEdit(type, _ => Reload());
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(delete, DeleteContent(), EditorStyles.iconButton))
            {
                DeleteType(type);
                GUIUtility.ExitGUI();
            }
        }

        void ApplyOrder()
        {
            var alive = new List<UnityEngine.Object>();
            for (int i = 0; i < _types.Count; i++)
            {
                if (_types[i] != null)
                    alive.Add(_types[i]);
            }

            Undo.RecordObjects(alive.ToArray(), "Reorder Fall Types");
            for (int i = 0; i < _types.Count; i++)
            {
                ActorFallType type = _types[i];
                if (type == null || type.order == i)
                    continue;

                type.order = i;
                EditorUtility.SetDirty(type);
            }

            WAVESCatalog.NotifyChanged();
        }

        void DeleteType(ActorFallType type)
        {
            if (type == null)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Fall Type",
                "Delete \"" + type.name + "\"?",
                "Delete",
                "Cancel");
            if (!confirmed)
                return;

            string path = AssetDatabase.GetAssetPath(type);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);
            Reload();
        }

        void Reload()
        {
            _types.Clear();
            _types.AddRange(LoadTypes());
            WAVESCatalog.NotifyChanged();
            Repaint();
        }

        public static List<ActorFallType> LoadTypes()
        {
            var result = new List<ActorFallType>();
            string[] guids = AssetDatabase.FindAssets("t:ActorFallType");
            for (int i = 0; i < guids.Length; i++)
            {
                ActorFallType type = AssetDatabase.LoadAssetAtPath<ActorFallType>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (type != null)
                    result.Add(type);
            }

            result.Sort(ActorFallType.CompareByOrder);
            return result;
        }

        static GUIContent EditContent()
        {
            return IconContent("editicon.sml", "Edit", "Edit");
        }

        static GUIContent DeleteContent()
        {
            return IconContent("TreeEditor.Trash", "Delete", "Delete");
        }

        static GUIContent IconContent(string iconName, string fallback, string tooltip)
        {
            Texture image = EditorGUIUtility.IconContent(iconName).image;
            return image != null
                ? new GUIContent(image, tooltip)
                : new GUIContent(fallback, tooltip);
        }
    }

    sealed class FallTypePrompt : EditorWindow
    {
        string _name = string.Empty;
        ActorFallType _editing;
        Action<ActorFallType> _created;
        bool _focused;

        public static void ShowCreate(Action<ActorFallType> created)
        {
            var window = CreateInstance<FallTypePrompt>();
            window._created = created;
            window.titleContent = new GUIContent("Create Fall Type");
            window.minSize = new Vector2(360f, 88f);
            window.maxSize = new Vector2(560f, 88f);
            window.ShowUtility();
        }

        public static void ShowEdit(ActorFallType type, Action<ActorFallType> changed)
        {
            if (type == null)
                return;

            var window = CreateInstance<FallTypePrompt>();
            window._editing = type;
            window._name = type.name;
            window._created = changed;
            window.titleContent = new GUIContent("Edit Fall Type");
            window.minSize = new Vector2(360f, 88f);
            window.maxSize = new Vector2(560f, 88f);
            window.ShowUtility();
        }

        void OnGUI()
        {
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.Use();
                return;
            }

            bool submit = evt.type == EventType.KeyDown
                && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);

            EditorGUILayout.Space(8f);
            GUI.SetNextControlName("FallTypeName");
            _name = EditorGUILayout.TextField("Name", _name);
            if (!_focused)
            {
                EditorGUI.FocusTextInControl("FallTypeName");
                _focused = true;
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(88f)))
                    Close();
                string label = _editing == null ? "Create" : "Apply";
                if (GUILayout.Button(label, GUILayout.Width(88f)) || submit)
                    Apply(submit ? evt : null);
            }
        }

        void Apply(Event submit)
        {
            string name = _name != null ? _name.Trim() : string.Empty;
            if (string.IsNullOrEmpty(name))
                return;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                EditorUtility.DisplayDialog("Fall Type", "That name cannot be used as an asset name.", "OK");
                return;
            }

            if (NameTaken(name, _editing))
            {
                EditorUtility.DisplayDialog(
                    "Fall Type",
                    "A fall type named \"" + name + "\" already exists.",
                    "OK");
                return;
            }

            submit?.Use();
            ActorFallType result = _editing == null ? Create(name) : Edit(_editing, name);
            Action<ActorFallType> created = _created;
            _created = null;
            Close();
            created?.Invoke(result);
        }

        static ActorFallType Create(string name)
        {
            string folder = TypeFolder();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
            var type = CreateInstance<ActorFallType>();
            type.order = NextOrder();
            AssetDatabase.CreateAsset(type, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            return type;
        }

        static ActorFallType Edit(ActorFallType type, string name)
        {
            Undo.RecordObject(type, "Edit Fall Type");
            EditorUtility.SetDirty(type);
            if (type.name != name)
            {
                string path = AssetDatabase.GetAssetPath(type);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.RenameAsset(path, name);
            }

            AssetDatabase.SaveAssets();
            return type;
        }

        static bool NameTaken(string name, ActorFallType except)
        {
            List<ActorFallType> types = FallTypeSettingsWindow.LoadTypes();
            for (int i = 0; i < types.Count; i++)
            {
                ActorFallType type = types[i];
                if (type == null || type == except)
                    continue;
                if (type.name == name)
                    return true;
            }

            return false;
        }

        static int NextOrder()
        {
            List<ActorFallType> types = FallTypeSettingsWindow.LoadTypes();
            int max = -1;
            for (int i = 0; i < types.Count; i++)
            {
                ActorFallType type = types[i];
                if (type != null && type.order > max)
                    max = type.order;
            }

            return max + 1;
        }

        static string TypeFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:ActorFallType");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;

                string folder = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(folder))
                    continue;

                return folder.Replace('\\', '/');
            }

            return "Assets";
        }
    }
}
