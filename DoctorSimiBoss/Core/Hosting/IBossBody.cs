using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;

namespace DoctorSimiBoss.Core.Hosting
{
    /// <summary>
    /// Everything the boss logic needs from the game. The scene integration (not written yet,
    /// pending the target game version and test scene) implements this on the boss GameObject;
    /// the core never touches Unity or Modding API types directly.
    /// </summary>
    public interface IBossBody
    {
        /// <summary>Player X minus boss X, in world units.</summary>
        float PlayerOffsetX { get; }

        void PlayAnimation(AnimationRequest request);

        /// <summary>-1 left, +1 right. Sprites are drawn facing the manifest's spriteFacing.</summary>
        void SetFacing(int direction);

        void SetVelocityX(float velocity);

        /// <summary>Enable or disable the damaging area of an attack.</summary>
        void SetAttackActive(AttackDefinition attack, bool active);

        void PlayEffect(string effectName);

        void OnDefeated();
    }
}
