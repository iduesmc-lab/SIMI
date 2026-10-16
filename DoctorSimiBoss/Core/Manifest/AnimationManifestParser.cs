using System;
using System.Collections.Generic;
using System.Linq;
using DoctorSimiBoss.Core.Json;

namespace DoctorSimiBoss.Core.Manifest
{
    /// <summary>
    /// Converts animation_manifest.json text into an <see cref="AnimationManifest"/>.
    /// Structural problems throw <see cref="FormatException"/>; checking that the
    /// referenced files exist is the job of <see cref="ManifestValidator"/>.
    /// </summary>
    public static class AnimationManifestParser
    {
        public const int SupportedSchemaVersion = 1;

        public static AnimationManifest Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Manifest root must be a JSON object");

            var manifest = new AnimationManifest
            {
                Name = GetString(root, "name"),
                SchemaVersion = (int)GetNumber(root, "schemaVersion", 0),
                Status = GetString(root, "status"),
            };
            if (manifest.SchemaVersion != SupportedSchemaVersion)
                throw new FormatException("Unsupported schemaVersion " + manifest.SchemaVersion +
                                          " (expected " + SupportedSchemaVersion + ")");

            var frameSize = GetObject(root, "frameSize");
            if (frameSize != null)
            {
                manifest.FrameWidth = (int)GetNumber(frameSize, "width", 0);
                manifest.FrameHeight = (int)GetNumber(frameSize, "height", 0);
            }

            var origin = GetObject(root, "origin");
            if (origin != null)
            {
                manifest.OriginX = (float)GetNumber(origin, "x", manifest.OriginX);
                manifest.OriginY = (float)GetNumber(origin, "y", manifest.OriginY);
            }

            string facing = GetString(root, "spriteFacing");
            if (facing != null)
            {
                if (facing != "left" && facing != "right") throw new FormatException("spriteFacing must be \"left\" or \"right\"");
                manifest.SpriteFacing = facing;
            }

            var scale = GetObject(root, "spriteScale");
            if (scale != null) manifest.SpritePixelsPerUnit = (float)GetNumber(scale, "pixelsPerUnit", 0);

            var hitboxes = GetObject(root, "hitboxes");
            if (hitboxes != null)
            {
                manifest.BodyBox = ReadRect(GetObject(hitboxes, "body"));
                manifest.PunchBox = ReadRect(GetObject(hitboxes, "punch"));
            }

            ReadClips(GetObject(root, "animations"), manifest.Animations);
            ReadClips(GetObject(root, "effects"), manifest.Effects);

            foreach (var item in GetArray(root, "encounterPhases"))
            {
                var p = AsObject(item, "encounterPhases[]");
                var phase = new EncounterPhase
                {
                    Id = (int)GetNumber(p, "id", 0),
                    Name = GetString(p, "name"),
                    IncludesPreviousSpecials = GetBool(p, "includesPreviousSpecials", false),
                };
                var range = GetObject(p, "healthPercent");
                if (range == null) throw new FormatException("Phase " + phase.Id + " has no healthPercent");
                phase.HealthFrom = (float)GetNumber(range, "from", 100);
                phase.HealthTo = (float)GetNumber(range, "to", 0);
                foreach (var s in GetArray(p, "specials")) phase.Specials.Add(s as string);
                manifest.Phases.Add(phase);
            }

            foreach (var item in GetArray(root, "plannedSpecials"))
            {
                var s = AsObject(item, "plannedSpecials[]");
                var special = new PlannedSpecial
                {
                    Id = GetString(s, "id"),
                    DisplayName = GetString(s, "displayName"),
                    Phase = (int)GetNumber(s, "phase", 0),
                    Animation = GetString(s, "animation"),
                    AssetStatus = GetString(s, "assetStatus"),
                    FrameCount = (int)GetNumber(s, "frameCount", 0),
                    ConceptSheet = GetString(s, "conceptSheet"),
                    PlaceholderAnimation = GetString(s, "placeholderAnimation"),
                };
                foreach (var pose in GetArray(s, "conceptPoses")) special.ConceptPoses.Add((int)ToDouble(pose, "conceptPoses[]"));

                var timing = GetObject(s, "prototypeTiming");
                if (timing != null)
                {
                    special.Timing = new AttackTimingData
                    {
                        Windup = GetOptionalFloat(timing, "windup"),
                        Active = GetOptionalFloat(timing, "active"),
                        Recovery = GetOptionalFloat(timing, "recovery"),
                        SelfStunAfter = GetOptionalFloat(timing, "selfStunAfter"),
                    };
                }
                manifest.PlannedSpecials.Add(special);
            }

