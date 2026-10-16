using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Combat
{
    /// <summary>
    /// Prototype numbers for the encounter. Distances and speeds are in game world units,
    /// whose scale is not yet calibrated against a real scene. None of this is final balance.
    /// </summary>
    public sealed class BossTuning
    {
        public int MaxHealth = 900;
        public float MoveSpeed = 4f;
        /// <summary>Horizontal distance at which melee attacks are considered.</summary>
        public float AttackRange = 3f;
        public float IdleBetweenActions = 0.8f;
        public float MoveDuration = 1.2f;
        /// <summary>Flinch length when hit while idle or moving. 0 disables flinching.</summary>
        public float HurtDuration = 0.25f;
        /// <summary>Hits needed to stagger the boss; the counter resets after each stun or phase change.</summary>
        public int StunHitThreshold = 12;
        public float StunDuration = 2.5f;
        public float PhaseTransitionDuration = 1.5f;
        /// <summary>Chance of choosing a special over the basic attack when one is allowed.</summary>
        public float SpecialChance = 0.45f;
        /// <summary>Entrance (jump in and land) before the first decision; invulnerable meanwhile. 0 skips it.</summary>
        public float IntroDuration = 1.0f;

        public static BossTuning FromData(BossTuningData data)
        {
            var t = new BossTuning();
            if (data == null) return t;
            if (data.MaxHealth.HasValue) t.MaxHealth = data.MaxHealth.Value;
            if (data.MoveSpeed.HasValue) t.MoveSpeed = data.MoveSpeed.Value;
            if (data.AttackRange.HasValue) t.AttackRange = data.AttackRange.Value;
            if (data.IdleBetweenActions.HasValue) t.IdleBetweenActions = data.IdleBetweenActions.Value;
            if (data.MoveDuration.HasValue) t.MoveDuration = data.MoveDuration.Value;
            if (data.HurtDuration.HasValue) t.HurtDuration = data.HurtDuration.Value;
            if (data.StunHitThreshold.HasValue) t.StunHitThreshold = data.StunHitThreshold.Value;
            if (data.StunDuration.HasValue) t.StunDuration = data.StunDuration.Value;
            if (data.PhaseTransitionDuration.HasValue) t.PhaseTransitionDuration = data.PhaseTransitionDuration.Value;
            if (data.SpecialChance.HasValue) t.SpecialChance = data.SpecialChance.Value;
            if (data.IntroDuration.HasValue) t.IntroDuration = data.IntroDuration.Value;
            return t;
        }
    }
}
