using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DoctorSimiBoss.Core.Manifest;
using UnityEngine;
using UnityEngine.UI;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// Boss music taken from the game itself. Hollow Knight's per-scene "SceneManager" component (global
    /// namespace, Assembly-CSharp 1.5.78.11833-77) keeps the room's MusicCue in the private field "musicCue";
    /// the mod preloads the objects listed in presentation.musicPreloads and reads that field. Cues are
    /// applied with AudioManager.ApplyMusicCue(cue, delay, transition, applySnapshot) and the room's cue
    /// (AudioManager.CurrentMusicCue) is restored afterwards.
    /// </summary>
    internal static class BossMusic
    {
        private static readonly List<MusicCue> Cues = new List<MusicCue>();
        private static MusicCue _saved;
        private static bool _playing;

        /// <summary>Reads the music cues from the preloaded scene managers (called from Mod.Initialize).</summary>
        public static void Collect(Dictionary<string, Dictionary<string, GameObject>> preloads, Action<string> log)
        {
            if (preloads == null) return;
            var field = typeof(global::SceneManager).GetField("musicCue", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var scene in preloads)
            {
                foreach (var entry in scene.Value)
                {
                    var manager = entry.Value != null ? entry.Value.GetComponent<global::SceneManager>() : null;
                    var cue = manager != null && field != null ? field.GetValue(manager) as MusicCue : null;
                    if (cue == null)
                    {
                        log("Music: no cue on " + scene.Key + "/" + entry.Key + ".");
                        continue;
                    }
                    if (!Cues.Contains(cue)) Cues.Add(cue);
                    log("Music: cue '" + cue.name + "' from " + scene.Key + ".");
                }
            }
        }

        public static void Play(PresentationSettings settings, Action<string> log)
        {
            if (settings == null || !settings.Music || _playing) return;
            var audio = GameManager.instance != null ? GameManager.instance.AudioManager : null;
            var cue = Pick(settings.MusicCue);
            if (audio == null || cue == null)
            {
                if (cue == null) log("Music: no boss cue available; the room's music keeps playing.");
                return;
            }
            try
            {
                _saved = audio.CurrentMusicCue;
                audio.ApplyMusicCue(cue, 0f, 0.5f, true);
                _playing = true;
                log("Music: playing '" + cue.name + "'.");
            }
            catch (Exception e) { log("Music failed: " + e.Message); }
        }

        /// <summary>Puts the room's music back (leaving the arena, or after the boss falls).</summary>
        public static void Restore(Action<string> log, float transition = 1.5f)
        {
            if (!_playing) return;
            _playing = false;
            var audio = GameManager.instance != null ? GameManager.instance.AudioManager : null;
            if (audio == null || _saved == null) return;
            try { audio.ApplyMusicCue(_saved, 0f, transition, true); }
            catch (Exception e) { log("Music restore failed: " + e.Message); }
        }

        /// <summary>The scene changed: the new room applies its own music, so nothing is restored.</summary>
        public static void Forget() { _playing = false; _saved = null; }

        private static MusicCue Pick(string wanted)
        {
            Cues.RemoveAll(c => c == null);
            if (Cues.Count == 0) return null;
            if (!string.IsNullOrEmpty(wanted))
            {
                var match = Cues.Find(c => c.name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null) return match;
            }
            return Cues[0];
        }
    }

    /// <summary>
    /// Boss title in the style of Hollow Knight's area/boss names: large letters in the lower part of the
    /// screen that fade in and out. Uses a font already loaded by the game when one with "trajan" in its
    /// name exists (the names are logged), otherwise Unity's built-in Arial.
    /// </summary>
    internal sealed class BossTitle : MonoBehaviour
    {
        private const float FadeIn = 0.8f, Hold = 2.6f, FadeOut = 1.2f;
        private static Font _font;

        public static void Show(Transform parent, string title, float delay, Action<string> log)
        {
            if (string.IsNullOrEmpty(title)) return;
            var go = new GameObject("Doctor Simi Title");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.12f);
            rect.anchorMax = new Vector2(1f, 0.32f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = FindFont(log);
            text.text = title;
            text.fontSize = 96;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = new Color(0.97f, 0.96f, 0.93f, 1f);
            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(3f, -3f);

            go.AddComponent<BossTitle>().StartCoroutine(Run(go, group, delay));
        }

        private static IEnumerator Run(GameObject go, CanvasGroup group, float delay)
        {
            yield return new WaitForSeconds(delay);
            for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime) { group.alpha = t / FadeIn; yield return null; }
            group.alpha = 1f;
            yield return new WaitForSeconds(Hold);
            for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / FadeOut; yield return null; }
            Destroy(go);
        }

        private static Font FindFont(Action<string> log)
        {
            if (_font != null) return _font;
            var names = new List<string>();
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (f == null) continue;
                names.Add(f.name);
                if (_font == null && f.name.IndexOf("trajan", StringComparison.OrdinalIgnoreCase) >= 0) _font = f;
            }
            log("Fonts loaded by the game: " + string.Join(", ", names.ToArray()));
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            log("Title font: " + (_font != null ? _font.name : "none"));
            return _font;
        }
    }

    /// <summary>
    /// Warning chime played when a special attack starts its wind-up. Synthesised (a short bright rising
    /// bell) so no game clip has to be guessed; routed through the same mixer group as the game's enemy
    /// sounds when a hit-effects donor is known, so it follows the sound-effects volume.
    /// </summary>
    internal static class TelegraphSound
    {
        private const int Rate = 44100;
        private static AudioClip _clip;

        public static AudioSource Attach(GameObject target)
        {
            var source = target.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = HitEffectsDonor.MixerGroup;
            return source;
        }

        public static void Play(AudioSource source)
        {
            if (source == null) return;
            if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = HitEffectsDonor.MixerGroup;
            source.PlayOneShot(Clip, 0.55f);
        }

        private static AudioClip Clip
        {
            get
            {
                if (_clip != null) return _clip;
                const float length = 0.45f;
                int n = (int)(Rate * length);
                var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 7f);
                    float f = Mathf.Lerp(660f, 990f, Mathf.Clamp01(t / 0.12f));
                    float phase = 2f * Mathf.PI * f * t;
                    data[i] = env * (0.6f * Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2.01f) + 0.12f * Mathf.Sin(phase * 3.02f));
                }
                _clip = AudioClip.Create("DoctorSimi Telegraph", n, 1, Rate, false);
                _clip.SetData(data, 0);
                return _clip;
            }
        }
    }

    /// <summary>Night rain over the rooftop: thin streaks behind the boss and a fainter layer in front.</summary>
    internal sealed class RainFx : MonoBehaviour
    {
        private struct Drop { public Transform T; public float Speed; }

        private readonly List<Drop> _drops = new List<Drop>();
        private float _width, _height;
        private static Sprite _streak;

        public static void Create(Transform arenaRoot, float width, float height)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(arenaRoot, false);
            var rain = go.AddComponent<RainFx>();
            rain._width = width;
            rain._height = height;
            var rng = new System.Random(7);
            rain.AddLayer(rng, 140, 0.15f, -5, new Color(0.75f, 0.82f, 0.95f, 0.35f), 0.8f);
            rain.AddLayer(rng, 50, -0.1f, 30, new Color(0.85f, 0.9f, 1f, 0.22f), 1.25f);
        }

        private void AddLayer(System.Random rng, int count, float z, int order, Color color, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Drop");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((float)rng.NextDouble() * _width, (float)rng.NextDouble() * _height, z);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = Streak;
                r.color = color;
                r.sortingOrder = order;
                _drops.Add(new Drop { T = go.transform, Speed = (26f + 10f * (float)rng.NextDouble()) * scale });
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var d in _drops)
            {
                var p = d.T.localPosition;
                p.y -= d.Speed * dt;
                p.x -= d.Speed * 0.21f * dt; // matches the 12 degree slant
                if (p.y < 0f) { p.y += _height; p.x += _height * 0.21f; }
                if (p.x < 0f) p.x += _width;
                d.T.localPosition = p;
            }
        }

        private static Sprite Streak
        {
            get
            {
                if (_streak != null) return _streak;
                const int w = 2, h = 40;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        pixels[y * w + x] = new Color(1f, 1f, 1f, 1f - (float)y / h); // bright head at the bottom, fading tail
                tex.SetPixels(pixels);
                tex.Apply();
                _streak = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 40f);
                return _streak;
            }
        }
    }
}
