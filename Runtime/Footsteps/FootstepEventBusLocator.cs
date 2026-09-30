#if UNITY_EDITOR
using MadeYellow.EventBus;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>Finds the project asset named FootstepEventBus.</summary>
    static class FootstepEventBusLocator
    {
        /// <summary>Loads the alphabetically first asset whose file name is FootstepEventBus.</summary>
        /// <returns>The bus, or null when the project has none.</returns>
        public static ScriptableEventBus Find()
        {
            string[] guids = AssetDatabase.FindAssets("FootstepEventBus t:ScriptableEventBus");
            string bestPath = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name != "FootstepEventBus")
                    continue;

                if (bestPath == null || string.CompareOrdinal(path, bestPath) < 0)
                    bestPath = path;
            }

            if (bestPath == null)
                return null;

            return AssetDatabase.LoadAssetAtPath<ScriptableEventBus>(bestPath);
        }
    }
}
#endif
