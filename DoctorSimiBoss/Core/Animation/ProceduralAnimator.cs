using System;
using DoctorSimiBoss.Core.Combat;

namespace DoctorSimiBoss.Core.Animation
{
    /// <summary>Extra deformation applied on top of the sprite frames. Pivot is the feet.</summary>
    public struct VisualPose
    {
        public float ScaleX;
        public float ScaleY;
        /// <summary>Lean in degrees; positive leans towards where the boss is facing.</summary>
        public float Lean;
        public float OffsetY;

        public static readonly VisualPose Neutral = new VisualPose { ScaleX = 1f, ScaleY = 1f };
    }

    /// <summary>Damped spring: follows a target with a natural overshoot; kicks add impulses.</summary>
    public sealed class Spring
    {
        public float Value;
        public float Velocity;
        public float Stiffness;
        public float Damping;

        public Spring(float stiffness, float damping)
        {
            Stiffness = stiffness;
            Damping = damping;
        }

        public void Tick(float dt, float target)
        {
            // Sub-steps keep it stable on long frames.
            int steps = Math.Max(1, (int)Math.Ceiling(dt / (1f / 120f)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float accel = -Stiffness * (Value - target) - Damping * Velocity;
                Velocity += accel * h;
                Value += Velocity * h;
            }
        }

        public void Kick(float velocity) { Velocity += velocity; }
    }

    /// <summary>
    /// Procedural "juice" for frame animations with few drawings: breathing, walk bob, anticipation
    /// squash, strike stretch, jump stretch, landing squash, hit flinch, stun wobble. Pure math so it
    /// can be tested without the game; the scene layer applies <see cref="Pose"/> to the sprite.
    /// </summary>
    public sealed class ProceduralAnimator
    {
        // Squash: + taller/thinner, - shorter/wider. Volume is roughly preserved.
        private readonly Spring _squash = new Spring(260f, 14f);
        private readonly Spring _lean = new Spring(420f, 26f);
        private readonly Spring _bob = new Spring(300f, 22f);
        private float _time;

        /// <summary>Strength multiplier for every effect (0 disables, 1 default).</summary>
        public float Intensity = 1f;

        public VisualPose Pose { get; private set; } = VisualPose.Neutral;

        /// <param name="stageProgress">0..1 progress through the current attack stage.</param>
        /// <param name="verticalSpeed">Upward speed while airborne (units/s), 0 on the ground.</param>
        public void Tick(float dt, BossState state, AttackStage stage, AttackDefinition attack, float stageProgress,
                         float horizontalSpeed, bool airborne, float verticalSpeed)
        {
            if (dt <= 0) return;
            _time += dt;
            float squash = 0f, lean = 0f, bob = 0f;
            float p = Clamp01(stageProgress);

            switch (state)
            {
                case BossState.Idle:
                    squash = 0.025f * (float)Math.Sin(_time * 2 * Math.PI * 0.7);
                    break;

                case BossState.Move:
                    float step = Math.Abs((float)Math.Sin(_time * 2 * Math.PI * 1.6));
                    bob = 0.06f * step;
                    squash = 0.035f * step - 0.015f;
                    lean = 5f;
                    break;

                case BossState.Attack:
                    bool heavy = attack != null && attack.IsSpecial;
                    bool dash = attack != null && attack.ActiveSpeedMultiplier >= 3f;
                    if (stage == AttackStage.Windup)
                    {
                        float e = EaseOutCubic(p);
                        squash = -(heavy ? 0.12f : 0.07f) * e;
                        lean = -(heavy ? 9f : 5f) * e;
                    }
                    else if (stage == AttackStage.Active)
                    {
                        squash = dash ? -0.04f : 0.04f;
                        lean = dash ? 14f : 7f;
                    }
                    else if (stage == AttackStage.Recovery)
                    {
                        lean = 3f * (1f - p);
                    }
                    break;

                case BossState.Hurt:
                    squash = -0.05f;
                    lean = -6f;
                    break;

                case BossState.Stun:
                    lean = 7f * (float)Math.Sin(_time * 2 * Math.PI * 1.1);
                    squash = -0.03f + 0.02f * (float)Math.Sin(_time * 2 * Math.PI * 2.2);
                    break;

                case BossState.PhaseTransition:
                    squash = 0.035f * (float)Math.Sin(_time * 2 * Math.PI * 7);
                    break;
            }

            if (airborne)
            {
                // Stretch while rising, a little less while falling.
                squash = Clamp(verticalSpeed * 0.012f, -0.08f, 0.14f);
                bob = 0f;
            }

            _squash.Tick(dt, squash * Intensity);
            _lean.Tick(dt, lean * Intensity);
            _bob.Tick(dt, bob * Intensity);

            float s = Clamp(_squash.Value, -0.35f, 0.35f);
            Pose = new VisualPose
            {
                ScaleY = 1f + s,
                ScaleX = 1f - s * 0.6f,
                Lean = Clamp(_lean.Value, -25f, 25f),
                OffsetY = _bob.Value,
            };
        }

        /// <summary>Landing after a jump or slam: squash that springs back.</summary>
        public void Land(float strength = 1f) { _squash.Kick(-3.2f * strength * Intensity); }

        /// <summary>Start of a strike: a quick forward snap.</summary>
        public void Strike(float strength = 1f)
        {
            // Snap out of the backwards anticipation lean: a strike reads best as an instant change.
            if (_lean.Value < 0f) { _lean.Value *= 0.2f; _lean.Velocity = 0f; }
            _lean.Kick(320f * strength * Intensity);
            _squash.Kick(1.2f * strength * Intensity);
        }

        /// <summary>Took a hit: small backwards flinch.</summary>
        public void Hit(float strength = 1f)
        {
            _squash.Kick(-1.6f * strength * Intensity);
            _lean.Kick(-90f * strength * Intensity);
        }

        private static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        private static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        private static float Clamp(float v, float min, float max) { return v < min ? min : v > max ? max : v; }
    }
}
