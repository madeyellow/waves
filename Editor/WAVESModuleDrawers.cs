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

        struct Entry
        {
            public Drawer Drawer;
            public bool Scroll;
        }

        static readonly Dictionary<Type, Entry> Registry = new Dictionary<Type, Entry>();

        public static void Register(Type moduleType, Drawer drawer)
        {
            Register(moduleType, drawer, true);
        }

        /// <summary>Registers <paramref name="drawer"/>. <paramref name="scroll"/> is false when it fills the space between the group header and footer.</summary>
        public static void Register(Type moduleType, Drawer drawer, bool scroll)
        {
            if (moduleType == null || drawer == null)
                return;

            Registry[moduleType] = new Entry { Drawer = drawer, Scroll = scroll };
        }

        public static bool Has(WAVESModuleBase module)
        {
            return module != null && Registry.ContainsKey(module.GetType());
        }

        /// <summary>False when the group editor should sit between the header and footer without a scroll view.</summary>
        public static bool Scrolls(WAVESModuleBase module)
        {
            if (module == null || !Registry.TryGetValue(module.GetType(), out Entry entry))
                return true;

            return entry.Scroll;
        }

        public static bool Draw(WAVESModuleBase module, SerializedProperty data, string scope)
        {
            if (module == null)
                return false;
            if (!Registry.TryGetValue(module.GetType(), out Entry entry) || entry.Drawer == null)
                return false;

            try
            {
                entry.Drawer(data, scope);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("This module editor failed.\n" + exception.Message, MessageType.Warning);
            }

            return true;
        }
    }
}
