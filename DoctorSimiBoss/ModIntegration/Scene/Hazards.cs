using UnityEngine;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// Placeholder sprites drawn in code for waves, capsules and lane warnings, until the art guide's
    /// effect sprites exist. Capsules are fantasy pills with no markings (CombatDesign.md).
    /// </summary>
    internal static class ProceduralSprites
    {
        private static Sprite _wave, _capsule, _warning;

        public static Sprite Wave => _wave ?? (_wave = Build(64, 32, 64f, new Vector2(0.5f, 0f), (x, y, w, h) =>
        {
            // Soft half-ellipse rising from the floor, bright gold core.
            float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h;
            float d = nx * nx + ny * ny;
            if (d > 1f) return Color.clear;
            float core = 1f - d;
            return new Color(1f, 0.85f + 0.15f * core, 0.35f + 0.5f * core, Mathf.Clamp01(0.35f + core));
        }));

        public static Sprite Capsule => _capsule ?? (_capsule = Build(24, 56, 40f, new Vector2(0.5f, 0.5f), (x, y, w, h) =>
        {
            float r = w / 2f, cx = w / 2f;
            float cy = Mathf.Clamp(y + 0.5f, r, h - r);
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > r) return Color.clear;
            if (dist > r - 2f) return new Color(0.15f, 0.05f, 0.05f, 1f); // outline
            bool top = y >= h / 2;
            var baseColor = top ? new Color(0.85f, 0.18f, 0.2f) : new Color(0.95f, 0.93f, 0.88f);
            float shine = dx < -r * 0.3f ? 0.15f : 0f;
            return new Color(Mathf.Min(1, baseColor.r + shine), Mathf.Min(1, baseColor.g + shine), Mathf.Min(1, baseColor.b + shine), 1f);
        }));

        public static Sprite Warning => _warning ?? (_warning = Build(8, 64, 64f, new Vector2(0.5f, 0f), (x, y, w, h) =>
            new Color(1f, 0.25f, 0.2f, 0.35f * (1f - (float)y / h))));

        private delegate Color Painter(int x, int y, int w, int h);

        private static Sprite Build(int w, int h, float ppu, Vector2 pivot, Painter paint)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = paint(x, y, w, h);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
        }
    }

    /// <summary>Moving damaging object (wave, capsule). Destroys itself on leaving the arena or reaching the floor.</summary>
    internal sealed class Hazard : MonoBehaviour
    {
        private Vector2 _velocity;
        private float _life;
        private float _minX, _maxX, _floorY;
        private bool _stopAtFloor;
        private System.Action<Vector3> _onFloor;

        /// <summary>Downward acceleration (units/s²) for lobbed projectiles; 0 flies straight.</summary>
        public float Gravity;
        /// <summary>Rotation speed in degrees per second (tumbling capsules).</summary>
        public float Spin;

        /// <param name="frames">Animated sprites (looped at <paramref name="fps"/>); a single frame is a still sprite.</param>
        /// <param name="colliderSize">Collider in the object's local units (before scale and rotation).</param>
        public static Hazard Spawn(Transform parent, string name, Sprite[] frames, float fps, bool flipX, float rotation,
                                   Vector3 position, Vector2 scale, Vector2 colliderSize, Vector2 colliderOffset,
                                   Vector2 velocity, float life, int layer, int damage,
                                   float minX, float maxX, float floorY, bool stopAtFloor, System.Action<Vector3> onFloor = null)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.flipX = flipX;
            renderer.sortingOrder = 5;
            if (frames.Length > 1) go.AddComponent<FramePlayer>().Play(frames, fps, true);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.isKinematic = true;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = colliderSize;
            box.offset = colliderOffset;
            var damageHero = go.AddComponent<DamageHero>();
            damageHero.damageDealt = damage;
            damageHero.hazardType = 0; // NON_HAZARD

            var hazard = go.AddComponent<Hazard>();
            hazard._velocity = velocity;
            hazard._life = life;
            hazard._minX = minX;
            hazard._maxX = maxX;
            hazard._floorY = floorY;
            hazard._stopAtFloor = stopAtFloor;
            hazard._onFloor = onFloor;
            return hazard;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            _velocity.y -= Gravity * dt;
            if (Spin != 0f) transform.Rotate(0f, 0f, Spin * dt);
            var p = transform.position + (Vector3)(_velocity * dt);
            transform.position = p;
            bool landed = _stopAtFloor && p.y <= _floorY;
            if (landed && _onFloor != null) _onFloor(new Vector3(p.x, _floorY, p.z));
            if (_life <= 0 || p.x < _minX || p.x > _maxX || landed) Destroy(gameObject);
        }
    }

    /// <summary>Non-damaging marker that blinks for a while (telegraph of a danger zone).</summary>
    internal sealed class Telegraph : MonoBehaviour
    {
        private float _life, _age;
        private SpriteRenderer _renderer;

        public static void Show(Transform parent, Vector3 position, Vector2 scale, float life)
        {
            var go = new GameObject("Warning");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var t = go.AddComponent<Telegraph>();
            t._renderer = go.AddComponent<SpriteRenderer>();
            t._renderer.sprite = ProceduralSprites.Warning;
            t._renderer.sortingOrder = -5;
            t._life = life;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life) { Destroy(gameObject); return; }
            _renderer.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(_age * 18f));
        }
    }

    /// <summary>Fading copy of the boss sprite left behind during dashes, lunges and jumps.</summary>
    internal sealed class Afterimage : MonoBehaviour
    {
        private const float Life = 0.25f;
        private SpriteRenderer _renderer;
        private float _age;

        public static void Spawn(Transform parent, SpriteRenderer source, Transform visual)
        {
            if (source.sprite == null) return;
            var go = new GameObject("Afterimage");
            go.transform.SetParent(parent, true);
            go.transform.position = visual.position + new Vector3(0f, 0f, 0.02f);
            go.transform.rotation = visual.rotation;
            go.transform.localScale = visual.lossyScale;
            var ghost = go.AddComponent<Afterimage>();
            ghost._renderer = go.AddComponent<SpriteRenderer>();
            ghost._renderer.sprite = source.sprite;
            ghost._renderer.flipX = source.flipX;
            ghost._renderer.sortingOrder = -1;
            ghost._renderer.color = new Color(1f, 0.85f, 0.45f, 0.45f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Life) { Destroy(gameObject); return; }
            var c = _renderer.color;
            c.a = 0.45f * (1f - _age / Life);
            _renderer.color = c;
        }
    }
}
