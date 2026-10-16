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
    /// Subtle procedural support for frame animations with few drawings: anticipation crouch,
    /// a small snap when an attack fires, stretch while rising and a landing squash. Pure math so it
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

            // Calm by design: no idle breathing, walk bob, leaning or wobble (they read as the sprite
            // "shaking"). Only a short crouch before an attack, a tiny snap when it fires, a stretch
            // while rising and the landing squash, so the drawn frames carry the animation.
            if (state == BossState.Attack && stage == AttackStage.Windup)
                squash = -(attack != null && attack.IsSpecial ? 0.06f : 0.035f) * EaseOutCubic(p);

            if (airborne)
            {
                // Stretch while rising, a little less while falling.
                squash = Clamp(verticalSpeed * 0.008f, -0.05f, 0.08f);
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
        public void Land(float strength = 1f) { _squash.Kick(-2.2f * strength * Intensity); }

        /// <summary>Start of a strike: a quick forward snap.</summary>
        public void Strike(float strength = 1f) { _squash.Kick(0.6f * strength * Intensity); }

        /// <summary>Took a hit: small backwards flinch.</summary>
        public void Hit(float strength = 1f) { /* hits are shown by the white flash, not by deforming the sprite */ }

        private static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        private static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        private static float Clamp(float v, float min, float max) { return v < min ? min : v > max ? max : v; }
    }
}
