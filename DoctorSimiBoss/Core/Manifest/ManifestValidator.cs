using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DoctorSimiBoss.Core.Manifest
{
    public enum ClipKind { Animation, Effect }

    /// <summary>Result of checking one animation or effect against the file system.</summary>
    public sealed class ClipCheck
    {
        public string Name;
        public ClipKind Kind;
        public int DeclaredFrames;
        public readonly List<string> MissingFrames = new List<string>();
        public readonly List<string> InvalidFrames = new List<string>();

        public bool IsPlayable => DeclaredFrames > 0 && MissingFrames.Count == 0 && InvalidFrames.Count == 0;
    }

    /// <summary>How a planned special attack will be shown in this build.</summary>
    public sealed class SpecialCheck
    {
        public string Id;
        public string AssetStatus;
        public string PlannedAnimation;
        public bool HasPlayableAnimation;
        public string AnimationUsed;
    }

    public sealed class ManifestReport
    {
        public readonly List<ClipCheck> Clips = new List<ClipCheck>();
        public readonly List<SpecialCheck> Specials = new List<SpecialCheck>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsPlayable(string animationName)
        {
            return Clips.Any(c => c.Kind == ClipKind.Animation && c.Name == animationName && c.IsPlayable);
        }

        /// <summary>True when the two required placeholders (Idle and Attack) are usable.</summary>
        public bool PlaceholdersAvailable =>
            IsPlayable(ManifestValidator.IdlePlaceholder) && IsPlayable(ManifestValidator.AttackPlaceholder);

        /// <summary>Errors block the boss; warnings only mean a clip falls back to a placeholder.</summary>
        public bool HasErrors => Errors.Count > 0;

        public IEnumerable<string> ToLines()
        {
            foreach (var c in Clips)
            {
                string state = c.IsPlayable ? "OK" : "NOT PLAYABLE";
                string detail = "";
                if (c.MissingFrames.Count > 0) detail += " missing=" + string.Join(", ", c.MissingFrames.ToArray());
                if (c.InvalidFrames.Count > 0) detail += " invalid=" + string.Join(", ", c.InvalidFrames.ToArray());
                yield return c.Kind + " " + c.Name + ": " + state + " (" + c.DeclaredFrames + " frames)" + detail;
            }
            foreach (var s in Specials)
            {
                yield return "Special " + s.Id + " [" + (s.AssetStatus ?? "unknown") + "]: " +
                             (s.HasPlayableAnimation
                                 ? "uses " + s.AnimationUsed
                                 : "no sprites for " + s.PlannedAnimation + ", placeholder " + s.AnimationUsed);
            }
            foreach (var w in Warnings) yield return "WARNING: " + w;
            foreach (var e in Errors) yield return "ERROR: " + e;
        }
    }

    /// <summary>
    /// Checks that every frame listed in the manifest exists, is a PNG and matches the
    /// declared frame size, and that phases and specials are consistent.
    /// </summary>
    public static class ManifestValidator
    {
        public const string IdlePlaceholder = "Idle";
        public const string AttackPlaceholder = "Attack";

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <param name="manifest">Parsed manifest.</param>
        /// <param name="resolvePath">Maps a manifest path ("Assets/Sprites/...") to a file on disk.</param>
        public static ManifestReport Validate(AnimationManifest manifest, Func<string, string> resolvePath)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (resolvePath == null) throw new ArgumentNullException(nameof(resolvePath));

            var report = new ManifestReport();
            var conceptPaths = new HashSet<string>(manifest.ConceptReferences.Select(c => Normalize(c.Path)), StringComparer.OrdinalIgnoreCase);

            foreach (var clip in manifest.Animations.Values) report.Clips.Add(CheckClip(manifest, clip, ClipKind.Animation, resolvePath, conceptPaths, report));
            foreach (var clip in manifest.Effects.Values) report.Clips.Add(CheckClip(manifest, clip, ClipKind.Effect, resolvePath, conceptPaths, report));

            if (!report.IsPlayable(IdlePlaceholder))
                report.Errors.Add("Required placeholder animation '" + IdlePlaceholder + "' is missing or incomplete");
            if (!report.IsPlayable(AttackPlaceholder))
                report.Errors.Add("Required placeholder animation '" + AttackPlaceholder + "' is missing or incomplete");

            CheckPhases(manifest, report);
            CheckSpecials(manifest, report);
            CheckArena(manifest, resolvePath, report);
            CheckBoxes(manifest, report);
            CheckEncounter(manifest, report);
            return report;
        }

        private static void CheckArena(AnimationManifest manifest, Func<string, string> resolvePath, ManifestReport report)
        {
            var a = manifest.Arena;
            if (a == null) return;

            if (string.IsNullOrEmpty(a.Background))
            {
                report.Errors.Add("Arena has no background image");
            }
            else
            {
                string path = resolvePath(a.Background);
                if (path == null || !File.Exists(path))
                    report.Errors.Add("Arena background not found: " + a.Background);
                else
                {
                    string problem = CheckPng(path, a.ImageWidth, a.ImageHeight);
                    if (problem != null) report.Errors.Add("Arena background " + a.Background + ": " + problem);
                }
            }

            if (a.ImageWidth <= 0 || a.ImageHeight <= 0) report.Errors.Add("Arena imageSize must be positive");
            if (a.PixelsPerUnit <= 0) report.Errors.Add("Arena pixelsPerUnit must be positive");
            if (a.FloorY <= 0 || a.FloorY >= a.ImageHeight) report.Errors.Add("Arena floorY is outside the image");
            if (a.LeftWallX < 0 || a.RightWallX > a.ImageWidth || a.LeftWallX >= a.RightWallX)
                report.Errors.Add("Arena walls must satisfy 0 <= left < right <= image width");
            if (a.BossLandingX <= a.LeftWallX || a.BossLandingX >= a.RightWallX)
                report.Errors.Add("Arena bossLandingX must be between the walls");
            if (a.PlayerEntryX <= a.LeftWallX || a.PlayerEntryX >= a.RightWallX)
                report.Errors.Add("Arena playerEntryX must be between the walls");
        }

        private static void CheckBoxes(AnimationManifest manifest, ManifestReport report)
        {
            if (manifest.SpritePixelsPerUnit < 0) report.Errors.Add("spriteScale.pixelsPerUnit must be positive");
            if (manifest.BossScale <= 0) report.Errors.Add("spriteScale.bossScale must be positive");
            CheckBox(manifest, manifest.BodyBox, "hitboxes.body", report);
            CheckBox(manifest, manifest.PunchBox, "hitboxes.punch", report);
        }

        private static void CheckBox(AnimationManifest manifest, PixelRect box, string name, ManifestReport report)
        {
            if (box == null) return;
            if (box.Width <= 0 || box.Height <= 0) report.Errors.Add(name + " must have a positive size");
            if (box.X < 0 || box.Y < 0 || box.X + box.Width > manifest.FrameWidth || box.Y + box.Height > manifest.FrameHeight)
                report.Errors.Add(name + " must fit inside the " + manifest.FrameWidth + "x" + manifest.FrameHeight + " frame");
        }

        private static void CheckEncounter(AnimationManifest manifest, ManifestReport report)
        {
            var e = manifest.Encounter;
            if (e == null) return;
            if (e.EntranceAnimation != null && !manifest.Animations.ContainsKey(e.EntranceAnimation))
                report.Errors.Add("Encounter entranceAnimation '" + e.EntranceAnimation + "' is not a declared animation");
            else if (e.EntranceAnimation != null && !report.IsPlayable(e.EntranceAnimation))
                report.Warnings.Add("Entrance animation '" + e.EntranceAnimation + "' is not playable; the entrance will use " + IdlePlaceholder);
        }

        private static ClipCheck CheckClip(AnimationManifest manifest, AnimationClipInfo clip, ClipKind kind,
                                           Func<string, string> resolvePath, HashSet<string> conceptPaths, ManifestReport report)
        {
            var check = new ClipCheck { Name = clip.Name, Kind = kind, DeclaredFrames = clip.Frames.Count };
            if (clip.Frames.Count == 0) report.Errors.Add(kind + " '" + clip.Name + "' declares no frames");
            if (clip.Fps <= 0) report.Errors.Add(kind + " '" + clip.Name + "' has no positive fps");
            if (clip.SuggestedDamageFrame.HasValue &&
                (clip.SuggestedDamageFrame < 0 || clip.SuggestedDamageFrame >= clip.Frames.Count))
                report.Errors.Add(kind + " '" + clip.Name + "' suggestedDamageFrame is outside its frames");

            foreach (var frame in clip.Frames)
            {
                string normalized = Normalize(frame);
                if (normalized.IndexOf("References/", StringComparison.OrdinalIgnoreCase) >= 0 || conceptPaths.Contains(normalized))
                {
                    check.InvalidFrames.Add(frame);
                    report.Errors.Add(kind + " '" + clip.Name + "' points to concept art, which must not be loaded as a sprite: " + frame);
                    continue;
                }

                string path = resolvePath(frame);
                if (path == null || !File.Exists(path))
                {
                    check.MissingFrames.Add(frame);
                    continue;
                }

                string problem = CheckPng(path, clip.FrameWidth ?? manifest.FrameWidth, clip.FrameHeight ?? manifest.FrameHeight);
                if (problem != null)
                {
                    check.InvalidFrames.Add(frame);
                    report.Warnings.Add(frame + ": " + problem);
                }
            }

            // Incomplete clips are warnings: the boss falls back to Idle/Attack. Missing placeholders
            // are reported as errors by Validate.
            if (check.MissingFrames.Count > 0)
                report.Warnings.Add(kind + " '" + clip.Name + "' is missing " + check.MissingFrames.Count + " frame file(s)");
            return check;
        }

        /// <summary>Returns null when the file is a PNG of the expected size, or a description of the problem.</summary>
        public static string CheckPng(string path, int expectedWidth, int expectedHeight)
        {
            var header = new byte[24];
            int read;
            using (var stream = File.OpenRead(path))
            {
                read = 0;
                while (read < header.Length)
                {
                    int n = stream.Read(header, read, header.Length - read);
                    if (n == 0) break;
                    read += n;
                }
            }
            if (read < header.Length) return "file too short to be a PNG";
            for (int i = 0; i < PngSignature.Length; i++)
                if (header[i] != PngSignature[i]) return "not a PNG file";

            // The first chunk of a valid PNG is IHDR: width and height are big-endian at offsets 16 and 20.
            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            if (expectedWidth > 0 && expectedHeight > 0 && (width != expectedWidth || height != expectedHeight))
                return "size " + width + "x" + height + " does not match frameSize " + expectedWidth + "x" + expectedHeight;
            return null;
        }

        private static void CheckPhases(AnimationManifest manifest, ManifestReport report)
        {
            if (manifest.Phases.Count == 0)
            {
                report.Errors.Add("Manifest declares no encounterPhases");
                return;
            }

            var ordered = manifest.Phases.OrderByDescending(p => p.HealthFrom).ToList();
            if (ordered[0].HealthFrom < 100) report.Errors.Add("First phase must start at 100% health");
            if (ordered[ordered.Count - 1].HealthTo > 0) report.Errors.Add("Last phase must end at 0% health");
            for (int i = 0; i < ordered.Count; i++)
            {
                var phase = ordered[i];
                if (phase.HealthTo > phase.HealthFrom)
                    report.Errors.Add("Phase " + phase.Id + " has healthPercent.to above healthPercent.from");
                // Integer percents are used in the manifest (100-66, 65-33...), so a gap of one point is contiguous.
                if (i > 0 && ordered[i - 1].HealthTo - phase.HealthFrom > 1)
                    report.Errors.Add("Gap in health ranges between phase " + ordered[i - 1].Id + " and phase " + phase.Id);
                if (i > 0 && phase.HealthFrom >= ordered[i - 1].HealthTo)
                    report.Errors.Add("Phase " + phase.Id + " overlaps phase " + ordered[i - 1].Id);
            }

            var specialIds = new HashSet<string>(manifest.PlannedSpecials.Select(s => s.Id));
            foreach (var phase in manifest.Phases)
                foreach (var id in phase.Specials)
                    if (!specialIds.Contains(id))
                        report.Errors.Add("Phase " + phase.Id + " lists unknown special '" + id + "'");
        }

        private static void CheckSpecials(AnimationManifest manifest, ManifestReport report)
        {
            var phaseIds = new HashSet<int>(manifest.Phases.Select(p => p.Id));
            foreach (var special in manifest.PlannedSpecials)
            {
                if (!phaseIds.Contains(special.Phase))
                    report.Errors.Add("Special '" + special.Id + "' refers to unknown phase " + special.Phase);

                bool declaredReady = special.AssetStatus == AssetStatus.Ready;
                bool playable = declaredReady && special.Animation != null && report.IsPlayable(special.Animation);
                if (declaredReady && !playable)
                    report.Warnings.Add("Special '" + special.Id + "' is marked ready but animation '" + special.Animation +
                                        "' is not playable; falling back to " + AttackPlaceholder);

                report.Specials.Add(new SpecialCheck
                {
                    Id = special.Id,
                    AssetStatus = special.AssetStatus,
                    PlannedAnimation = special.Animation,
                    HasPlayableAnimation = playable,
                    AnimationUsed = playable ? special.Animation : AttackPlaceholder,
                });
            }
        }

        private static string Normalize(string path)
        {
            return (path ?? "").Replace('\\', '/');
        }
    }

    /// <summary>Ways to map manifest paths ("Assets/...") to files on disk.</summary>
    public static class ManifestPaths
    {
        /// <summary>Installed or built mod: Assets/ sits next to the DLL.</summary>
        public static Func<string, string> ForModDirectory(string modDirectory)
        {
            return p => Path.Combine(modDirectory, p.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Source package layout: "Assets/Sprites/..." lives at "&lt;package&gt;/Sprites/...",
        /// matching the Content links in DoctorSimiBoss.csproj.
        /// </summary>
        public static Func<string, string> ForSourcePackage(string packageRoot)
        {
            const string prefix = "Assets/";
            return p =>
            {
                string relative = p.Replace('\\', '/');
                if (relative.StartsWith(prefix, StringComparison.Ordinal)) relative = relative.Substring(prefix.Length);
                return Path.Combine(packageRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            };
        }
    }
}