            foreach (var item in GetArray(root, "conceptReferences"))
            {
                var c = AsObject(item, "conceptReferences[]");
                manifest.ConceptReferences.Add(new ConceptReference { Path = GetString(c, "path"), Note = GetString(c, "note") });
            }

            var tuning = GetObject(root, "tuning");
            if (tuning != null)
            {
                manifest.Tuning = new BossTuningData
                {
                    MaxHealth = GetOptionalInt(tuning, "maxHealth"),
                    MoveSpeed = GetOptionalFloat(tuning, "moveSpeed"),
                    AttackRange = GetOptionalFloat(tuning, "attackRange"),
                    IdleBetweenActions = GetOptionalFloat(tuning, "idleBetweenActions"),
                    MoveDuration = GetOptionalFloat(tuning, "moveDuration"),
                    HurtDuration = GetOptionalFloat(tuning, "hurtDuration"),
                    StunHitThreshold = GetOptionalInt(tuning, "stunHitThreshold"),
                    StunDuration = GetOptionalFloat(tuning, "stunDuration"),
                    PhaseTransitionDuration = GetOptionalFloat(tuning, "phaseTransitionDuration"),
                    SpecialChance = GetOptionalFloat(tuning, "specialChance"),
                    IntroDuration = GetOptionalFloat(tuning, "introDuration"),
                    ContactDamage = GetOptionalInt(tuning, "contactDamage"),
                    AttackDamage = GetOptionalInt(tuning, "attackDamage"),
                    MaxCombo = GetOptionalInt(tuning, "maxCombo"),
                    PhaseSpeed = tuning.ContainsKey("phaseSpeed") ? GetArray(tuning, "phaseSpeed").Select(v => (float)ToDouble(v, "phaseSpeed[]")).ToArray() : null,
                };
            }

            var arena = GetObject(root, "arena");
            if (arena != null)
            {
                var size = GetObject(arena, "imageSize");
                var walls = GetObject(arena, "walls");
                manifest.Arena = new ArenaInfo
                {
                    Background = GetString(arena, "background"),
                    ImageWidth = size != null ? (int)GetNumber(size, "width", 0) : 0,
                    ImageHeight = size != null ? (int)GetNumber(size, "height", 0) : 0,
                    PixelsPerUnit = (float)GetNumber(arena, "pixelsPerUnit", 0),
                    FloorY = (float)GetNumber(arena, "floorY", 0),
                    LeftWallX = walls != null ? (float)GetNumber(walls, "left", 0) : 0,
                    RightWallX = walls != null ? (float)GetNumber(walls, "right", 0) : 0,
                    BossLandingX = (float)GetNumber(arena, "bossLandingX", 0),
                    PlayerEntryX = (float)GetNumber(arena, "playerEntryX", 0),
                };
            }

            var grade = GetObject(root, "colorGrade");
            if (grade != null)
            {
                var g = new DoctorSimiBoss.Core.Animation.ColorGrade
                {
                    Enabled = GetBool(grade, "enabled", true),
                    Saturation = (float)GetNumber(grade, "saturation", 1),
                    Brightness = (float)GetNumber(grade, "brightness", 1),
                    Contrast = (float)GetNumber(grade, "contrast", 1),
                    TintAmount = (float)GetNumber(grade, "tintAmount", 0),
                };
                var tint = GetArray(grade, "tint").ToList();
                if (tint.Count == 3)
                {
                    g.TintR = (byte)Math.Max(0, Math.Min(255, ToDouble(tint[0], "tint[]")));
                    g.TintG = (byte)Math.Max(0, Math.Min(255, ToDouble(tint[1], "tint[]")));
                    g.TintB = (byte)Math.Max(0, Math.Min(255, ToDouble(tint[2], "tint[]")));
                }
                manifest.ColorGrade = g;
            }

