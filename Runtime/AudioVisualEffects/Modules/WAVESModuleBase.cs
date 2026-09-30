using System;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects.Modules
{
    /// <summary>Rack title for a module asset. The editor uses the class name when this is absent.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class WAVESModuleNameAttribute : Attribute
    {
        /// <summary>Stores the rack title.</summary>
        public WAVESModuleNameAttribute(string name)
        {
            Name = name;
        }

        /// <summary>Title shown on the module row.</summary>
        public string Name { get; }
    }

    /// <summary>Shared module asset. <see cref="WAVES"/> binds it while the component is enabled.</summary>
    public abstract class WAVESModuleBase : ScriptableObject
    {
        /// <summary>Starts this module for <paramref name="waves"/>.</summary>
        public abstract void Bind(WAVES waves);

        /// <summary>Stops this module for <paramref name="waves"/>.</summary>
        public abstract void Unbind(WAVES waves);
    }
}
