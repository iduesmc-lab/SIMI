using System.IO;
using System.Reflection;
using Modding;

namespace DoctorSimiBoss
{
    /// <summary>
    /// Loader entry point for the Doctor Simi visual prototype.
    /// This only verifies that the packaged manifest is present; boss spawning,
    /// runtime texture creation, AI, and damage logic are not implemented yet.
    /// </summary>
    public sealed class DoctorSimiBossMod : Mod
    {
        public override string GetVersion() => "0.1.0-prototype";

        public override void Initialize()
        {
            string modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string manifestPath = Path.Combine(modDirectory, "Assets", "animation_manifest.json");

            if (!File.Exists(manifestPath))
            {
                Log("Animation manifest not found: " + manifestPath);
                return;
            }

            string manifest = File.ReadAllText(manifestPath);
            Log("Prototype animation manifest found (" + manifest.Length + " characters). " +
                "No boss scene or runtime animation hook is connected yet.");
        }
    }
}
