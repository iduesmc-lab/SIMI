using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// Screen shake and hit-stop through the game's own systems (checked in Assembly-CSharp
    /// 1.5.78.11833-77): GameCameras.cameraShakeFSM (PlayMakerFSM.SendEvent) and
    /// GameManager.FreezeMoment(rampDown, wait, rampUp, targetSpeed). The shake FSM's event names are
    /// game data, so they are matched at runtime by keyword and the available names are logged once.
    /// </summary>
    internal static class CameraFx
    {
        private static bool _loggedEvents;
        private static readonly Dictionary<string, string> Resolved = new Dictionary<string, string>();

        public enum Strength { Small, Average, Big }

        public static void Shake(Strength strength, Action<string> log)
        {
            try
            {
                var fsm = GameCameras.instance != null ? GameCameras.instance.cameraShakeFSM : null;
                if (fsm == null) return;
                string key = strength.ToString();
                if (!Resolved.TryGetValue(key, out var eventName))
                {
                    var names = new List<string>();
                    foreach (var e in fsm.FsmEvents) if (e != null && !string.IsNullOrEmpty(e.Name)) names.Add(e.Name);
                    if (!_loggedEvents)
                    {
                        _loggedEvents = true;
                        log("Camera shake events: " + string.Join(", ", names.ToArray()));
                    }
                    eventName = names.Find(n => n.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                n.IndexOf("shake", StringComparison.OrdinalIgnoreCase) >= 0);
                    Resolved[key] = eventName;
                }
                if (eventName != null) fsm.SendEvent(eventName);
            }
            catch (Exception e)
            {
                log("Camera shake failed: " + e.Message);
            }
        }

        /// <summary>Brief hit-stop: slows time to a stop for <paramref name="hold"/> seconds.</summary>
        public static void Freeze(float hold, Action<string> log)
        {
            try
            {
                var gm = GameManager.instance;
                if (gm != null) gm.StartCoroutine(gm.FreezeMoment(0.01f, hold, 0.06f, 0f));
            }
            catch (Exception e)
            {
                log("Freeze failed: " + e.Message);
            }
        }
    }
}
