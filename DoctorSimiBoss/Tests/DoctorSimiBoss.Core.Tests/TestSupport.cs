using System;
using System.Collections.Generic;
using System.IO;
using DoctorSimiBoss.Core;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Hosting;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Tests
{
    internal static class TestSupport
    {
        /// <summary>The DoctorSimiBoss package folder in this repository.</summary>
        public static string PackageRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "ModIntegration", "animation_manifest.json");
                    if (File.Exists(candidate)) return dir.FullName;
                    dir = dir.Parent;
                }
                throw new InvalidOperationException("Could not find the DoctorSimiBoss package folder");
            }
        }

        public static string ManifestPath => Path.Combine(PackageRoot, "ModIntegration", "animation_manifest.json");

        public static string ManifestJson => File.ReadAllText(ManifestPath);

        public static BossDefinition LoadRealDefinition()
        {
            return BossDefinition.Load(ManifestPath, ManifestPaths.ForSourcePackage(PackageRoot));
        }

        /// <summary>Copies the real Sprites, Effects and Arena into a temp folder that tests may damage.</summary>
        public static string CopyPackageToTemp()
        {
            string target = Path.Combine(Path.GetTempPath(), "simi-tests-" + Guid.NewGuid().ToString("N"));
            foreach (var sub in new[] { "Sprites", "Effects", "Arena" })
                CopyDirectory(Path.Combine(PackageRoot, sub), Path.Combine(target, sub));
            return target;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }

    /// <summary>Brain that returns queued actions, then a fallback; makes state tests deterministic.</summary>
    internal sealed class ScriptedBrain : IBossBrain
    {
        public readonly Queue<Func<BrainContext, BossAction>> Script = new Queue<Func<BrainContext, BossAction>>();
        public readonly List<BrainContext> Calls = new List<BrainContext>();
        public Func<BrainContext, BossAction> Fallback = ctx => BossAction.Idle(10f);

        public BossAction Decide(BrainContext context)
        {
            Calls.Add(context);
            return Script.Count > 0 ? Script.Dequeue()(context) : Fallback(context);
        }
    }

    internal sealed class FakeBody : IBossBody
    {
        public float PlayerOffsetX { get; set; }
        public readonly List<string> Log = new List<string>();
        public readonly List<AnimationRequest> Animations = new List<AnimationRequest>();
        public int Facing;
        public float Velocity;
        public bool HitboxActive;

        public void PlayAnimation(AnimationRequest request) { Animations.Add(request); Log.Add("anim:" + request.Clip); }
        public void SetFacing(int direction) { Facing = direction; }
        public void SetVelocityX(float velocity) { Velocity = velocity; }
        public void SetAttackActive(AttackDefinition attack, bool active) { HitboxActive = active; Log.Add((active ? "hitbox-on:" : "hitbox-off:") + attack.Id); }
        public void PlayEffect(string effectName) { Log.Add("effect:" + effectName); }
        public void OnDefeated() { Log.Add("defeated"); }
    }
}
