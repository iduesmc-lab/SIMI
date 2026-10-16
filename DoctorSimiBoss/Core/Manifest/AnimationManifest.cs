using System.Collections.Generic;

namespace DoctorSimiBoss.Core.Manifest
{
    /// <summary>In-memory form of animation_manifest.json.</summary>
    public sealed class AnimationManifest
    {
        public string Name;
        public int SchemaVersion;
        public string Status;
        public int FrameWidth;
        public int FrameHeight;
        public float OriginX = 0.5f;
        public float OriginY = 0.9f;
        /// <summary>Direction the sprites are drawn facing: "left" or "right".</summary>
        public string SpriteFacing = "left";
        /// <summary>Frame pixels per game world unit for the boss sprites; 0 when not declared.</summary>
        public float SpritePixelsPerUnit;
        /// <summary>Extra in-game size multiplier for the boss (sprite and its hit areas); 1 = as drawn.</summary>
        public float BossScale = 1f;
        /// <summary>Hurtbox in frame pixels, or null.</summary>
        public PixelRect BodyBox;
        /// <summary>Damaging area of the punch in frame pixels, or null.</summary>
        public PixelRect PunchBox;

        /// <summary>Character animations keyed by name (Idle, Attack, Walk...).</summary>
        public readonly Dictionary<string, AnimationClipInfo> Animations = new Dictionary<string, AnimationClipInfo>();

        /// <summary>Standalone effects keyed by name (PunchImpact...).</summary>
        public readonly Dictionary<string, AnimationClipInfo> Effects = new Dictionary<string, AnimationClipInfo>();

        /// <summary>Encounter phases in manifest order.</summary>
        public readonly List<EncounterPhase> Phases = new List<EncounterPhase>();

        /// <summary>Special attacks from the combat design; most have no game-ready sprites yet.</summary>
        public readonly List<PlannedSpecial> PlannedSpecials = new List<PlannedSpecial>();

        /// <summary>Files that are reference art only and must never be loaded as sprites.</summary>
        public readonly List<ConceptReference> ConceptReferences = new List<ConceptReference>();

        /// <summary>Optional prototype tuning; null when the manifest has no "tuning" block.</summary>
        public BossTuningData Tuning;

        /// <summary>Arena background and walkable area; null when the manifest has no "arena" block.</summary>
        public ArenaInfo Arena;

        /// <summary>How the fight is reached and starts; null when the manifest has no "encounter" block.</summary>
        public EncounterInfo Encounter;

        /// <summary>Colour adjustment for boss and effect sprites; disabled when the manifest has no "colorGrade" block.</summary>
        public DoctorSimiBoss.Core.Animation.ColorGrade ColorGrade = DoctorSimiBoss.Core.Animation.ColorGrade.None;

        /// <summary>Procedural motion settings ("motion" block).</summary>
        public MotionSettings Motion = new MotionSettings();

        /// <summary>Boss title, music and sounds ("presentation" block).</summary>
        public PresentationSettings Presentation = new PresentationSettings();
    }

    public sealed class PresentationSettings
    {
        /// <summary>Name shown on screen when the fight starts; empty disables the title.</summary>
        public string Title = "DOCTOR SIMI";
        public bool Music = true;
        /// <summary>Exact name of the MusicCue to use; null picks the first available candidate.</summary>
        public string MusicCue;
        /// <summary>(scene, object path) pairs preloaded at startup to keep their scene music cues in memory.</summary>
        public readonly List<KeyValuePair<string, string>> MusicPreloads = new List<KeyValuePair<string, string>>();
        public bool TelegraphSound = true;
    }

    public sealed class MotionSettings
    {
        /// <summary>Strength of squash/stretch/lean effects; 0 disables them.</summary>
        public float Intensity = 1f;
        /// <summary>Horizontal acceleration in units/s² for walking starts and stops.</summary>
        public float Acceleration = 45f;
        public bool Afterimages = true;
    }

    /// <summary>
    /// Arena drawn as a single background image. All coordinates are pixels of that image,
    /// origin top-left, measured by eye and still to be calibrated in-game.
    /// </summary>
    public sealed class ArenaInfo
    {
        public string Background;
        public int ImageWidth;
        public int ImageHeight;
        /// <summary>Image pixels per game world unit; sets the arena's size in the scene.</summary>
        public float PixelsPerUnit;
        /// <summary>Pixel row where feet rest on the stone platform.</summary>
        public float FloorY;
        /// <summary>Pixel columns of the invisible walls at each end of the platform.</summary>
        public float LeftWallX;
        public float RightWallX;
        /// <summary>Pixel column where the boss lands after its entrance jump.</summary>
        public float BossLandingX;
        /// <summary>Pixel column where the player is placed when entering the arena.</summary>
        public float PlayerEntryX;
        /// <summary>Draw animated rain over the arena.</summary>
        public bool Rain;
    }

