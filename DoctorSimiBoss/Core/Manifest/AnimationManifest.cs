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
    }

    public static class AssetStatus
    {
        public const string Ready = "ready";
        public const string ConceptOnly = "concept-only";
        public const string DesignOnly = "design-only";
    }
}
