using MadeYellow.WAVES.AudioVisualEffects.Modules;
using MadeYellow.WAVES.AudioVisualEffects.Modules.DirectionalActionsModule;
using MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule;
using MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule;
using MadeYellow.WAVES.Editor;
using NUnit.Framework;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class WAVESModuleCatalogTests
    {
        [Test]
        public void BuiltinModulesLeadTheSlotList()
        {
            WAVESModuleCatalog.Invalidate();
            var slots = WAVESModuleCatalog.Slots;
            Assert.GreaterOrEqual(slots.Count, 3);
            Assert.AreEqual(typeof(WAVESFootstepModule), slots[0].Type);
            Assert.AreEqual("Footsteps", slots[0].Title);
            Assert.IsTrue(slots[0].Builtin);
            Assert.AreEqual(typeof(WAVESJumpLandModule), slots[1].Type);
            Assert.AreEqual("Jumps & Lands", slots[1].Title);
            Assert.IsTrue(slots[1].Builtin);
            Assert.AreEqual(typeof(WAVESDirectionalActionModule), slots[2].Type);
            Assert.AreEqual("Directional Actions", slots[2].Title);
            Assert.IsTrue(slots[2].Builtin);
            for (int i = 3; i < slots.Count; i++)
                Assert.IsFalse(slots[i].Builtin);
        }

        [Test]
        public void MissingModuleNameUsesTheClassName()
        {
            Assert.AreEqual("String", WAVESModuleCatalog.Title(typeof(string)));
            Assert.AreEqual("Missing", WAVESModuleCatalog.Title((WAVESModuleBase)null));
        }
    }
}
