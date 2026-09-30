using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    public sealed class FootstepTypeSettingsWindow : EditorWindow
    {
        readonly List<FootstepType> _types = new List<FootstepType>();
        ReorderableList _list;
        Vector2 _scroll;
        GUIStyle _weight;

        public static void Open()
        {
            var window = GetWindow<FootstepTypeSettingsWindow>();
            window.titleContent = new GUIContent("Footstep Types");
            window.minSize = new Vector2(320f, 280f);
            window.Show();
        }

        public static void PromptCreate(Action<FootstepType> created)
        {
            FootstepTypePrompt.ShowCreate(created);
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Footstep Types");
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
            if (GUILayout.Button("Create new Footstep type"))
                FootstepTypePrompt.ShowCreate(_ => Reload());
            EditorGUILayout.EndScrollView();
        }

        void EnsureList()
        {
            if (_list != null && _list.list == _types)
                return;

            _list = new ReorderableList(_types, typeof(FootstepType), true, false, false, false)
            {
                elementHeight = 40f,
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

            FootstepType type = _types[index];
            if (type == null)
                return;

            const float button = 18f;
            var delete = new Rect(rect.xMax - button, rect.y + 8f, button, button);
            var edit = new Rect(delete.x - 4f - button, delete.y, button, button);
            float textWidth = Mathf.Max(0f, edit.x - rect.x - 6f);

            var title = new Rect(rect.x + 4f, rect.y + 3f, textWidth, 16f);
            var subtitle = new Rect(rect.x + 4f, rect.y + 19f, textWidth, 14f);
            EditorGUI.LabelField(title, type.name, EditorStyles.label);
            EditorGUI.LabelField(subtitle, WeightText(type.weight), WeightStyle());

            if (GUI.Button(edit, EditContent(), EditorStyles.iconButton))
            {
                FootstepTypePrompt.ShowEdit(type, _ => Reload());
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

            Undo.RecordObjects(alive.ToArray(), "Reorder Footstep Types");
            for (int i = 0; i < _types.Count; i++)
            {
                FootstepType type = _types[i];
                if (type == null || type.order == i)
                    continue;

                type.order = i;
                EditorUtility.SetDirty(type);
            }

            WAVESCatalog.NotifyChanged();
        }

        void DeleteType(FootstepType type)
        {
            if (type == null)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Footstep Type",
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

        public static List<FootstepType> LoadTypes()
        {
            var result = new List<FootstepType>();
            string[] guids = AssetDatabase.FindAssets("t:FootstepType");
            for (int i = 0; i < guids.Length; i++)
            {
                FootstepType type = AssetDatabase.LoadAssetAtPath<FootstepType>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (type != null)
                    result.Add(type);
            }

            result.Sort(FootstepType.CompareByOrder);
            return result;
        }

        static string WeightText(float weight)
        {
            string value = weight.ToString("0.##", CultureInfo.InvariantCulture);
            return "Animation curve weight <b>" + value + "</b>";
        }

        GUIStyle WeightStyle()
        {
            if (_weight != null)
                return _weight;

            _weight = new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true
            };
            return _weight;
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

    sealed class FootstepTypePrompt : EditorWindow
    {
        string _name = string.Empty;
        float _weight = 1f;
        FootstepType _editing;
        Action<FootstepType> _created;
        bool _focused;

        public static void ShowCreate(Action<FootstepType> created)
        {
            var window = CreateInstance<FootstepTypePrompt>();
            window._created = created;
            window._weight = NextWeight();
            window.titleContent = new GUIContent("Create Footstep Type");
            window.minSize = new Vector2(360f, 112f);
            window.maxSize = new Vector2(560f, 112f);
            window.ShowUtility();
        }

        public static void ShowEdit(FootstepType type, Action<FootstepType> changed)
        {
            if (type == null)
                return;

            var window = CreateInstance<FootstepTypePrompt>();
            window._editing = type;
            window._name = type.name;
            window._weight = type.weight;
            window._created = changed;
            window.titleContent = new GUIContent("Edit Footstep Type");
            window.minSize = new Vector2(360f, 112f);
            window.maxSize = new Vector2(560f, 112f);
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
            GUI.SetNextControlName("FootstepTypeName");
            _name = EditorGUILayout.TextField("Name", _name);
            _weight = EditorGUILayout.FloatField("Weight", _weight);
            if (!_focused)
            {
                EditorGUI.FocusTextInControl("FootstepTypeName");
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
                EditorUtility.DisplayDialog("Footstep Type", "That name cannot be used as an asset name.", "OK");
                return;
            }

            if (NameTaken(name, _editing))
            {
                EditorUtility.DisplayDialog(
                    "Footstep Type",
                    "A footstep type named \"" + name + "\" already exists.",
                    "OK");
                return;
            }

            submit?.Use();
            FootstepType result = _editing == null ? Create(name, _weight) : Edit(_editing, name, _weight);
            Action<FootstepType> created = _created;
            _created = null;
            Close();
            created?.Invoke(result);
        }

        static FootstepType Create(string name, float weight)
        {
            string folder = TypeFolder();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
            var type = CreateInstance<FootstepType>();
            type.weight = weight;
            type.order = NextOrder();
            AssetDatabase.CreateAsset(type, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            return type;
        }

        static FootstepType Edit(FootstepType type, string name, float weight)
        {
            Undo.RecordObject(type, "Edit Footstep Type");
            type.weight = weight;
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

        static bool NameTaken(string name, FootstepType except)
        {
            List<FootstepType> types = FootstepTypeSettingsWindow.LoadTypes();
            for (int i = 0; i < types.Count; i++)
            {
                FootstepType type = types[i];
                if (type == null || type == except)
                    continue;
                if (type.name == name)
                    return true;
            }

            return false;
        }

        static float NextWeight()
        {
            List<FootstepType> types = FootstepTypeSettingsWindow.LoadTypes();
            bool any = false;
            float max = 0f;
            for (int i = 0; i < types.Count; i++)
            {
                FootstepType type = types[i];
                if (type == null)
                    continue;
                if (!any || type.weight > max)
                {
                    max = type.weight;
                    any = true;
                }
            }

            return any ? Mathf.Round(max) + 1f : 1f;
        }

        static int NextOrder()
        {
            List<FootstepType> types = FootstepTypeSettingsWindow.LoadTypes();
            int max = -1;
            for (int i = 0; i < types.Count; i++)
            {
                FootstepType type = types[i];
                if (type != null && type.order > max)
                    max = type.order;
            }

            return max + 1;
        }

        static string TypeFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:FootstepType");
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
