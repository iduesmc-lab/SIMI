using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// Borrows Hollow Knight's own enemy hit feedback. EnemyHitEffectsUninfected.RecieveHitEffect plays
    /// the enemy damage sound (enemyDamage via audioPlayerPrefab), spawns the hit particles
    /// (uninfectedHitPt) and flings the slash effects (slashEffectGhost1/2); checked in Assembly-CSharp
    /// 1.5.78.11833-77. Those are asset references on every normal enemy, so the first one found in a
    /// loaded scene is copied (by reflection, field by field) onto a hidden persistent holder and later
    /// onto the boss. Without a donor the boss still gets the white flash, just no sound.
    /// </summary>
    internal static class HitEffectsDonor
    {
        private static readonly string[] SkipFields = { "spriteFlash", "didFireThisFrame", "effectOrigin", "sendDamageFlashEvent" };
        private static EnemyHitEffectsUninfected _holder;

        public static bool Ready => _holder != null;

        /// <summary>Mixer group of the donor's audio player (the game's sound-effects channel), or null.</summary>
        public static UnityEngine.Audio.AudioMixerGroup MixerGroup =>
            _holder != null && _holder.audioPlayerPrefab != null ? _holder.audioPlayerPrefab.outputAudioMixerGroup : null;

        /// <summary>Looks for a usable enemy among loaded objects; cheap once a donor is stored.</summary>
        public static void TryCapture(Action<string> log)
        {
            if (_holder != null) return;
            EnemyHitEffectsUninfected donor = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<EnemyHitEffectsUninfected>())
            {
                if (candidate != null && HasSound(candidate)) { donor = candidate; break; }
            }
            if (donor == null) return;

            var go = new GameObject("DoctorSimiBoss HitEffects");
            go.SetActive(false); // keeps the copied component from running; it only holds the references
            UnityEngine.Object.DontDestroyOnLoad(go);
            _holder = go.AddComponent<EnemyHitEffectsUninfected>();
            Copy(donor, _holder);
            log("Hit effects borrowed from '" + donor.gameObject.name + "' (Hollow Knight enemy hit sound and particles).");
        }

        /// <summary>Adds the game's hit effects to the boss, or returns null if no donor was found yet.</summary>
        public static EnemyHitEffectsUninfected AttachTo(GameObject target, Vector3 effectOrigin)
        {
            if (_holder == null) return null;
            var effects = target.AddComponent<EnemyHitEffectsUninfected>();
            Copy(_holder, effects);
            effects.effectOrigin = effectOrigin;
            effects.sendDamageFlashEvent = false;
            return effects;
        }

        private static void Copy(EnemyHitEffectsUninfected from, EnemyHitEffectsUninfected to)
        {
            foreach (var field in typeof(EnemyHitEffectsUninfected).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (Array.IndexOf(SkipFields, field.Name) >= 0) continue;
                field.SetValue(to, field.GetValue(from));
            }
        }

        /// <summary>True when the donor has a damage sound (AudioEvent.Clip) and an audio player prefab.</summary>
        private static bool HasSound(EnemyHitEffectsUninfected e)
        {
            var type = typeof(EnemyHitEffectsUninfected);
            var player = type.GetField("audioPlayerPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(e);
            var audioEvent = type.GetField("enemyDamage")?.GetValue(e);
            if (player == null || audioEvent == null || (player is UnityEngine.Object o && o == null)) return false;
            // AudioEvent.Clip may be a field or a property depending on the build; accept both.
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var t = audioEvent.GetType();
            var clip = t.GetField("Clip", any)?.GetValue(audioEvent) ?? t.GetProperty("Clip", any)?.GetValue(audioEvent, null);
            return clip != null && !(clip is UnityEngine.Object c && c == null);
        }
    }

    /// <summary>
    /// White hit flash: a white silhouette of the current frame drawn over the boss and faded out,
    /// like Hollow Knight enemies. White copies are made lazily per frame and cached.
    /// </summary>
    internal sealed class WhiteFlash : MonoBehaviour
    {
        private const float Duration = 0.14f;
        private static readonly Dictionary<Sprite, Sprite> WhiteCache = new Dictionary<Sprite, Sprite>();

        private SpriteRenderer _source;
        private SpriteRenderer _overlay;
        private float _timer;
        private Color _glow;
        private float _glowAlpha;

        public static WhiteFlash Create(SpriteRenderer source)
        {
            var go = new GameObject("Hit Flash");
            go.transform.SetParent(source.transform, false);
            var flash = go.AddComponent<WhiteFlash>();
            flash._source = source;
            flash._overlay = go.AddComponent<SpriteRenderer>();
            flash._overlay.sortingOrder = source.sortingOrder + 1;
            flash._overlay.enabled = false;
            return flash;
        }

        public void Flash() { _timer = Duration; }

        /// <summary>Continuous coloured glow (attack telegraph); alpha 0 turns it off. The hit flash wins while active.</summary>
        public void SetGlow(Color color, float alpha) { _glow = color; _glowAlpha = alpha; }

        private void LateUpdate()
        {
            var sprite = _source.sprite;
            bool flashing = _timer > 0f;
            if ((!flashing && _glowAlpha <= 0f) || sprite == null) { if (_overlay.enabled) _overlay.enabled = false; return; }
            _overlay.enabled = true;
            _overlay.sprite = WhiteOf(sprite);
            _overlay.flipX = _source.flipX;
            if (flashing)
            {
                _timer -= Time.deltaTime;
                float t = Mathf.Clamp01(_timer / Duration);
                _overlay.color = new Color(1f, 1f, 1f, 0.9f * t);
            }
            else
            {
                _overlay.color = new Color(_glow.r, _glow.g, _glow.b, _glowAlpha);
            }
        }

        private static Sprite WhiteOf(Sprite sprite)
        {
            if (WhiteCache.TryGetValue(sprite, out var white)) return white;
            var src = sprite.texture;
            var pixels = src.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = src.filterMode, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply();
            var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            white = Sprite.Create(tex, sprite.rect, pivot, sprite.pixelsPerUnit);
            WhiteCache[sprite] = white;
            return white;
        }
    }
}
