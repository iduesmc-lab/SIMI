using System;
using System.IO;
using System.Reflection;
using DoctorSimiBoss.Core;
using DoctorSimiBoss.Core.Manifest;
using DoctorSimiBoss.Scene;
using Modding;
using UnityEngine;

namespace DoctorSimiBoss
{
    /// <summary>
    /// Loader entry point. It loads and validates the animation manifest, builds the boss definition
    /// (tuning, attacks, phases), loads the sprites and installs the debug-key encounter (Scene/).
    ///
    /// Modding API members used here, checked against ModdingAPI 1.5.78.11833-77 (source and the
    /// published Assembly-CSharp.dll): Mod(string name), virtual Initialize(), virtual GetVersion(),
    /// Loggable.Log(string).
    /// The loader scans Managed/Mods/&lt;subfolder&gt;/*.dll, so this DLL must live in its own
    /// subfolder with Assets/ beside it.
    /// </summary>
    public sealed class DoctorSimiBossMod : Mod
    {
        public const string ManifestRelativePath = "Assets/animation_manifest.json";

        /// <summary>Validated definition, or null if loading failed. Scene integration will use it.</summary>
        public static BossDefinition Definition { get; private set; }

        public DoctorSimiBossMod() : base("Doctor Simi Boss") { }

        public override string GetVersion() => "0.4.0-prototype";

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
            if (arena == null)
            {
                Log("Manifest has no arena; the encounter is not available.");
                return;
            }
            Log("Arena: " + arena.Width.ToString("0.##") + " x " + arena.Height.ToString("0.##") +
                " world units, floor at y=" + arena.FloorY.ToString("0.##") +
                ", walls at x=" + arena.LeftWallX.ToString("0.##") + " and x=" + arena.RightWallX.ToString("0.##") + ".");

            string keyName = definition.Manifest.Encounter?.DebugKey ?? "F8";
            KeyCode key;
            try { key = (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true); }
            catch (ArgumentException)
            {
                Log("Unknown encounter.debugKey '" + keyName + "'; using F8.");
                key = KeyCode.F8;
            }

            SpriteLibrary sprites;
            try { sprites = SpriteLibrary.Load(definition, modDirectory); }
            catch (Exception e)
            {
                Log("Could not load sprites: " + e.Message);
                return;
            }

            EncounterController.Install(definition, sprites, key, Log);
            Log("Encounter ready: press " + key + " in a gameplay room to enter the arena, and again to leave.");
        }
    }
}
