using System;
using System.IO;
using System.Reflection;
using DoctorSimiBoss.Core;
using DoctorSimiBoss.Core.Manifest;
using Modding;

namespace DoctorSimiBoss
{
    /// <summary>
    /// Loader entry point. It loads and validates the animation manifest and builds the boss
    /// definition (tuning, attacks, phases), then logs the result.
    ///
    /// It does NOT insert the boss into any scene yet: the target game version, the Managed
    /// folder and the test location still have to be confirmed. Only the Modding API members
    /// already used by the original skeleton appear here (Mod, Initialize, Log, GetVersion);
    /// they must still be checked by compiling against the player's API-patched Assembly-CSharp.dll.
    /// </summary>
    public sealed class DoctorSimiBossMod : Mod
    {
        public const string ManifestRelativePath = "Assets/animation_manifest.json";

        /// <summary>Validated definition, or null if loading failed. Scene integration will use it.</summary>
        public static BossDefinition Definition { get; private set; }

        public override string GetVersion() => "0.2.0-prototype";

        public override void Initialize()
        {
            string modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string manifestPath = Path.Combine(modDirectory, ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));

            BossDefinition definition;
            try
            {
                definition = BossDefinition.Load(manifestPath, ManifestPaths.ForModDirectory(modDirectory));
            }
            catch (Exception e)
            {
                Log("Could not load " + manifestPath + ": " + e.Message);
                return;
            }

            foreach (string line in definition.Report.ToLines()) Log(line);

            if (!definition.CanRun)
            {
                Log("Boss definition has errors; the boss will not be available.");
                return;
            }

            Definition = definition;
            Log("Boss definition ready: " + definition.Manifest.Phases.Count + " phases, max health " +
                definition.Tuning.MaxHealth + ". Scene insertion is not implemented yet.");

            var arena = definition.CreateArenaLayout();
            if (arena != null)
                Log("Arena: " + arena.Width.ToString("0.##") + " x " + arena.Height.ToString("0.##") +
                    " world units, floor at y=" + arena.FloorY.ToString("0.##") +
                    ", walls at x=" + arena.LeftWallX.ToString("0.##") + " and x=" + arena.RightWallX.ToString("0.##") + ".");
        }
    }
}
