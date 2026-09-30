using MadeYellow.WAVES.AudioVisualEffects.Modules;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Inspector for one module asset, opened from the WAVES rack.</summary>
    public sealed class WAVESModuleWindow : EditorWindow
    {
        [SerializeField] WAVESModuleBase _module;

        UnityEditor.Editor _editor;
        Vector2 _scroll;

        /// <summary>Shows <paramref name="module"/> in the shared module window.</summary>
        public static void Open(WAVESModuleBase module)
        {
            if (module == null)
                return;

            WAVESBrowserWindow.Open(null, module);
        }

        void OnEnable()
        {
            if (_module == null || _editor != null)
                return;

            titleContent = new GUIContent(_module.name);
            _editor = UnityEditor.Editor.CreateEditor(_module);
        }

        void OnDisable()
        {
            if (_editor == null)
                return;

            DestroyImmediate(_editor);
            _editor = null;
        }

        void OnGUI()
        {
            if (_module == null)
            {
                EditorGUILayout.HelpBox("Select a module from the WAVES rack.", MessageType.Info);
                return;
            }

            if (_editor == null)
                _editor = UnityEditor.Editor.CreateEditor(_module);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
