using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects.Modules.DirectionalActionsModule
{
    /// <summary>Directional action reactions. Playback is not wired up yet.</summary>
    [CreateAssetMenu(fileName = "New Directional Actions Preset", menuName = "MadeYellow/WAVES/Directional Actions Module")]
    [WAVESModuleName("Directional Actions")]
    public sealed class WAVESDirectionalActionModule : WAVESModuleBase
    {
        [Serializable]
        sealed class Cell
        {
        }

        [SerializeField] List<ModuleCell<Cell>> _cells = new List<ModuleCell<Cell>>();

        void OnEnable()
        {
            if (_cells == null)
                _cells = new List<ModuleCell<Cell>>();
        }

        /// <summary>Does nothing until directional action playback exists.</summary>
        public override void Bind(WAVES waves)
        {
        }

        /// <summary>Does nothing until directional action playback exists.</summary>
        public override void Unbind(WAVES waves)
        {
        }
    }
}
