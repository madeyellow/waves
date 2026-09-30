using System;
using System.Collections.Generic;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using UnityEditor;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Module-specific group editors plugged into the shared browser.</summary>
    static class WAVESModuleDrawers
    {
        public delegate void Drawer(SerializedProperty data, string scope);

        static readonly Dictionary<Type, Drawer> Registry = new Dictionary<Type, Drawer>();

        public static void Register(Type moduleType, Drawer drawer)
        {
            if (moduleType == null || drawer == null)
                return;

            Registry[moduleType] = drawer;
        }

        public static bool Draw(WAVESModuleBase module, SerializedProperty data, string scope)
        {
            if (module == null || data == null)
                return false;
            if (!Registry.TryGetValue(module.GetType(), out Drawer drawer))
                return false;

            drawer(data, scope);
            return true;
        }
    }
}
