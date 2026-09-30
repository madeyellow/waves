using MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    [CustomEditor(typeof(WAVESFootstepModule))]
    public sealed class WAVESFootstepModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Actor and surface groups are edited in the WAVES browser.",
                MessageType.Info);
            if (GUILayout.Button("Open WAVES Browser"))
                WAVESBrowserWindow.Open(null, (WAVESFootstepModule)target);
        }
    }
}
