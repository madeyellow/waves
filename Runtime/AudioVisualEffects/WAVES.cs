using System;
using System.Collections.Generic;
using MadeYellow.EventBus;
using MadeYellow.WAVES.AudioVisualEffects.Abstractions;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using MadeYellow.WAVES.Footsteps;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>World audio and visual effects. Modules in the list query the ground and route playback.</summary>
    [DisallowMultipleComponent]
    public sealed class WAVES : MonoBehaviour
    {
        /// <summary>Bus the modules subscribe to. Defaults to an asset named FootstepEventBus.</summary>
        [SerializeField]
        [Tooltip("Bus the modules subscribe to. When this component is added, an asset named FootstepEventBus is assigned if the project has one.")]
        ScriptableEventBus _bus;

        /// <summary>Modules bound while this component is enabled. One asset per module type.</summary>
        [SerializeField]
        [Tooltip("Modules bound while this component is enabled. A second asset of the same type replaces the first.")]
        List<WAVESModuleBase> _modules = new List<WAVESModuleBase>();

        readonly List<WAVESModuleBase> _bound = new List<WAVESModuleBase>();

        /// <summary>Ground queries. Created when this component awakens.</summary>
        public WAVESQuery Query { get; private set; }

        /// <summary>Bus the modules subscribe to.</summary>
        public ScriptableEventBus Bus => _bus;

        /// <summary>Audio player on this object. Null when WAVES Audio FX is missing.</summary>
        public IWAVESAudioFX Audio { get; private set; }

        /// <summary>Visual player on this object. Null when WAVES Visual FX is missing.</summary>
        public IWAVESVisualFX Visual { get; private set; }

        /// <summary>Number of module slots, including an empty slot.</summary>
        public int ModuleCount => _modules != null ? _modules.Count : 0;

        /// <summary>Module in slot <paramref name="index"/>. Null when the slot is empty.</summary>
        public WAVESModuleBase GetModule(int index)
        {
            if (_modules == null || index < 0 || index >= _modules.Count)
                return null;

            return _modules[index];
        }

        /// <summary>Adds <paramref name="module"/>, or replaces the asset of the same type.</summary>
        public void AssignModule(WAVESModuleBase module)
        {
            if (module == null)
                return;

            if (_modules == null)
                _modules = new List<WAVESModuleBase>();

            Type type = module.GetType();
            for (int i = 0; i < _modules.Count; i++)
            {
                WAVESModuleBase current = _modules[i];
                if (current == null || current.GetType() != type)
                    continue;

                if (current == module)
                    return;

                _modules[i] = module;
                RebindIfPlaying();
                return;
            }

            _modules.Add(module);
            RebindIfPlaying();
        }

        /// <summary>Removes the module in slot <paramref name="index"/>.</summary>
        public void RemoveModuleAt(int index)
        {
            if (_modules == null || index < 0 || index >= _modules.Count)
                return;

            _modules.RemoveAt(index);
            RebindIfPlaying();
        }

#if UNITY_EDITOR
        void Reset()
        {
            if (_bus != null)
                return;

            _bus = FootstepEventBusLocator.Find();
        }
#endif

        void Awake()
        {
            Query = new WAVESQuery();
            Audio = GetComponent<IWAVESAudioFX>();
            Visual = GetComponent<IWAVESVisualFX>();
        }

        void OnEnable()
        {
            RebindModules();
        }

        void OnDisable()
        {
            UnbindModules();
        }

        void OnValidate()
        {
            DeduplicateModules();
            RebindIfPlaying();
        }

        void RebindIfPlaying()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !gameObject.scene.IsValid())
                return;

            RebindModules();
        }

        void RebindModules()
        {
            UnbindModules();
            if (_modules == null)
                return;

            for (int i = 0; i < _modules.Count; i++)
            {
                WAVESModuleBase module = _modules[i];
                if (module == null)
                    continue;

                module.Bind(this);
                _bound.Add(module);
            }
        }

        void UnbindModules()
        {
            for (int i = 0; i < _bound.Count; i++)
            {
                WAVESModuleBase module = _bound[i];
                if (module != null)
                    module.Unbind(this);
            }

            _bound.Clear();
        }

        void DeduplicateModules()
        {
            if (_modules == null)
                return;

            for (int i = 0; i < _modules.Count; i++)
            {
                WAVESModuleBase module = _modules[i];
                if (module == null)
                    continue;

                Type type = module.GetType();
                int last = -1;
                for (int j = i + 1; j < _modules.Count; j++)
                {
                    WAVESModuleBase later = _modules[j];
                    if (later != null && later.GetType() == type)
                        last = j;
                }

                if (last < 0)
                    continue;

                _modules[i] = _modules[last];
                for (int j = _modules.Count - 1; j > i; j--)
                {
                    WAVESModuleBase later = _modules[j];
                    if (later != null && later.GetType() == type)
                        _modules.RemoveAt(j);
                }
            }
        }

        void Start()
        {
            if (_bus != null)
                return;

            Debug.LogWarning("WAVES has no Event Bus. Modules will not receive footsteps.", this);
        }
    }
}
