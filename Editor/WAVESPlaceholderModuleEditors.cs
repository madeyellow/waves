using MadeYellow.WAVES.AudioVisualEffects.Modules.DirectionalActionsModule;
using MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule;
using UnityEditor;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Stand-in group editors for built-in modules that are not playable yet.</summary>
    [InitializeOnLoad]
    static class WAVESPlaceholderModuleEditors
    {
        static WAVESPlaceholderModuleEditors()
        {
            WAVESModuleDrawers.Register(typeof(WAVESJumpLandModule), Draw, false);
            WAVESModuleDrawers.Register(typeof(WAVESDirectionalActionModule), Draw, false);
        }

        static void Draw(SerializedProperty data, string scope)
        {
            WAVESModuleEditors.DrawCenteredNotice("WIP: Coming soon", null);
        }
    }
}
