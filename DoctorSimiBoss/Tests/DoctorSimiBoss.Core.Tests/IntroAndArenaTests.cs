using System.IO;
using System.Linq;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Hosting;
using DoctorSimiBoss.Core.Manifest;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class IntroTests
    {
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        [Fact]
        public void FightStartsWithAnInvulnerableEntranceThenIdles()
        {
            var brain = new ScriptedBrain();
            var m = _def.CreateStateMachine(brain);
            Assert.Equal(1.0f, _def.Tuning.IntroDuration);

            m.Tick(0.016f, 1f);
            Assert.Equal(BossState.Intro, m.State);
            Assert.True(m.IsInvulnerable);
            Assert.Equal(HitResult.Ignored, m.ReceiveHit(50));
            Assert.Equal(_def.Tuning.MaxHealth, m.Health);

            m.Tick(1.0f, 1f);
            Assert.Equal(BossState.Idle, m.State);
            Assert.Empty(brain.Calls);
        }

        [Fact]
        public void HitBeforeTheFirstTickStartsTheEntranceAndIsIgnored()
        {
            var m = _def.CreateStateMachine(new ScriptedBrain());
            Assert.Equal(HitResult.Ignored, m.ReceiveHit(50));
            Assert.Equal(BossState.Intro, m.State);
            Assert.Equal(_def.Tuning.MaxHealth, m.Health);
        }

        [Fact]
        public void DriverPlaysSpawnForTheEntrance()
        {
            var body = new FakeBody { PlayerOffsetX = -5f };
            var driver = new BossDriver(_def.CreateStateMachine(new ScriptedBrain()),
                                        _def.CreateAnimationResolver(PlaceholderPolicy.PreferAvailable), body);
            driver.Tick(0.016f);
            Assert.Equal("Spawn", body.Animations.Single().Clip);
            driver.Tick(1.0f);
            Assert.Equal("Idle", body.Animations.Last().Clip);
        }
    }

    public class ArenaTests
    {
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        [Fact]
        public void ManifestDescribesTheArenaAndEncounter()
        {
            var a = _def.Manifest.Arena;
            Assert.Equal("Assets/Arena/arena_background.png", a.Background);
            Assert.Equal(1671, a.ImageWidth);
            Assert.Equal(941, a.ImageHeight);
            Assert.Equal("debugKey", _def.Manifest.Encounter.Access);
            Assert.Equal("Spawn", _def.Manifest.Encounter.EntranceAnimation);
            Assert.Empty(_def.Report.Errors);
        }

        [Fact]
        public void ConvertsImagePixelsToWorldUnits()
        {
            var layout = _def.CreateArenaLayout();
            Assert.Equal(1671f / 40f, layout.Width, 3);
            Assert.Equal((941f - 715f) / 40f, layout.FloorY, 3);
            Assert.Equal(5f, layout.LeftWallX, 3);
            Assert.Equal(1475f / 40f, layout.RightWallX, 3);
            Assert.True(layout.PlayerEntryX < layout.BossLandingX);
            Assert.Equal(layout.LeftWallX + 0.5f, layout.ClampX(-100f, 0.5f), 3);
            Assert.Equal(layout.RightWallX - 0.5f, layout.ClampX(100f, 0.5f), 3);
            Assert.Equal(20f, layout.ClampX(20f, 0.5f), 3);
        }

        [Fact]
        public void MissingOrResizedBackgroundIsAnError()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            m.Arena.ImageWidth = 1920;
            var report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));
            Assert.Contains(report.Errors, e => e.Contains("Arena background") && e.Contains("1671x941"));

            m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            m.Arena.Background = "Assets/Arena/nope.png";
            m.Arena.BossLandingX = 50;
            report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));
            Assert.Contains(report.Errors, e => e.Contains("not found"));
            Assert.Contains(report.Errors, e => e.Contains("bossLandingX"));
        }

        [Fact]
        public void OriginalArenaImageIsKeptAlongsideThePng()
        {
            Assert.True(File.Exists(Path.Combine(TestSupport.PackageRoot, "Arena", "arena_background_original.webp")));
        }
    }
}
