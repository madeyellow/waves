using MadeYellow.WAVES.Footsteps;
using NUnit.Framework;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class FootstepInfoTests
    {
        [Test]
        public void Version_IsSet()
        {
            Assert.That(FootstepInfo.Version, Is.Not.Null.And.Not.Empty);
        }
    }
}