    public sealed class EncounterInfo
    {
        /// <summary>"debugKey" for now: a key press in any room takes the player to the arena.</summary>
        public string Access;
        /// <summary>Key name for the debug access; provisional until checked against other installed mods.</summary>
        public string DebugKey;
        /// <summary>Animation played by the boss when it enters (Spawn: jumps in and lands).</summary>
        public string EntranceAnimation;
    }

    /// <summary>Rectangle in frame pixels: origin at the top-left corner, sprite facing <see cref="AnimationManifest.SpriteFacing"/>.</summary>
    public sealed class PixelRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }

    public sealed class AnimationClipInfo
    {
        public string Name;
        public bool Loop;
        public float Fps;
        public int? SuggestedDamageFrame;
        /// <summary>Frame that should start exactly when an attack's active part ends (landing, slam impact); null if none.</summary>
        public int? ActiveEndFrame;
        /// <summary>Per-clip frame size; null means the manifest's frameSize.</summary>
        public int? FrameWidth;
        public int? FrameHeight;
        /// <summary>Per-clip normalized pivot from the top-left; null means the default (manifest origin for animations, centre for effects).</summary>
        public float? OriginX;
        public float? OriginY;
        public readonly List<string> Frames = new List<string>();
        /// <summary>
        /// Relative duration of each frame (1 = normal); null means all equal. Lets key poses
        /// (anticipation, impact, landing) hold longer while in-betweens pass quickly.
        /// </summary>
        public float[] FrameWeights;

        public float WeightOf(int frame)
        {
            return FrameWeights != null && frame >= 0 && frame < FrameWeights.Length ? FrameWeights[frame] : 1f;
        }

        /// <summary>Sum of the weights of the frames before <paramref name="frame"/>.</summary>
        public float WeightBefore(int frame)
        {
            float sum = 0;
            for (int i = 0; i < frame && i < Frames.Count; i++) sum += WeightOf(i);
            return sum;
        }

        public float TotalWeight => WeightBefore(Frames.Count);
    }

    public sealed class EncounterPhase
    {
        public int Id;
        public string Name;
        /// <summary>Upper bound of remaining health percent (inclusive), e.g. 100.</summary>
        public float HealthFrom;
        /// <summary>Lower bound of remaining health percent (inclusive), e.g. 66.</summary>
        public float HealthTo;
        /// <summary>When true the phase also uses the specials of every earlier phase.</summary>
        public bool IncludesPreviousSpecials;
        public readonly List<string> Specials = new List<string>();
    }

    public sealed class PlannedSpecial
    {
        public string Id;
        public string DisplayName;
        public int Phase;
        /// <summary>Name of the dedicated animation once it exists (SpecialPunch...).</summary>
        public string Animation;
        /// <summary>"concept-only", "design-only" or "ready".</summary>
        public string AssetStatus;
        public int FrameCount;
        public readonly List<int> ConceptPoses = new List<int>();
        public string ConceptSheet;
        /// <summary>Existing clip shown until the dedicated animation exists (e.g. Spawn for jumps); null means Attack.</summary>
        public string PlaceholderAnimation;
        /// <summary>Optional timing overrides; null means use the code defaults.</summary>
        public AttackTimingData Timing;
    }

    public sealed class AttackTimingData
    {
        public float? Windup;
        public float? Active;
        public float? Recovery;
        public float? SelfStunAfter;
    }

    public sealed class ConceptReference
    {
        public string Path;
        public string Note;
    }

    /// <summary>Raw optional tuning values; missing values stay null and fall back to defaults.</summary>
    public sealed class BossTuningData
    {
        public int? MaxHealth;
        public float? MoveSpeed;
        public float? AttackRange;
        public float? IdleBetweenActions;
        public float? MoveDuration;
        public float? HurtDuration;
        public int? StunHitThreshold;
        public float? StunDuration;
        public float? PhaseTransitionDuration;
        public float? SpecialChance;
        public float? IntroDuration;
        public int? ContactDamage;
        public int? AttackDamage;
        public int? MaxCombo;
        /// <summary>Speed multiplier per phase (index 0 = phase 1); null when not declared.</summary>
        public float[] PhaseSpeed;
    }

    public static class AssetStatus
    {
        public const string Ready = "ready";
        public const string ConceptOnly = "concept-only";
        public const string DesignOnly = "design-only";
    }
}
