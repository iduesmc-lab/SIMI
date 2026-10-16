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

            if (inRange)
            {
                // Phase 1 chains up to 2 punches, later phases up to MaxCombo.
                int maxCombo = Math.Max(1, Math.Min(ctx.Tuning.MaxCombo, ctx.Phase + 1));
                var punch = BossAction.Perform(ctx.BasicAttack);
                punch.ComboExtra = _random.Next(maxCombo);
                return punch;
            }
            if (ctx.LeapAttack != null && _random.NextDouble() < 0.5) return BossAction.Perform(ctx.LeapAttack);
            return BossAction.Move(ctx.Tuning.MoveDuration);
        }
    }
}
