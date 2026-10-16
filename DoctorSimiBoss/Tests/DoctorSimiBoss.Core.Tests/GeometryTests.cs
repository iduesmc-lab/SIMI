using DoctorSimiBoss.Core.Arena;
using DoctorSimiBoss.Core.Manifest;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class GeometryTests
    {
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        [Fact]
        public void ManifestDeclaresScaleHitboxesDamageAndKey()
        {
            var m = _def.Manifest;
            Assert.Equal(32f, m.SpritePixelsPerUnit);
            Assert.Equal(0.97f, m.OriginY, 3);
            Assert.Equal(70f, m.BodyBox.Width);
            Assert.Equal(4f, m.PunchBox.X);
            Assert.Equal(2, _def.Tuning.ContactDamage);
            Assert.Equal(2, _def.Tuning.AttackDamage);
            Assert.Equal(1300, _def.Tuning.MaxHealth);
            Assert.Equal(3, _def.Tuning.MaxCombo);
            Assert.Equal(1.3f, _def.Tuning.SpeedForPhase(3), 3);
            Assert.Equal(1.3f, _def.Tuning.SpeedForPhase(9), 3);
            Assert.Equal(1f, _def.Tuning.SpeedForPhase(1), 3);
            Assert.Equal("F8", m.Encounter.DebugKey);
            Assert.Empty(_def.Report.Errors);
        }

        [Fact]
        public void BodyBoxSitsOnThePivotAndHasTheExpectedSize()
        {
            var box = FrameGeometry.ToLocal(_def.Manifest, _def.Manifest.BodyBox, -1);
            Assert.Equal(70f / 32f, box.Width, 3);
            Assert.Equal(114f / 32f, box.Height, 3);
            // Pivot row = 0.97 * 128 = 124.16; box spans rows 10..124, so its bottom is ~0 units from the feet.
            Assert.Equal((124.16f - 67f) / 32f, box.CenterY, 3);
            Assert.Equal((65f - 64f) / 32f, box.CenterX, 3);
        }

        [Fact]
        public void PunchIsInFrontOfTheBossAndMirrorsWhenFacingRight()
        {
            var left = FrameGeometry.ToLocal(_def.Manifest, _def.Manifest.PunchBox, -1);
            var right = FrameGeometry.ToLocal(_def.Manifest, _def.Manifest.PunchBox, 1);
            Assert.True(left.CenterX < -1f, "sprites face left, so the fist is on the left");
            Assert.Equal(-left.CenterX, right.CenterX, 4);
            Assert.Equal(left.CenterY, right.CenterY, 4);
            Assert.False(FrameGeometry.NeedsFlip(_def.Manifest, -1));
            Assert.True(FrameGeometry.NeedsFlip(_def.Manifest, 1));
        }

        [Fact]
        public void HitboxOutsideTheFrameIsAnError()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            m.PunchBox.X = 120;
            var report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));
            Assert.Contains(report.Errors, e => e.Contains("hitboxes.punch"));
        }
    }
}
