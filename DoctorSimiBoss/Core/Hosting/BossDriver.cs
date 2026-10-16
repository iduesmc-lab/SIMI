using System;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;

namespace DoctorSimiBoss.Core.Hosting
{
    /// <summary>
    /// Connects a <see cref="BossStateMachine"/> to an <see cref="IBossBody"/>: each tick it feeds
    /// the player's position in and pushes animation, facing, velocity and hitbox changes out.
    /// </summary>
    public sealed class BossDriver
    {
        private readonly BossStateMachine _machine;
        private readonly BossAnimationResolver _animations;
        private readonly IBossBody _body;

        private AnimationRequest? _currentAnimation;
        private AttackDefinition _activeHitbox;
        private int _lastFacing;
        private float _lastVelocity = float.NaN;

        public BossStateMachine Machine => _machine;

        public BossDriver(BossStateMachine machine, BossAnimationResolver animations, IBossBody body)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            _animations = animations ?? throw new ArgumentNullException(nameof(animations));
            _body = body ?? throw new ArgumentNullException(nameof(body));

            _machine.StateChanged += OnStateChanged;
            _machine.AttackStageChanged += OnAttackStageChanged;
        }

        public void Tick(float deltaTime)
        {
            _machine.Tick(deltaTime, _body.PlayerOffsetX);
            Sync();
        }

        public HitResult ReceiveHit(int damage)
        {
            var result = _machine.ReceiveHit(damage);
            Sync();
            return result;
        }

        private void Sync()
        {
            if (_machine.Facing != _lastFacing)
            {
                _lastFacing = _machine.Facing;
                _body.SetFacing(_lastFacing);
            }
            if (!_machine.VelocityX.Equals(_lastVelocity))
            {
                _lastVelocity = _machine.VelocityX;
                _body.SetVelocityX(_lastVelocity);
            }
        }

        private void OnStateChanged(BossState previous, BossState next)
        {
            // Re-entering the same state (Idle -> Idle) does not restart the clip; a new attack always does.
            var request = _animations.Resolve(next, _machine.CurrentAttack);
            if (next == BossState.Attack || !_currentAnimation.HasValue || !_currentAnimation.Value.Equals(request))
            {
                _currentAnimation = request;
                _body.PlayAnimation(request);
            }
            if (next == BossState.Defeated) _body.OnDefeated();
        }

        private void OnAttackStageChanged(AttackDefinition attack, AttackStage stage)
        {
            if (stage == AttackStage.Active)
            {
                _activeHitbox = attack;
                _body.SetAttackActive(attack, true);
                if (attack.ImpactEffect != null) _body.PlayEffect(attack.ImpactEffect);
            }
            else if (_activeHitbox != null)
            {
                _body.SetAttackActive(_activeHitbox, false);
                _activeHitbox = null;
            }
        }
    }
}
