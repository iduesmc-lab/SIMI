using DoctorSimiBoss.Core.Animation;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class ColorGradeTests
    {
        [Fact]
        public void ManifestEnablesTheHollowKnightGrade()
        {
            var g = TestSupport.LoadRealDefinition().Manifest.ColorGrade;
            Assert.True(g.Enabled);
            Assert.Equal(0.62f, g.Saturation, 3);
            Assert.Equal(150, g.TintR);
        }

        [Fact]
        public void GradeMutesSaturatedRedAndKeepsItRed()
        {
            var g = TestSupport.LoadRealDefinition().Manifest.ColorGrade;
            byte r = 220, gr = 30, b = 30;
            g.Apply(ref r, ref gr, ref b);
            Assert.True(r < 220, "darker");
            Assert.True(r - gr < 190, "less saturated");
            Assert.True(r > gr && r > b, "still reads as red");
        }

        [Fact]
        public void DisabledGradeChangesNothing()
        {
            byte r = 220, g = 30, b = 30;
            ColorGrade.None.Apply(ref r, ref g, ref b);
            Assert.Equal(new byte[] { 220, 30, 30 }, new[] { r, g, b });
        }
    }
}
