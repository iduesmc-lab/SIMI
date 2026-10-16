using System;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Manifest;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class ProceduralAnimatorTests
    {
        private const float Dt = 1f / 60f;
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        private static void Run(ProceduralAnimator a, float seconds, BossState state, AttackStage stage = AttackStage.None,
                                AttackDefinition attack = null, Func<float, float> progress = null, bool airborne = false, float vy = 0f,
                                Action<VisualPose> each = null)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                a.Tick(Dt, state, stage, attack, progress?.Invoke(t / seconds) ?? 0f, 0f, airborne, vy);
                each?.Invoke(a.Pose);
            }
        }

        [Fact]
        public void IdleBreathesGentlyAndStaysBounded()
        {
            var a = new ProceduralAnimator();
            float min = 9, max = -9;
            Run(a, 5f, BossState.Idle, each: p => { min = Math.Min(min, p.ScaleY); max = Math.Max(max, p.ScaleY); });
            Assert.True(max > 1.01f && min < 0.99f, "breathing is visible");
            Assert.True(max < 1.05f && min > 0.95f, "but subtle");
        }

        [Fact]
        public void WindupSquashesAndLeansBackThenStrikeLeansForward()
        {
            var a = new ProceduralAnimator();
            var saludo = _def.Attacks.Get("SaludoCargado");
            Run(a, 0.8f, BossState.Attack, AttackStage.Windup, saludo, t => t);
            Assert.True(a.Pose.ScaleY < 0.95f, "crouched during anticipation");
            Assert.True(a.Pose.Lean < -4f, "leaning back");
            a.Strike();
            Run(a, 0.15f, BossState.Attack, AttackStage.Active, saludo);
            Assert.True(a.Pose.Lean > 3f, "leaning into the punch");
        }

        [Fact]
        public void LandingSquashesThenRecovers()
        {
            var a = new ProceduralAnimator();
            Run(a, 1f, BossState.Idle);
            a.Land();
            float lowest = 9;
            Run(a, 0.15f, BossState.Idle, each: p => lowest = Math.Min(lowest, p.ScaleY));
            Assert.True(lowest < 0.92f, "visible squash on landing");
            Run(a, 1.5f, BossState.Idle);
            Assert.InRange(a.Pose.ScaleY, 0.96f, 1.04f);
        }

        [Fact]
        public void RisingStretchesAndIntensityZeroDisablesEverything()
        {
            var a = new ProceduralAnimator();
            Run(a, 0.3f, BossState.Attack, AttackStage.Active, _def.Attacks.Leap, airborne: true, vy: 10f);
            Assert.True(a.Pose.ScaleY > 1.08f && a.Pose.ScaleX < 1f);

            var off = new ProceduralAnimator { Intensity = 0f };
            off.Land(); off.Hit(); off.Strike();
            Run(off, 1f, BossState.Stun);
            Assert.Equal(1f, off.Pose.ScaleY, 3);
            Assert.Equal(0f, off.Pose.Lean, 3);
        }

        [Fact]
        public void LongFramesStayStable()
        {
            var a = new ProceduralAnimator();
            a.Land(); a.Hit();
            for (int i = 0; i < 20; i++) a.Tick(0.25f, BossState.Stun, AttackStage.None, null, 0f, 0f, false, 0f);
            Assert.False(float.IsNaN(a.Pose.ScaleY) || float.IsInfinity(a.Pose.ScaleY));
            Assert.InRange(a.Pose.ScaleY, 0.65f, 1.35f);
        }
    }

    public class FrameWeightTests
    {
        [Fact]
        public void WeightsShiftTheDamageFrameTiming()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            var clip = m.Animations["Attack"];
            clip.FrameWeights = new[] { 1f, 2f, 0.5f, 3f, 1f, 1f };
            Assert.Equal(3.5f, clip.WeightBefore(3), 3);
            Assert.Equal(8.5f, clip.TotalWeight, 3);

            var report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));
            var attacks = AttackCatalog.FromManifest(m);
            var request = new BossAnimationResolver(m, report, PlaceholderPolicy.PreferAvailable).Resolve(BossState.Attack, attacks.Get("SaludoCargado"));
            var punch = m.Animations["SpecialPunch"];
            Assert.Equal(punch.WeightBefore(3) / attacks.Get("SaludoCargado").Windup, request.Fps, 3);
            Assert.Same(punch.FrameWeights, request.FrameWeights);
        }

        [Fact]
        public void WrongNumberOfWeightsIsAnError()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            m.Animations["Idle"].FrameWeights = new[] { 1f, 1f };
            var report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));
            Assert.Contains(report.Errors, e => e.Contains("frameWeights"));
        }
    }
}
