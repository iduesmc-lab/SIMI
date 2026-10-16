using System;
using System.IO;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core
{
    /// <summary>
    /// Everything read from the package: parsed manifest, file validation report,
    /// tuning and attack catalog. Building one does not touch the game.
    /// </summary>
    public sealed class BossDefinition
    {
        public AnimationManifest Manifest { get; private set; }
        public ManifestReport Report { get; private set; }
        public BossTuning Tuning { get; private set; }
        public AttackCatalog Attacks { get; private set; }

        /// <summary>
        /// True when nothing blocks the boss: Idle and Attack are playable and phases/specials are consistent.
        /// Incomplete optional clips only produce warnings and fall back to those placeholders.
        /// </summary>
        public bool CanRun => !Report.HasErrors;

        public static BossDefinition Load(string manifestPath, Func<string, string> resolveAssetPath)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Animation manifest not found", manifestPath);
            return FromJson(File.ReadAllText(manifestPath), resolveAssetPath);
        }

        public static BossDefinition FromJson(string json, Func<string, string> resolveAssetPath)
        {
            var manifest = AnimationManifestParser.Parse(json);
            return new BossDefinition
            {
                Manifest = manifest,
                Report = ManifestValidator.Validate(manifest, resolveAssetPath),
                Tuning = BossTuning.FromData(manifest.Tuning),
                Attacks = AttackCatalog.FromManifest(manifest),
            };
        }

        public BossStateMachine CreateStateMachine(IBossBrain brain)
        {
            return new BossStateMachine(Tuning, Manifest.Phases, Attacks, brain);
        }

        public BossAnimationResolver CreateAnimationResolver(PlaceholderPolicy policy)
        {
            return new BossAnimationResolver(Manifest, Report, policy);
        }
    }
}
