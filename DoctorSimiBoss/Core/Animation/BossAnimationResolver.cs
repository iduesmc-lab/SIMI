using System;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Animation
{
    /// <summary>What the host should play: a validated clip from the manifest.</summary>
    public struct AnimationRequest : IEquatable<AnimationRequest>
    {
        public string Clip;
        public bool Loop;
        public float Fps;
        /// <summary>True when the clip stands in for an animation that does not exist yet.</summary>
        public bool IsPlaceholder;

        public bool Equals(AnimationRequest other)
        {
            return Clip == other.Clip && Loop == other.Loop && Fps.Equals(other.Fps) && IsPlaceholder == other.IsPlaceholder;
        }

        public override bool Equals(object obj) { return obj is AnimationRequest r && Equals(r); }
        public override int GetHashCode() { return (Clip ?? "").GetHashCode() ^ Fps.GetHashCode(); }
        public override string ToString() { return Clip + (IsPlaceholder ? " (placeholder)" : "") + " @" + Fps + "fps" + (Loop ? " loop" : ""); }
    }

    public enum PlaceholderPolicy
    {
        /// <summary>Use every validated clip (Walk, Hurt, Stun, Defeat...), falling back to Idle/Attack.</summary>
        PreferAvailable,
        /// <summary>Use only Idle and Attack, ignoring the other existing clips.</summary>
        IdleAndAttackOnly,
    }

    /// <summary>
    /// Picks the clip for each boss state. Anything without validated frames falls back to
    /// Idle (non-attacking states) or Attack (every attack, including all specials for now).
    /// </summary>
    public sealed class BossAnimationResolver
    {
        private readonly AnimationManifest _manifest;
        private readonly ManifestReport _report;
        private readonly PlaceholderPolicy _policy;
        private readonly string _entranceAnimation;

        public BossAnimationResolver(AnimationManifest manifest, ManifestReport report, PlaceholderPolicy policy)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _policy = policy;
            _entranceAnimation = manifest.Encounter?.EntranceAnimation ?? "Spawn";
            if (!report.PlaceholdersAvailable)
                throw new InvalidOperationException("Idle and Attack must be playable before the boss can be animated");
        }

        public AnimationRequest Resolve(BossState state, AttackDefinition attack)
        {
            switch (state)
            {
                case BossState.Intro:
                    return Pick(_entranceAnimation, ManifestValidator.IdlePlaceholder);
                case BossState.Attack:
                    return ResolveAttack(attack);
                case BossState.Move:
                    return Pick("Walk", ManifestValidator.IdlePlaceholder);
                case BossState.Hurt:
                    return Pick("Hurt", ManifestValidator.IdlePlaceholder);
                case BossState.Stun:
                    return Pick("Stun", ManifestValidator.IdlePlaceholder);
                case BossState.Defeated:
                    return Pick("Defeat", ManifestValidator.IdlePlaceholder);
                case BossState.PhaseTransition:
                    // No dedicated transition clip exists yet; Spawn is not reused because it includes a jump.
                    return Make(ManifestValidator.IdlePlaceholder, false);
                default:
                    return Make(ManifestValidator.IdlePlaceholder, false);
            }
        }

        private AnimationRequest ResolveAttack(AttackDefinition attack)
        {
            AnimationRequest request = attack != null && attack.IsSpecial
                ? Pick(attack.PlannedAnimation, ManifestValidator.AttackPlaceholder)
                : Make(ManifestValidator.AttackPlaceholder, false);
            if (attack == null) return request;

            // Stretch the clip so its damage frame lands when the attack's active part begins.
            var clip = _manifest.Animations[request.Clip];
            if (clip.SuggestedDamageFrame.HasValue && clip.SuggestedDamageFrame.Value > 0 && attack.Windup > 0)
                request.Fps = clip.SuggestedDamageFrame.Value / attack.Windup;
            return request;
        }

        private AnimationRequest Pick(string preferred, string placeholder)
        {
            bool allowed = _policy == PlaceholderPolicy.PreferAvailable || preferred == placeholder;
            if (allowed && preferred != null && _report.IsPlayable(preferred)) return Make(preferred, false);
            return Make(placeholder, true);
        }

        private AnimationRequest Make(string clipName, bool isPlaceholder)
        {
            var clip = _manifest.Animations[clipName];
            return new AnimationRequest { Clip = clipName, Loop = clip.Loop, Fps = clip.Fps, IsPlaceholder = isPlaceholder };
        }
    }
}
