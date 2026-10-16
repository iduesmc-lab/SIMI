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
        public readonly List<string> Frames = new List<string>();
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
    }

    public static class AssetStatus
    {
        public const string Ready = "ready";
        public const string ConceptOnly = "concept-only";
        public const string DesignOnly = "design-only";
    }
}
