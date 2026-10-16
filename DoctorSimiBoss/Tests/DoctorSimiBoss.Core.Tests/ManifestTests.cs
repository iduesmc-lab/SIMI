using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoctorSimiBoss.Core.Json;
using DoctorSimiBoss.Core.Manifest;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class MiniJsonTests
    {
        [Fact]
        public void ParsesNestedValues()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(
                "{\"a\": [1, -2.5e1, true, null], \"b\": {\"c\": \"x\\u00e1\\n\"}}");
            var a = (List<object>)root["a"];
            Assert.Equal(1.0, a[0]);
            Assert.Equal(-25.0, a[1]);
            Assert.Equal(true, a[2]);
            Assert.Null(a[3]);
            Assert.Equal("xá\n", ((Dictionary<string, object>)root["b"])["c"]);
        }

        [Theory]
        [InlineData("{\"a\": 1,}")]
        [InlineData("{\"a\" 1}")]
        [InlineData("[1, 2")]
        [InlineData("{} extra")]
        [InlineData("\"unterminated")]
        public void RejectsMalformedJson(string json)
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse(json));
        }
    }

    public class RealManifestTests
    {
        [Fact]
        public void ParsesThePackagedManifest()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);

            Assert.Equal(128, m.FrameWidth);
            Assert.Equal(128, m.FrameHeight);
            Assert.Equal("left", m.SpriteFacing);
            Assert.Equal(new[] { "Spawn", "Idle", "Walk", "Attack", "Hurt", "Stun", "Defeat" }, m.Animations.Keys.ToArray());
            Assert.Equal(3, m.Animations["Attack"].SuggestedDamageFrame);
            Assert.True(m.Effects.ContainsKey("PunchImpact"));

            Assert.Equal(new[] { "Turno de bienvenida", "Ronda express", "Ultima visita" }, m.Phases.Select(p => p.Name).ToArray());
            Assert.Equal(new[] { "SaludoCargado", "PisotonSimi" }, m.Phases[0].Specials);
            Assert.Equal(new[] { "CarreraPasillo", "GiroBotarga" }, m.Phases[1].Specials);
            Assert.Equal(new[] { "LluviaCapsulas", "AplausoSalida" }, m.Phases[2].Specials);

            Assert.Equal(6, m.PlannedSpecials.Count);
            Assert.All(m.PlannedSpecials, s => Assert.NotEqual(AssetStatus.Ready, s.AssetStatus));
            Assert.Equal(500, m.Tuning.MaxHealth);
            Assert.Contains(m.ConceptReferences, c => c.Path == "References/special_attacks_concept_sheet.png");
        }

        [Fact]
        public void EveryDeclaredFrameExistsAndIs128Png()
        {
            var definition = TestSupport.LoadRealDefinition();
            var report = definition.Report;

            Assert.Empty(report.Errors);
            Assert.Empty(report.Warnings);
            Assert.True(definition.CanRun);
            Assert.True(report.PlaceholdersAvailable);
            Assert.Equal(8, report.Clips.Count);
            Assert.All(report.Clips, c => Assert.True(c.IsPlayable, c.Name));
            Assert.Equal(48, report.Clips.Sum(c => c.DeclaredFrames));
        }

        [Fact]
        public void AllSpecialsUseTheAttackPlaceholderForNow()
        {
            var report = TestSupport.LoadRealDefinition().Report;
            Assert.Equal(6, report.Specials.Count);
            Assert.All(report.Specials, s =>
            {
                Assert.False(s.HasPlayableAnimation);
                Assert.Equal("Attack", s.AnimationUsed);
            });
        }

        [Fact]
        public void ConceptReferencesAreNotListedAsFramesAndExistInThePackage()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            var frames = m.Animations.Values.Concat(m.Effects.Values).SelectMany(c => c.Frames).ToList();
            Assert.DoesNotContain(frames, f => f.Contains("References/"));
            foreach (var reference in m.ConceptReferences)
                Assert.True(File.Exists(Path.Combine(TestSupport.PackageRoot, reference.Path)), reference.Path);
            // Every file in References/ is declared as concept-only.
            var declared = new HashSet<string>(m.ConceptReferences.Select(c => c.Path));
            foreach (var file in Directory.GetFiles(Path.Combine(TestSupport.PackageRoot, "References"), "*.png"))
                Assert.Contains("References/" + Path.GetFileName(file), declared);
        }
    }

    public class ManifestValidatorTests
    {
        [Fact]
        public void MissingOptionalFrameIsAWarningAndBossCanStillRun()
        {
            string root = TestSupport.CopyPackageToTemp();
            try
            {
                File.Delete(Path.Combine(root, "Sprites", "Walk", "walk_03.png"));
                var def = BossDefinition.FromJson(TestSupport.ManifestJson, ManifestPaths.ForSourcePackage(root));

                Assert.False(def.Report.IsPlayable("Walk"));
                Assert.Contains(def.Report.Clips.Single(c => c.Name == "Walk").MissingFrames, f => f.EndsWith("walk_03.png"));
                Assert.Contains(def.Report.Warnings, w => w.Contains("Walk"));
                Assert.True(def.CanRun);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void MissingPlaceholderIsAnError()
        {
            string root = TestSupport.CopyPackageToTemp();
            try
            {
                File.Delete(Path.Combine(root, "Sprites", "Idle", "idle_00.png"));
                var def = BossDefinition.FromJson(TestSupport.ManifestJson, ManifestPaths.ForSourcePackage(root));

                Assert.False(def.Report.PlaceholdersAvailable);
                Assert.Contains(def.Report.Errors, e => e.Contains("'Idle'"));
                Assert.False(def.CanRun);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void WrongSizeOrNonPngFramesAreRejected()
        {
            string root = TestSupport.CopyPackageToTemp();
            try
            {
                // Swap a frame for a 384x256 spritesheet and another for a text file.
                File.Copy(Path.Combine(root, "Sprites", "Hurt", "hurt_spritesheet_128.png"), Path.Combine(root, "Sprites", "Hurt", "hurt_02.png"), true);
                File.WriteAllText(Path.Combine(root, "Sprites", "Stun", "stun_01.png"), "not an image, just some text");
                var report = ManifestValidator.Validate(AnimationManifestParser.Parse(TestSupport.ManifestJson), ManifestPaths.ForSourcePackage(root));

                Assert.False(report.IsPlayable("Hurt"));
                Assert.False(report.IsPlayable("Stun"));
                Assert.Contains(report.Warnings, w => w.Contains("384x256"));
                Assert.Contains(report.Warnings, w => w.Contains("not a PNG"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ConceptArtListedAsFrameIsAnError()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            m.Animations["Walk"].Frames[0] = "Assets/References/walk_concept_sheet.png";
            var report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));

            Assert.False(report.IsPlayable("Walk"));
            Assert.Contains(report.Errors, e => e.Contains("concept art"));
        }

        [Fact]
        public void PhaseGapsAndUnknownSpecialsAreErrors()
        {
            var m = AnimationManifestParser.Parse(TestSupport.ManifestJson);
            m.Phases[1].HealthFrom = 50;
            m.Phases[2].Specials.Add("Inventado");
            var report = ManifestValidator.Validate(m, ManifestPaths.ForSourcePackage(TestSupport.PackageRoot));

            Assert.Contains(report.Errors, e => e.Contains("Gap"));
            Assert.Contains(report.Errors, e => e.Contains("Inventado"));
        }

        [Fact]
        public void UnsupportedSchemaVersionIsRejected()
        {
            string json = TestSupport.ManifestJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
            Assert.Throws<FormatException>(() => AnimationManifestParser.Parse(json));
        }
    }
}
