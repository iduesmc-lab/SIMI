using System;
using System.Collections.Generic;
using System.IO;
using DoctorSimiBoss.Core;
using DoctorSimiBoss.Core.Manifest;
using UnityEngine;

namespace DoctorSimiBoss.Scene
{
    /// <summary>Unity sprites for every validated clip, effect and the arena background.</summary>
    internal sealed class SpriteLibrary
    {
        private readonly Dictionary<string, Sprite[]> _clips = new Dictionary<string, Sprite[]>();

        public Sprite Background { get; private set; }

        public Sprite[] Get(string clip)
        {
            return clip != null && _clips.TryGetValue(clip, out var frames) ? frames : null;
        }

        public static SpriteLibrary Load(BossDefinition definition, string modDirectory)
        {
            var manifest = definition.Manifest;
            var resolve = ManifestPaths.ForModDirectory(modDirectory);
            var library = new SpriteLibrary();

            // Manifest origin is measured from the top-left of the frame; Unity pivots start at the bottom-left.
            var bossPivot = new Vector2(manifest.OriginX, 1f - manifest.OriginY);
            float bossPpu = manifest.SpritePixelsPerUnit > 0 ? manifest.SpritePixelsPerUnit : 32f;

            foreach (var check in definition.Report.Clips)
            {
                if (!check.IsPlayable) continue;
                var clip = check.Kind == ClipKind.Animation ? manifest.Animations[check.Name] : manifest.Effects[check.Name];
                var pivot = check.Kind == ClipKind.Animation ? bossPivot : new Vector2(0.5f, 0.5f);
                var frames = new Sprite[clip.Frames.Count];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = LoadSprite(resolve(clip.Frames[i]), pivot, bossPpu, FilterMode.Point);
                library._clips[check.Name] = frames;
            }

            if (manifest.Arena != null)
                library.Background = LoadSprite(resolve(manifest.Arena.Background), Vector2.zero, manifest.Arena.PixelsPerUnit, FilterMode.Bilinear);

            return library;
        }

        private static Sprite LoadSprite(string path, Vector2 pivot, float pixelsPerUnit, FilterMode filter)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
                throw new InvalidOperationException("Unity could not decode " + path);
            texture.filterMode = filter;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit);
        }
    }

    /// <summary>Plays a list of sprites on the SpriteRenderer of the same GameObject.</summary>
    internal sealed class FramePlayer : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _fps;
        private bool _loop;
        private float _time;

        /// <summary>Destroy the GameObject after the last frame of a non-looping clip (one-shot effects).</summary>
        public bool DestroyWhenDone;

        /// <summary>Playback speed multiplier (the boss uses its phase speed).</summary>
        public float Speed = 1f;

        public void Play(Sprite[] frames, float fps, bool loop)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _frames = frames;
            _fps = fps > 0 ? fps : 10f;
            _loop = loop;
            _time = 0;
            if (_frames != null && _frames.Length > 0) _renderer.sprite = _frames[0];
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0) return;
            _time += Time.deltaTime * Speed;
            int index = (int)(_time * _fps);
            if (index >= _frames.Length)
            {
                if (_loop) index %= _frames.Length;
                else
                {
                    index = _frames.Length - 1;
                    if (DestroyWhenDone) { Destroy(gameObject); return; }
                }
            }
            _renderer.sprite = _frames[index];
        }
    }
}
