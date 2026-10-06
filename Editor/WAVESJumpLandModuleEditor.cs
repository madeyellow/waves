using MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    [CustomEditor(typeof(WAVESJumpLandModule))]
    public sealed class WAVESJumpLandModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Actor and surface groups are edited in the WAVES Module Browser.",
                MessageType.Info);
            if (GUILayout.Button("Open WAVES Module Browser"))
                WAVESBrowserWindow.Open(null, (WAVESJumpLandModule)target);
        }
    }
}
