using System.Collections.Generic;
using MadeYellow.WAVES.Surfaces;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Project search for materials and textures whose names match surface keywords.</summary>
    static class SurfaceAssetSearch
    {
        static readonly string[] MaterialFilters = { "t:Material" };
        static readonly string[] TextureFilters = { "t:Texture2D", "t:Cubemap", "t:Texture3D" };

        /// <summary>Project assets of the group type whose names match <paramref name="keywords"/>.</summary>
        public static void Find(bool materials, string folder, string[] keywords, List<UnityEngine.Object> into)
        {
            if (keywords == null || keywords.Length == 0 || into == null)
                return;

            Gather(materials, folder, keywords, into);
        }

        /// <summary>Every material or texture under <paramref name="folder"/>, including nested folders.</summary>
        public static void Collect(bool materials, string folder, List<UnityEngine.Object> into)
        {
            if (string.IsNullOrEmpty(folder) || into == null)
                return;

            Gather(materials, folder, null, into);
        }

        static void Gather(bool materials, string folder, string[] keywords, List<UnityEngine.Object> into)
        {
            string[] filters = materials ? MaterialFilters : TextureFilters;
            var guids = new List<string>();
            var guidSeen = new HashSet<string>();
            string[] scope = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            for (int f = 0; f < filters.Length; f++)
            {
                string[] found = scope == null
                    ? AssetDatabase.FindAssets(filters[f])
                    : AssetDatabase.FindAssets(filters[f], scope);
                for (int i = 0; i < found.Length; i++)
                {
                    if (guidSeen.Add(found[i]))
                        guids.Add(found[i]);
                }
            }

            var seen = new HashSet<EntityId>();
            try
            {
                for (int i = 0; i < guids.Count; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if ((i & 31) == 0 && EditorUtility.DisplayCancelableProgressBar("Auto Search", path, guids.Count == 0 ? 1f : (float)i / guids.Count))
                        break;

                    UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int a = 0; a < assets.Length; a++)
                        Consider(assets[a], materials, keywords, seen, into);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Consider(UnityEngine.Object asset, bool materials, string[] keywords, HashSet<EntityId> seen, List<UnityEngine.Object> into)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset))
                return;

            if (materials)
            {
                if (!(asset is Material))
                    return;
            }
            else if (!(asset is Texture))
            {
                return;
            }

            if (keywords != null && !SurfaceNameMatch.Any(asset.name, keywords))
                return;

            if (seen.Add(asset.GetEntityId()))
                into.Add(asset);
        }
    }
}
