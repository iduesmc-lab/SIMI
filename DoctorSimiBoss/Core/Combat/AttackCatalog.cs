using System;
using System.Collections.Generic;
using System.Linq;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Combat
{
    public sealed class AttackDefinition
    {
        public string Id;
        public string DisplayName;
        public bool IsSpecial;
        /// <summary>Phase that introduces the attack; 0 for the basic attack available in every phase.</summary>
        public int Phase;
        /// <summary>Dedicated animation once drawn (SpecialPunch...); the basic attack uses "Attack".</summary>
        public string PlannedAnimation;
        public string AssetStatus;
        /// <summary>Clip shown while the dedicated animation does not exist; null means Attack.</summary>
        public string PlaceholderAnimation;
        /// <summary>Anticipation time before the hitbox can deal damage.</summary>
        public float Windup;
        /// <summary>Time the hitbox (or projectiles/waves) is dangerous.</summary>
        public float Active;
        /// <summary>Punish window after the active part.</summary>
        public float Recovery;
        /// <summary>When above zero the boss stuns itself for this long after recovering.</summary>
        public float SelfStunAfter;
        /// <summary>Horizontal speed during the active part, as a multiple of BossTuning.MoveSpeed.</summary>
        public float ActiveSpeedMultiplier;
        /// <summary>The boss may start this attack from outside AttackRange.</summary>
        public bool Ranged;
        /// <summary>Effect to play when the active part starts, or null.</summary>
        public string ImpactEffect;

        public float TotalDuration => Windup + Active + Recovery;
    }

    /// <summary>
    /// All attacks the boss can perform. Specials come from the manifest's plannedSpecials;
    /// their timings are prototype defaults unless the manifest overrides them.
    /// </summary>
    public sealed class AttackCatalog
    {
        public const string BasicAttackId = "GolpeBasico";
        public const string LeapAttackId = "Brinco";
        public const string PunchImpactEffect = "PunchImpact";

        private readonly Dictionary<string, AttackDefinition> _byId = new Dictionary<string, AttackDefinition>();
        private readonly Dictionary<int, List<AttackDefinition>> _specialsByPhase = new Dictionary<int, List<AttackDefinition>>();

        public AttackDefinition Basic { get; private set; }
        /// <summary>Jump towards the player, used to close distance instead of only walking.</summary>
        public AttackDefinition Leap { get; private set; }
        public IEnumerable<AttackDefinition> All => _byId.Values;

        public AttackDefinition Get(string id)
        {
            return id != null && _byId.TryGetValue(id, out var a) ? a : null;
        }

        /// <summary>Specials the boss may use while in the given phase (empty for unknown phases).</summary>
        public IList<AttackDefinition> SpecialsForPhase(int phase)
        {
            return _specialsByPhase.TryGetValue(phase, out var list) ? list : (IList<AttackDefinition>)new AttackDefinition[0];
        }

        public static AttackCatalog FromManifest(AnimationManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var catalog = new AttackCatalog();
            catalog.Basic = CreateBasicAttack(manifest);
            catalog._byId[catalog.Basic.Id] = catalog.Basic;
            catalog.Leap = new AttackDefinition
            {
                Id = LeapAttackId,
                DisplayName = "Brinco",
                IsSpecial = false,
                Phase = 0,
                PlannedAnimation = "Spawn",
                PlaceholderAnimation = "Spawn",
                AssetStatus = Manifest.AssetStatus.Ready,
                Windup = 0.3f,
                Active = 0.55f,
                Recovery = 0.3f,
                Ranged = true,
            };
            catalog._byId[catalog.Leap.Id] = catalog.Leap;

            foreach (var planned in manifest.PlannedSpecials)
            {
                var attack = CreateSpecial(planned);
                catalog._byId[attack.Id] = attack;
            }

            // A phase uses the specials it lists; "includesPreviousSpecials" also adds those of earlier phases.
            var ordered = manifest.Phases.OrderBy(p => p.Id).ToList();
            var accumulated = new List<AttackDefinition>();
            foreach (var phase in ordered)
            {
                var own = phase.Specials.Select(catalog.Get).Where(a => a != null).ToList();
                var available = phase.IncludesPreviousSpecials ? accumulated.Concat(own).Distinct().ToList() : own;
                catalog._specialsByPhase[phase.Id] = available;
                accumulated.AddRange(own);
            }
            return catalog;
        }

        /// <summary>
        /// The basic punch follows the Attack clip: anticipation until suggestedDamageFrame,
        /// one active frame, then the remaining frames as recovery.
        /// </summary>
        private static AttackDefinition CreateBasicAttack(AnimationManifest manifest)
        {
            float windup = 0.25f, active = 0.1f, recovery = 0.2f;
            if (manifest.Animations.TryGetValue(ManifestValidator.AttackPlaceholder, out var clip) &&
                clip.Fps > 0 && clip.Frames.Count > 0)
            {
                // Frame weights (held key poses) shift the timings: the hitbox opens exactly on the damage frame.
                int damageFrame = clip.SuggestedDamageFrame ?? clip.Frames.Count / 2;
                windup = clip.WeightBefore(damageFrame) / clip.Fps;
                active = clip.WeightOf(damageFrame) / clip.Fps;
                recovery = Math.Max(0f, clip.TotalWeight - clip.WeightBefore(damageFrame + 1)) / clip.Fps;
            }
            return new AttackDefinition
            {
                Id = BasicAttackId,
                DisplayName = "Golpe",
                IsSpecial = false,
                Phase = 0,
                PlannedAnimation = ManifestValidator.AttackPlaceholder,
                AssetStatus = Manifest.AssetStatus.Ready,
                Windup = windup,
                Active = active,
                Recovery = recovery,
                ImpactEffect = PunchImpactEffect,
            };
        }

        private static AttackDefinition CreateSpecial(PlannedSpecial planned)
        {
            var a = new AttackDefinition
            {
                Id = planned.Id,
                DisplayName = planned.DisplayName ?? planned.Id,
                IsSpecial = true,
                Phase = planned.Phase,
                PlannedAnimation = planned.Animation,
                AssetStatus = planned.AssetStatus,
                PlaceholderAnimation = planned.PlaceholderAnimation,
            };
            ApplyDesignDefaults(a);
            var t = planned.Timing;
            if (t != null)
            {
                if (t.Windup.HasValue) a.Windup = t.Windup.Value;
                if (t.Active.HasValue) a.Active = t.Active.Value;
                if (t.Recovery.HasValue) a.Recovery = t.Recovery.Value;
                if (t.SelfStunAfter.HasValue) a.SelfStunAfter = t.SelfStunAfter.Value;
            }
            return a;
        }

        /// <summary>Starting values taken from CombatDesign.md; they need in-game testing.</summary>
        private static void ApplyDesignDefaults(AttackDefinition a)
        {
            switch (a.Id)
            {
                case "SaludoCargado": // charged horizontal punch, recovery after a miss
                    Set(a, 0.8f, 0.35f, 0.8f); a.ActiveSpeedMultiplier = 4f; a.ImpactEffect = PunchImpactEffect; a.Ranged = true; break;
                case "PisotonSimi": // short jump, ground wave on landing
                    Set(a, 0.6f, 0.4f, 0.8f); a.Ranged = true; break;
                case "CarreraPasillo": // straight dash across the arena, brief stun when braking
                    Set(a, 0.7f, 1.3f, 0.3f); a.ActiveSpeedMultiplier = 5f; a.SelfStunAfter = 1.0f; a.Ranged = true; break;
                case "GiroBotarga": // spinning jump, low circular wave on landing
                    Set(a, 0.6f, 0.8f, 0.8f); break;
                case "LluviaCapsulas": // three marked lanes, always one safe gap
                    Set(a, 1.0f, 1.5f, 1.0f); a.Ranged = true; break;
                case "AplausoSalida": // three ground pulses, long punish window after the third
                    Set(a, 0.9f, 2.1f, 1.6f); a.Ranged = true; break;
                default:
                    Set(a, 0.7f, 0.4f, 0.8f); break;
            }
        }

        private static void Set(AttackDefinition a, float windup, float active, float recovery)
        {
            a.Windup = windup;
            a.Active = active;
            a.Recovery = recovery;
        }
    }
}
