using MadeYellow.WAVES.Surfaces;
using NUnit.Framework;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class SurfaceNameMatchTests
    {
        [Test]
        public void IsMatch_IgnoresCaseForAnExactName()
        {
            Assert.IsTrue(SurfaceNameMatch.IsMatch("DarkWood", "darkwood"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("grass_with_rocks_01_color_4k", "grass"));
            Assert.IsFalse(SurfaceNameMatch.IsMatch("StoneWall", "grass"));
        }

        [Test]
        public void IsMatch_LetsAStarStandForAnyText()
        {
            Assert.IsTrue(SurfaceNameMatch.IsMatch("DarkWoodFloor", "*wood*"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("WoodFloor", "wood*"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("DarkWood", "*wood"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("Grass", "*"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("abc", "a*b*c"));
            Assert.IsFalse(SurfaceNameMatch.IsMatch("Wood", "stone*"));
        }

        [Test]
        public void Any_MatchesWhenOneKeywordFits()
        {
            Assert.IsTrue(SurfaceNameMatch.Any("Grass01", new[] { "stone", "*grass*" }));
            Assert.IsFalse(SurfaceNameMatch.Any("Grass01", new[] { "stone", "wood*" }));
            Assert.IsFalse(SurfaceNameMatch.Any("Grass", null));
            Assert.IsFalse(SurfaceNameMatch.Any("Grass", new[] { "", null }));
        }

        [Test]
        public void IsMatch_RejectsEmptyText()
        {
            Assert.IsFalse(SurfaceNameMatch.IsMatch(null, "grass"));
            Assert.IsFalse(SurfaceNameMatch.IsMatch(string.Empty, "grass"));
            Assert.IsFalse(SurfaceNameMatch.IsMatch("Grass", null));
            Assert.IsFalse(SurfaceNameMatch.IsMatch("Grass", string.Empty));
        }

        [Test]
        public void IsMatch_LetsAStarMatchNoText()
        {
            Assert.IsTrue(SurfaceNameMatch.IsMatch("wood", "wood*"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("wood", "*wood"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("wood", "**"));
            Assert.IsTrue(SurfaceNameMatch.IsMatch("DarkWoodFloor", "*WOOD*"));
            Assert.IsFalse(SurfaceNameMatch.IsMatch("ab", "a*c"));
        }
    }
}
