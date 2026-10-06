using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule
{
    /// <summary>Jump and land reactions. Playback is not wired up yet.</summary>
    [CreateAssetMenu(fileName = "New Jumps & Lands Preset", menuName = "MadeYellow/WAVES/Jumps & Lands Module")]
    [WAVESModuleName("Jumps & Lands")]
    public sealed class WAVESJumpLandModule : WAVESModuleBase
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

        /// <summary>Does nothing until jump and land playback exists.</summary>
        public override void Bind(WAVES waves)
        {
        }

        /// <summary>Does nothing until jump and land playback exists.</summary>
        public override void Unbind(WAVES waves)
        {
        }
    }
}
