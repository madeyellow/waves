using System.Collections.Generic;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.Footsteps;
using MadeYellow.WAVES.Jumps;
using MadeYellow.WAVES.Surfaces;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Project assets used as matrix axes and step rows.</summary>
    static class WAVESCatalog
    {
        static int _users;

        public static readonly List<SurfaceTypeDefinition> Surfaces = new List<SurfaceTypeDefinition>();
        public static readonly List<ActorProfile> Actors = new List<ActorProfile>();
        public static readonly List<FootstepType> Steps = new List<FootstepType>();
        public static readonly List<ActorFallType> Falls = new List<ActorFallType>();

        public static void Retain()
        {
            if (_users == 0)
                EditorApplication.projectChanged += Refresh;

            _users++;
            Refresh();
        }

        public static void Release()
        {
            _users--;
            if (_users > 0)
                return;

            _users = 0;
            EditorApplication.projectChanged -= Refresh;
        }

        public static void Refresh()
        {
            Load(Surfaces);
            Load(Actors);
            Load(Steps);
            Load(Falls);
            Surfaces.Sort(CompareSurface);
            Actors.Sort(CompareActor);
            Steps.Sort(FootstepType.CompareByOrder);
            Falls.Sort(ActorFallType.CompareByOrder);
        }

        public static void NotifyChanged()
        {
            Refresh();
            WAVESBrowserWindow.RepaintOpen();
        }

        static void Load<T>(List<T> into) where T : Object
        {
            into.Clear();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null)
                    into.Add(asset);
            }
        }

        static int CompareSurface(SurfaceTypeDefinition left, SurfaceTypeDefinition right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.name, right.name);
        }

        static int CompareActor(ActorProfile left, ActorProfile right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.name, right.name);
        }
    }
}
