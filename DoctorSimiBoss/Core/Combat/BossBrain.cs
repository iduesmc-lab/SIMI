using System;
using System.Collections.Generic;
using System.Linq;

namespace DoctorSimiBoss.Core.Combat
{
    public enum BossActionKind { Idle, Move, Attack }

    public struct BossAction
    {
        public BossActionKind Kind;
        public float Duration;
        public AttackDefinition Attack;
        /// <summary>Extra repetitions of the same attack right after this one (basic punch combos).</summary>
        public int ComboExtra;

        public static BossAction Idle(float duration) { return new BossAction { Kind = BossActionKind.Idle, Duration = duration }; }
        public static BossAction Move(float duration) { return new BossAction { Kind = BossActionKind.Move, Duration = duration }; }
        public static BossAction Perform(AttackDefinition attack) { return new BossAction { Kind = BossActionKind.Attack, Attack = attack }; }
    }

    /// <summary>What the brain knows when choosing the next action.</summary>
    public struct BrainContext
    {
        public int Phase;
        /// <summary>Player X minus boss X, in world units.</summary>
        public float PlayerOffsetX;
        public BossTuning Tuning;
        public AttackDefinition BasicAttack;
        /// <summary>Jump towards the player; may be null.</summary>
        public AttackDefinition LeapAttack;
        public IList<AttackDefinition> PhaseSpecials;
        /// <summary>Last attack performed, or null.</summary>
        public AttackDefinition LastAttack;
    }

    public interface IBossBrain
    {
        BossAction Decide(BrainContext context);
    }

    /// <summary>
    /// Prototype decision logic. Never chains two specials back to back
    /// (CombatDesign.md: avoid unavoidable follow-ups) and never repeats the same special twice in a row.
    /// </summary>
    public sealed class DefaultBossBrain : IBossBrain
    {
        private readonly Random _random;

        public DefaultBossBrain(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public BossAction Decide(BrainContext ctx)
        {
            bool inRange = Math.Abs(ctx.PlayerOffsetX) <= ctx.Tuning.AttackRange;
            bool lastWasSpecial = ctx.LastAttack != null && ctx.LastAttack.IsSpecial;

            var specials = (ctx.PhaseSpecials ?? new AttackDefinition[0])
                .Where(a => !lastWasSpecial && (inRange || a.Ranged))
                .ToList();

            if (specials.Count > 0 && _random.NextDouble() < ctx.Tuning.SpecialChance)
                return BossAction.Perform(specials[_random.Next(specials.Count)]);

            bool basicUsable = ctx.BasicAttack != null && (inRange || ctx.BasicAttack.Ranged);
            // From far away, sometimes close the distance instead of throwing.
            if (!inRange && ctx.LeapAttack != null && _random.NextDouble() < (basicUsable ? 0.25 : 0.5))
                return BossAction.Perform(ctx.LeapAttack);
            if (basicUsable)
            {
                // Phase 1 chains up to 2 throws, later phases up to MaxCombo.
                int maxCombo = Math.Max(1, Math.Min(ctx.Tuning.MaxCombo, ctx.Phase + 1));
                var basic = BossAction.Perform(ctx.BasicAttack);
                basic.ComboExtra = _random.Next(maxCombo);
                return basic;
            }
            return BossAction.Move(ctx.Tuning.MoveDuration);
        }
    }
}
