using MadeYellow.WAVES.AudioVisualEffects;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    static class CreateWavesMenu
    {
        [MenuItem("GameObject/Create WAVES", false, 0)]
        static void Create(MenuCommand command)
        {
            var go = new GameObject("World Audio & Visual Effects System");
            go.AddComponent<AudioVisualEffects.WAVES>();
            go.AddComponent<WAVESAudioFX>();
            go.AddComponent<WAVESVisualFX>();
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create WAVES");
            Selection.activeGameObject = go;
        }
    }
}