            var encounter = GetObject(root, "encounter");
            if (encounter != null)
            {
                manifest.Encounter = new EncounterInfo
                {
                    Access = GetString(encounter, "access"),
                    DebugKey = GetString(encounter, "debugKey"),
                    EntranceAnimation = GetString(encounter, "entranceAnimation"),
                };
            }

            return manifest;
        }

        private static void ReadClips(Dictionary<string, object> source, Dictionary<string, AnimationClipInfo> target)
        {
            if (source == null) return;
            foreach (var pair in source)
            {
                var c = AsObject(pair.Value, pair.Key);
                var clip = new AnimationClipInfo
                {
                    Name = pair.Key,
                    Loop = GetBool(c, "loop", false),
                    Fps = (float)GetNumber(c, "fps", 0),
                };
                if (c.ContainsKey("suggestedDamageFrame"))
                    clip.SuggestedDamageFrame = (int)GetNumber(c, "suggestedDamageFrame", 0);
                var size = GetObject(c, "frameSize");
                if (size != null)
                {
                    clip.FrameWidth = (int)GetNumber(size, "width", 0);
                    clip.FrameHeight = (int)GetNumber(size, "height", 0);
                }
                var origin = GetObject(c, "origin");
                if (origin != null)
                {
                    clip.OriginX = GetOptionalFloat(origin, "x");
                    clip.OriginY = GetOptionalFloat(origin, "y");
                }
                foreach (var f in GetArray(c, "frames"))
                {
                    var path = f as string;
                    if (string.IsNullOrEmpty(path)) throw new FormatException("Clip '" + pair.Key + "' has a non-string frame entry");
                    clip.Frames.Add(path);
                }
                target[pair.Key] = clip;
            }
        }

        private static PixelRect ReadRect(Dictionary<string, object> obj)
        {
            if (obj == null) return null;
            return new PixelRect
            {
                X = (float)GetNumber(obj, "x", 0),
                Y = (float)GetNumber(obj, "y", 0),
                Width = (float)GetNumber(obj, "width", 0),
                Height = (float)GetNumber(obj, "height", 0),
            };
        }

        private static Dictionary<string, object> AsObject(object value, string where)
        {
            var obj = value as Dictionary<string, object>;
            if (obj == null) throw new FormatException("'" + where + "' must be an object");
            return obj;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key)
        {
            return obj.TryGetValue(key, out object v) && v != null ? AsObject(v, key) : null;
        }

        private static IEnumerable<object> GetArray(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object v) || v == null) return Array.Empty<object>();
            var list = v as List<object>;
            if (list == null) throw new FormatException("'" + key + "' must be an array");
            return list;
        }

        private static string GetString(Dictionary<string, object> obj, string key)
        {
            return obj.TryGetValue(key, out object v) ? v as string : null;
        }

        private static bool GetBool(Dictionary<string, object> obj, string key, bool fallback)
        {
            return obj.TryGetValue(key, out object v) && v is bool b ? b : fallback;
        }

        private static double GetNumber(Dictionary<string, object> obj, string key, double fallback)
        {
            return obj.TryGetValue(key, out object v) && v != null ? ToDouble(v, key) : fallback;
        }

        private static float? GetOptionalFloat(Dictionary<string, object> obj, string key)
        {
            return obj.TryGetValue(key, out object v) && v != null ? (float)ToDouble(v, key) : (float?)null;
        }

        private static int? GetOptionalInt(Dictionary<string, object> obj, string key)
        {
            return obj.TryGetValue(key, out object v) && v != null ? (int)ToDouble(v, key) : (int?)null;
        }

        private static double ToDouble(object value, string key)
        {
            if (value is double d) return d;
            throw new FormatException("'" + key + "' must be a number");
        }
    }
}
