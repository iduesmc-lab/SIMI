using System;
using System.Collections.Generic;
using System.Linq;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Combat
{
    public enum BossState { Intro, Idle, Move, Attack, Hurt, Stun, PhaseTransition, Defeated }

    public enum AttackStage { None, Windup, Active, Recovery }

    public enum HitResult
    {
        /// <summary>Damage was zero/negative or the boss is invulnerable.</summary>
        Ignored,
        Damaged,
        /// <summary>Damaged and briefly flinched (only from Idle or Move).</summary>
        Flinched,
        /// <summary>Damaged and reached the stun hit threshold.</summary>
        Stunned,
        /// <summary>Damaged and crossed into a new phase.</summary>
        PhaseChanged,
        Defeated,
    }

    /// <summary>
    /// Engine-independent state machine for the boss. The host calls <see cref="Tick"/> every frame
    /// with the player's relative position and <see cref="ReceiveHit"/> when the boss is struck,
    /// then reads <see cref="State"/>, <see cref="VelocityX"/>, <see cref="Facing"/> and the attack
    /// stage to drive animation, movement and hitboxes.
    /// </summary>
    public sealed class BossStateMachine
    {
        private readonly BossTuning _tuning;
        private readonly List<EncounterPhase> _phases;
        private readonly AttackCatalog _attacks;
        private readonly IBossBrain _brain;

        private float _timer;
        private int _hitsSinceStun;
        private bool _started;
        private int _comboLeft;
        private float _lastPlayerOffsetX;

        public BossState State { get; private set; } = BossState.Intro;
        public int Phase { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth => _tuning.MaxHealth;
        public float HealthPercent => MaxHealth > 0 ? Health * 100f / MaxHealth : 0f;

        /// <summary>-1 facing left, +1 facing right.</summary>
        public int Facing { get; private set; } = -1;
        public float VelocityX { get; private set; }

        public AttackDefinition CurrentAttack { get; private set; }
        public AttackStage AttackStage { get; private set; }
        public AttackDefinition LastAttack { get; private set; }

        /// <summary>Seconds left in the current state or attack stage.</summary>
        public float TimeRemaining => _timer;

        public bool IsInvulnerable => State == BossState.Intro || State == BossState.PhaseTransition || State == BossState.Defeated;
        public bool IsAttackHitboxActive => State == BossState.Attack && AttackStage == AttackStage.Active;

        public event Action<BossState, BossState> StateChanged;
        public event Action<int, int> PhaseChanged;
        public event Action<AttackDefinition, AttackStage> AttackStageChanged;

        public BossStateMachine(BossTuning tuning, IEnumerable<EncounterPhase> phases, AttackCatalog attacks, IBossBrain brain)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
            if (phases == null) throw new ArgumentNullException(nameof(phases));
            _phases = phases.OrderByDescending(p => p.HealthFrom).ToList();
            if (_phases.Count == 0) throw new ArgumentException("At least one encounter phase is required", nameof(phases));
            if (_tuning.MaxHealth <= 0) throw new ArgumentException("MaxHealth must be positive", nameof(tuning));

            Health = _tuning.MaxHealth;
            Phase = PhaseForPercent(100f);
        }

        /// <summary>
        /// Begins the fight with the entrance (Intro), or straight in Idle when IntroDuration is 0.
        /// Called automatically by the first Tick or hit.
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            if (_tuning.IntroDuration > 0) Enter(BossState.Intro, _tuning.IntroDuration);
            else Enter(BossState.Idle, _tuning.IdleBetweenActions);
        }

        public void Tick(float deltaTime, float playerOffsetX)
        {
            if (!_started) Start();
            if (deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (State == BossState.Defeated) return;

            _timer -= deltaTime;
            _lastPlayerOffsetX = playerOffsetX;
            switch (State)
            {
                case BossState.Idle:
                    VelocityX = 0;
                    FaceTowards(playerOffsetX);
                    if (_timer <= 0) Decide(playerOffsetX);
                    break;

                case BossState.Move:
                    FaceTowards(playerOffsetX);
                    VelocityX = Facing * _tuning.MoveSpeed;
                    if (_timer <= 0 || Math.Abs(playerOffsetX) <= _tuning.AttackRange) Decide(playerOffsetX);
                    break;

                case BossState.Attack:
                    // Facing is locked for the whole attack so the player can read and dodge it.
                    while (State == BossState.Attack && _timer <= 0) AdvanceAttackStage();
                    if (State == BossState.Attack)
                        VelocityX = AttackStage == AttackStage.Active ? Facing * _tuning.MoveSpeed * CurrentAttack.ActiveSpeedMultiplier : 0;
                    break;

                case BossState.Intro:
                case BossState.Hurt:
                case BossState.Stun:
                case BossState.PhaseTransition:
                    VelocityX = 0;
                    if (_timer <= 0) Enter(BossState.Idle, _tuning.IdleBetweenActions);
                    break;
            }
        }

        public HitResult ReceiveHit(int damage)
        {
            if (!_started) Start();
            if (damage <= 0 || IsInvulnerable) return HitResult.Ignored;

            Health = Math.Max(0, Health - damage);
            if (Health == 0)
            {
                EndAttack();
                Enter(BossState.Defeated, 0);
                return HitResult.Defeated;
            }

            int newPhase = PhaseForPercent(HealthPercent);
            if (newPhase > Phase)
            {
                int previous = Phase;
                Phase = newPhase;
                _hitsSinceStun = 0;
                EndAttack();
                PhaseChanged?.Invoke(previous, newPhase);
                Enter(BossState.PhaseTransition, _tuning.PhaseTransitionDuration);
                return HitResult.PhaseChanged;
            }

            if (State != BossState.Stun && _tuning.StunHitThreshold > 0 && ++_hitsSinceStun >= _tuning.StunHitThreshold)
            {
                _hitsSinceStun = 0;
                EndAttack();
                Enter(BossState.Stun, _tuning.StunDuration);
                return HitResult.Stunned;
            }

            // Attacks have super armour; only idle or walking flinch.
            if ((State == BossState.Idle || State == BossState.Move) && _tuning.HurtDuration > 0)
            {
                Enter(BossState.Hurt, _tuning.HurtDuration);
                return HitResult.Flinched;
            }
            return HitResult.Damaged;
        }

        /// <summary>
        /// Phase for a remaining-health percentage: the first phase (from full health down)
        /// whose lower bound is not above the percentage. Phases only advance, never revert.
        /// </summary>
        public int PhaseForPercent(float percent)
        {
            foreach (var phase in _phases)
                if (percent >= phase.HealthTo) return phase.Id;
            return _phases[_phases.Count - 1].Id;
        }

        private void Decide(float playerOffsetX)
        {
            var action = _brain.Decide(new BrainContext
            {
                Phase = Phase,
                PlayerOffsetX = playerOffsetX,
                Tuning = _tuning,
                BasicAttack = _attacks.Basic,
                LeapAttack = _attacks.Leap,
                PhaseSpecials = _attacks.SpecialsForPhase(Phase),
                LastAttack = LastAttack,
            });

            switch (action.Kind)
            {
                case BossActionKind.Attack when action.Attack != null:
                    _comboLeft = Math.Max(0, action.ComboExtra);
                    StartAttack(action.Attack, playerOffsetX);
                    break;
                case BossActionKind.Move:
                    FaceTowards(playerOffsetX);
                    Enter(BossState.Move, Math.Max(0.01f, action.Duration));
                    VelocityX = Facing * _tuning.MoveSpeed;
                    break;
                default:
                    Enter(BossState.Idle, Math.Max(0.01f, action.Duration > 0 ? action.Duration : _tuning.IdleBetweenActions));
                    break;
            }
        }

        private void StartAttack(AttackDefinition attack, float playerOffsetX)
        {
            FaceTowards(playerOffsetX);
            CurrentAttack = attack;
            LastAttack = attack;
            Enter(BossState.Attack, 0);
            SetStage(AttackStage.Windup, attack.Windup);
        }

        private void AdvanceAttackStage()
        {
            switch (AttackStage)
            {
                case AttackStage.Windup:
                    SetStage(AttackStage.Active, CurrentAttack.Active);
                    break;
                case AttackStage.Active:
                    SetStage(AttackStage.Recovery, CurrentAttack.Recovery);
                    break;
                default:
                    if (_comboLeft > 0)
                    {
                        // Chain the same attack straight away, re-aiming at the player.
                        _comboLeft--;
                        var again = CurrentAttack;
                        float leftover = _timer;
                        EndAttack();
                        StartAttack(again, _lastPlayerOffsetX);
                        _timer += leftover;
                        break;
                    }
                    float selfStun = CurrentAttack.SelfStunAfter;
                    float carry = _timer;
                    EndAttack();
                    if (selfStun > 0) Enter(BossState.Stun, selfStun + carry);
                    else Enter(BossState.Idle, _tuning.IdleBetweenActions + carry);
                    break;
            }
        }

        private void SetStage(AttackStage stage, float duration)
        {
            AttackStage = stage;
            _timer += duration; // keeps leftover time from the previous stage
            AttackStageChanged?.Invoke(CurrentAttack, stage);
        }

        private void EndAttack()
        {
            if (State != BossState.Attack) return;
            var attack = CurrentAttack;
            AttackStage = AttackStage.None;
            CurrentAttack = null;
            AttackStageChanged?.Invoke(attack, AttackStage.None);
        }

        private void Enter(BossState next, float duration)
        {
            var previous = State;
            State = next;
            _timer = duration;
            if (next != BossState.Move && next != BossState.Attack) VelocityX = 0;
            if (next != BossState.Attack) _comboLeft = 0;
            StateChanged?.Invoke(previous, next);
        }

        private void FaceTowards(float playerOffsetX)
        {
            if (playerOffsetX > 0.01f) Facing = 1;
            else if (playerOffsetX < -0.01f) Facing = -1;
        }
    }
}
