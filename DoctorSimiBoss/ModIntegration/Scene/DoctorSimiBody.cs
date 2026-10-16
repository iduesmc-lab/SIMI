using System;
using DoctorSimiBoss.Core;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Arena;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Hosting;
using DoctorSimiBoss.Core.Manifest;
using UnityEngine;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// The boss GameObject. It implements <see cref="IBossBody"/> for the engine-independent
    /// <see cref="BossDriver"/>, and <see cref="IHitResponder"/> so the Knight's nail and spells
    /// reach it: HitTaker.Hit looks for an IHitResponder on the struck object and its parents.
    /// Contact and punch damage use DamageHero, which HeroBox.CheckForDamage reads.
    /// </summary>
    internal sealed class DoctorSimiBody : MonoBehaviour, IBossBody, IHitResponder
    {
        private const float HitDedupSeconds = 0.15f;
        private const float FlashSeconds = 0.12f;

        private BossDefinition _definition;
        private SpriteLibrary _sprites;
        private ArenaLayout _arena;
        private Vector2 _arenaOrigin;
        private Action<string> _log;

        private BossDriver _driver;
        private SpriteRenderer _renderer;
        private FramePlayer _player;
        private GameObject _contact;
        private GameObject _punch;
        private float _velocityX;
        private int _facing = -1;
        private float _halfWidth;
        private float _flashTimer;
        private GameObject _lastHitSource;
        private float _lastHitTime = -1f;

        public BossStateMachine Machine => _driver?.Machine;
        public event Action Defeated;

        public static DoctorSimiBody Create(BossDefinition definition, SpriteLibrary sprites, ArenaLayout arena,
                                            Vector2 arenaOrigin, Transform parent, LayerChoice layers, Action<string> log)
        {
            var go = new GameObject("Doctor Simi");
            go.transform.SetParent(parent, false);
            go.layer = layers.EnemyBody;
            var body = go.AddComponent<DoctorSimiBody>();
            body.Init(definition, sprites, arena, arenaOrigin, layers, log);
            return body;
        }

        private void Init(BossDefinition definition, SpriteLibrary sprites, ArenaLayout arena, Vector2 arenaOrigin,
                          LayerChoice layers, Action<string> log)
        {
            _definition = definition;
            _sprites = sprites;
            _arena = arena;
            _arenaOrigin = arenaOrigin;
            _log = log;

            var manifest = definition.Manifest;
            transform.position = new Vector3(arenaOrigin.x + arena.BossLandingX, arenaOrigin.y + arena.FloorY, 0.01f);

            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _player = gameObject.AddComponent<FramePlayer>();

            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.isKinematic = true;

            // Hurtbox: what the nail hits.
            var bodyBox = FrameGeometry.ToLocal(manifest, manifest.BodyBox, -1);
            var hurt = gameObject.AddComponent<BoxCollider2D>();
            hurt.isTrigger = true;
            hurt.offset = new Vector2(bodyBox.CenterX, bodyBox.CenterY);
            hurt.size = new Vector2(bodyBox.Width, bodyBox.Height);
            _halfWidth = bodyBox.Width / 2f;

            // Contact damage lives on its own child: HeroBox only checks that a DamageHero component exists,
            // so it is switched off by deactivating the GameObject (entrance, phase change, defeat).
            _contact = CreateDamageArea("Contact Damage", layers.EnemyDamage, bodyBox, definition.Tuning.ContactDamage);

            var punchBox = FrameGeometry.ToLocal(manifest, manifest.PunchBox, -1);
            _punch = CreateDamageArea("Punch", layers.EnemyDamage, punchBox, definition.Tuning.AttackDamage);
            _punch.SetActive(false);

            var machine = definition.CreateStateMachine(new DefaultBossBrain(new System.Random()));
            machine.PhaseChanged += (from, to) => _log("Phase " + from + " -> " + to + " (" + machine.Health + "/" + machine.MaxHealth + ")");
            _driver = new BossDriver(machine, definition.CreateAnimationResolver(PlaceholderPolicy.PreferAvailable), this);
        }

        private GameObject CreateDamageArea(string name, int layer, LocalBox box, int damage)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(transform, false);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.offset = new Vector2(box.CenterX, box.CenterY);
            collider.size = new Vector2(box.Width, box.Height);
            var damageHero = go.AddComponent<DamageHero>();
            damageHero.damageDealt = damage;
            damageHero.hazardType = 0; // GlobalEnums.HazardType.NON_HAZARD
            return go;
        }

        private void Update()
        {
            if (_driver == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;

            _driver.Tick(dt);

            var position = transform.position;
            float worldX = position.x + _velocityX * dt - _arenaOrigin.x;
            position.x = _arenaOrigin.x + _arena.ClampX(worldX, _halfWidth);
            transform.position = position;

            var machine = _driver.Machine;
            bool contactOn = !machine.IsInvulnerable && machine.State != BossState.Defeated;
            if (_contact.activeSelf != contactOn) _contact.SetActive(contactOn);

            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                _renderer.color = _flashTimer > 0 ? new Color(1f, 0.6f, 0.6f) : Color.white;
            }
        }

        // ---- IHitResponder ----

        public void Hit(HitInstance damageInstance)
        {
            if (_driver == null) return;
            var source = damageInstance.Source;
            if (source != null && source == _lastHitSource && Time.time - _lastHitTime < HitDedupSeconds) return;
            _lastHitSource = source;
            _lastHitTime = Time.time;

            float multiplier = damageInstance.Multiplier > 0 ? damageInstance.Multiplier : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(damageInstance.DamageDealt * multiplier));
            var result = _driver.ReceiveHit(damage);
            if (result == HitResult.Ignored) return;

            _flashTimer = FlashSeconds;
            if (damageInstance.AttackType == AttackTypes.Nail && HeroController.instance != null)
                HeroController.instance.SoulGain();
            if (result == HitResult.Stunned) _log("Stunned (" + _driver.Machine.Health + "/" + _driver.Machine.MaxHealth + ")");
        }

        // ---- IBossBody ----

        public float PlayerOffsetX
        {
            get
            {
                var hero = HeroController.instance;
                return hero != null ? hero.transform.position.x - transform.position.x : 0f;
            }
        }

        public void PlayAnimation(AnimationRequest request)
        {
            var frames = _sprites.Get(request.Clip);
            if (frames != null) _player.Play(frames, request.Fps, request.Loop);
        }

        public void SetFacing(int direction)
        {
            _facing = direction;
            _renderer.flipX = FrameGeometry.NeedsFlip(_definition.Manifest, direction);
            PlaceBox(_contact, _definition.Manifest.BodyBox);
            PlaceBox(_punch, _definition.Manifest.PunchBox);
        }

        public void SetVelocityX(float velocity) { _velocityX = velocity; }

        public void SetAttackActive(AttackDefinition attack, bool active)
        {
            PlaceBox(_punch, _definition.Manifest.PunchBox);
            _punch.SetActive(active);
        }

        public void PlayEffect(string effectName)
        {
            var frames = _sprites.Get(effectName);
            var clip = _definition.Manifest.Effects.TryGetValue(effectName, out var info) ? info : null;
            if (frames == null || clip == null) return;

            var box = FrameGeometry.ToLocal(_definition.Manifest, _definition.Manifest.PunchBox, _facing);
            var go = new GameObject(effectName);
            go.transform.SetParent(transform.parent, false);
            go.transform.position = transform.position + new Vector3(box.CenterX, box.CenterY, -0.02f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.flipX = _renderer.flipX;
            var player = go.AddComponent<FramePlayer>();
            player.DestroyWhenDone = true;
            player.Play(frames, clip.Fps, false);
        }

        public void OnDefeated()
        {
            _punch.SetActive(false);
            _contact.SetActive(false);
            _log("Doctor Simi defeated.");
            Defeated?.Invoke();
        }

        private void PlaceBox(GameObject area, PixelRect rect)
        {
            if (area == null || rect == null) return;
            var box = FrameGeometry.ToLocal(_definition.Manifest, rect, _facing);
            area.GetComponent<BoxCollider2D>().offset = new Vector2(box.CenterX, box.CenterY);
        }
    }
}
