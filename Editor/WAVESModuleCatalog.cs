using System;
using System.Collections.Generic;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using MadeYellow.WAVES.AudioVisualEffects.Modules.DirectionalActionsModule;
using MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule;
using MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule;
using UnityEditor;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Concrete module types, built-in ones first, then every other module in the project.</summary>
    public static class WAVESModuleCatalog
    {
        static readonly Type[] BuiltinOrder =
        {
            typeof(WAVESFootstepModule),
            typeof(WAVESJumpLandModule),
            typeof(WAVESDirectionalActionModule)
        };

        static Slot[] _slots = Array.Empty<Slot>();
        static bool _ready;

        /// <summary>One drawable module slot.</summary>
        public readonly struct Slot
        {
            /// <summary>Stores a discovered module type.</summary>
            public Slot(Type type, string title, bool builtin)
            {
                Type = type;
                Title = title;
                Builtin = builtin;
            }

            /// <summary>Concrete module class.</summary>
            public Type Type { get; }

            /// <summary>Rack title. The class name when the module has no readable name.</summary>
            public string Title { get; }

            /// <summary>True for the modules shipped with WAVES, in their fixed order.</summary>
            public bool Builtin { get; }
        }

        /// <summary>Module slots in display order.</summary>
        public static IReadOnlyList<Slot> Slots
        {
            get
            {
                Ensure();
                return _slots;
            }
        }

        /// <summary>Drops the cached type list. The next read builds it again.</summary>
        public static void Invalidate()
        {
            _ready = false;
        }

        /// <summary>Readable name for <paramref name="module"/>. "Missing" when the asset is gone.</summary>
        public static string Title(WAVESModuleBase module)
        {
            if (module == null)
                return "Missing";

            return Title(module.GetType());
        }

        /// <summary>Readable name for <paramref name="type"/>. The class name when no name attribute is present.</summary>
        public static string Title(Type type)
        {
            if (type == null)
                return "Module";

            try
            {
                var named = Attribute.GetCustomAttribute(type, typeof(WAVESModuleNameAttribute), false) as WAVESModuleNameAttribute;
                if (named != null && !string.IsNullOrEmpty(named.Name))
                    return named.Name;
            }
            catch (Exception)
            {
                // A broken attribute still leaves the class name as a title.
            }

            return string.IsNullOrEmpty(type.Name) ? "Module" : type.Name;
        }

        static void Ensure()
        {
            if (_ready)
                return;

            _ready = true;
            var found = new List<Slot>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<WAVESModuleBase>())
            {
                if (!IsConcreteModule(type))
                    continue;

                int builtin = BuiltinIndex(type);
                found.Add(new Slot(type, Title(type), builtin >= 0));
            }

            found.Sort(CompareSlots);
            _slots = found.ToArray();
        }

        static bool IsConcreteModule(Type type)
        {
            return type != null &&
                   type.IsClass &&
                   !type.IsAbstract &&
                   !type.ContainsGenericParameters &&
                   typeof(WAVESModuleBase).IsAssignableFrom(type);
        }

        static int BuiltinIndex(Type type)
        {
            for (int i = 0; i < BuiltinOrder.Length; i++)
            {
                if (BuiltinOrder[i] == type)
                    return i;
            }

            return -1;
        }

        static int CompareSlots(Slot left, Slot right)
        {
            if (left.Builtin != right.Builtin)
                return left.Builtin ? -1 : 1;

            if (left.Builtin && right.Builtin)
                return BuiltinIndex(left.Type).CompareTo(BuiltinIndex(right.Type));

            int title = string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            if (title != 0)
                return title;

            string leftName = left.Type != null ? left.Type.FullName : string.Empty;
            string rightName = right.Type != null ? right.Type.FullName : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }
    }
}
