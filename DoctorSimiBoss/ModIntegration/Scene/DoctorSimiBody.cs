using System;
using System.Collections.Generic;
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
    /// Contact, punch and hazard damage use DamageHero, which HeroBox.CheckForDamage reads.
    ///
    /// Core decides WHAT happens and WHEN (state, attack stage, timings); this class performs the
    /// choreography of each attack: jump arcs, waves, capsule lanes, pulses and telegraphs.
    /// </summary>
    internal sealed class DoctorSimiBody : MonoBehaviour, IBossBody, IHitResponder
    {
        private const float HitDedupSeconds = 0.15f;
        private const float FlashSeconds = 0.1f;
        private const int CapsuleLanes = 7;

        private BossDefinition _definition;
        private SpriteLibrary _sprites;
        private ArenaLayout _arena;
        private Vector2 _origin;
        private LayerChoice _layers;
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

        // Choreography state (times are in boss time, i.e. scaled by the phase speed).
        private float _clock;
        private float _stageStart;
        private AttackDefinition _stageAttack;
        private AttackStage _stage;
        private Arc _arc;
        private readonly List<Scheduled> _schedule = new List<Scheduled>();

        private struct Arc
        {
            public bool Active, Slam;
            public float FromX, ToX, Height, Duration, Time, Spins;
        }

        private struct Scheduled
        {
            public float At;
            public Action Run;
        }

        public BossStateMachine Machine => _driver?.Machine;
        public event Action Defeated;

        private float Floor => _origin.y + _arena.FloorY;
        private float LeftWall => _origin.x + _arena.LeftWallX;
        private float RightWall => _origin.x + _arena.RightWallX;
        private float Top => _origin.y + _arena.Height;

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
            _origin = arenaOrigin;
            _layers = layers;
            _log = log;

            var manifest = definition.Manifest;
            transform.position = new Vector3(_origin.x + arena.BossLandingX, Floor, 0.01f);

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

            // HeroBox only checks that a DamageHero component exists, so damage areas are switched
            // off by deactivating their GameObject (entrance, phase change, defeat).
            _contact = CreateDamageArea("Contact Damage", bodyBox, definition.Tuning.ContactDamage);
            _punch = CreateDamageArea("Punch", FrameGeometry.ToLocal(manifest, manifest.PunchBox, -1), definition.Tuning.AttackDamage);
            _punch.SetActive(false);

            var machine = definition.CreateStateMachine(new DefaultBossBrain(new System.Random()));
            machine.PhaseChanged += (from, to) => _log("Phase " + from + " -> " + to + " (" + machine.Health + "/" + machine.MaxHealth + ")");
            machine.AttackStageChanged += OnAttackStage;
            _driver = new BossDriver(machine, definition.CreateAnimationResolver(PlaceholderPolicy.PreferAvailable), this);
        }

        private GameObject CreateDamageArea(string name, LocalBox box, int damage)
        {
            var go = new GameObject(name);
            go.layer = _layers.EnemyDamage;
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

            var machine = _driver.Machine;
            float speed = _definition.Tuning.SpeedForPhase(machine.Phase);
            float bdt = dt * speed; // boss time
            _clock += bdt;
            _player.Speed = speed;

            _driver.Tick(bdt);
            RunSchedule();

            var position = transform.position;
            if (_arc.Active)
            {
                _arc.Time += bdt;
                float p = Mathf.Clamp01(_arc.Time / _arc.Duration);
                position.x = Mathf.Lerp(_arc.FromX, _arc.ToX, _arc.Slam ? Mathf.Min(1f, p / 0.6f) : p);
                float lift = _arc.Slam
                    ? (p < 0.6f ? Mathf.Sin(p / 0.6f * Mathf.PI / 2f) : 1f - (p - 0.6f) / 0.4f)
                    : 4f * p * (1f - p);
                position.y = Floor + _arc.Height * lift;
                transform.rotation = Quaternion.Euler(0, 0, _arc.Spins * 360f * p * -_facing);
                if (p >= 1f) EndArc();
            }
            else
            {
                position.x += _velocityX * bdt;
                position.y = Floor;
            }
            position.x = _origin.x + _arena.ClampX(position.x - _origin.x, _halfWidth);
            transform.position = position;

            bool contactOn = !machine.IsInvulnerable && machine.State != BossState.Defeated;
            if (_contact.activeSelf != contactOn) _contact.SetActive(contactOn);

            UpdateTint(dt);
        }

        private void UpdateTint(float dt)
        {
            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                _renderer.color = new Color(1f, 0.55f, 0.55f);
                return;
            }
            if (_stage == AttackStage.Windup && _stageAttack != null && _stageAttack.IsSpecial)
            {
                // Telegraph: the reflective bands "light up" before a special.
                float pulse = 0.5f + 0.5f * Mathf.Sin(_clock * 28f);
                var glow = _stageAttack.Id == "SaludoCargado" ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.9f, 0.35f);
                _renderer.color = Color.Lerp(Color.white, glow, pulse);
                return;
            }
            _renderer.color = Color.white;
        }

        // ---- Choreography ----

        private void OnAttackStage(AttackDefinition attack, AttackStage stage)
        {
            _stage = stage;
            _stageAttack = attack;
            _stageStart = _clock;
            if (stage == AttackStage.None) { _schedule.Clear(); EndArc(); return; }

            var hero = HeroController.instance;
            float heroX = hero != null ? hero.transform.position.x : transform.position.x;
            float targetX = Mathf.Clamp(heroX, LeftWall + _halfWidth, RightWall - _halfWidth);

            switch (attack.Id)
            {
                case AttackCatalog.LeapAttackId:
                    if (stage == AttackStage.Active) StartArc(targetX, 3.2f, attack.Active, false, 0f);
                    break;

                case "PisotonSimi":
                    if (stage == AttackStage.Active) StartArc(targetX, 7f, attack.Active, true, 0f);
                    if (stage == AttackStage.Recovery) SpawnWaves(13f, 1.6f, 1.3f);
                    break;

                case "GiroBotarga":
                    if (stage == AttackStage.Active) StartArc(targetX, 3.6f, attack.Active, false, 2f);
                    if (stage == AttackStage.Recovery) SpawnWaves(9f, 1.1f, 0.9f, 1.4f);
                    break;

                case "LluviaCapsulas":
                    if (stage == AttackStage.Windup) PlanCapsuleWave(0f, attack.Windup);
                    if (stage == AttackStage.Active)
                    {
                        float second = attack.Active * 0.5f;
                        // Warnings appear 0.55 s before the second wave lands, with a new safe lane.
                        At(second - 0.55f, () => PlanCapsuleWave(0f, 0.55f));
                    }
                    break;

                case "AplausoSalida":
                    if (stage == AttackStage.Active)
                        for (int i = 0; i < 3; i++)
                        {
                            float at = i * attack.Active / 3f;
                            At(at, () => SpawnWaves(12f, 1.4f, 1.2f));
                        }
                    break;
            }
        }

        private void StartArc(float toX, float height, float duration, bool slam, float spins)
        {
            _arc = new Arc { Active = true, Slam = slam, FromX = transform.position.x, ToX = toX, Height = height, Duration = Mathf.Max(0.05f, duration), Spins = spins };
        }

        private void EndArc()
        {
            _arc.Active = false;
            transform.rotation = Quaternion.identity;
        }

        private void At(float delay, Action run) { _schedule.Add(new Scheduled { At = _clock + Mathf.Max(0f, delay), Run = run }); }

        private void RunSchedule()
        {
            for (int i = 0; i < _schedule.Count; i++)
            {
                if (_schedule[i].At > _clock) continue;
                var run = _schedule[i].Run;
                _schedule.RemoveAt(i--);
                run();
            }
        }

        /// <summary>Two ground waves travelling left and right from the boss's feet.</summary>
        private void SpawnWaves(float speed, float width, float height, float life = 6f)
        {
            float x = transform.position.x;
            foreach (int dir in new[] { -1, 1 })
            {
                Hazard.Spawn(transform.parent, "Wave", ProceduralSprites.Wave, new Vector3(x + dir * 0.6f, Floor, 0f),
                             new Vector2(width, height * 2f), new Vector2(0.8f, 0.4f), new Vector2(0f, 0.2f),
                             new Vector2(dir * speed, 0f), life, _layers.EnemyDamage, _definition.Tuning.AttackDamage,
                             LeftWall, RightWall, float.MinValue, false);
            }
        }

        /// <summary>
        /// Splits the arena into lanes, keeps one random lane safe, shows warnings on the rest and drops
        /// capsules there after <paramref name="warnFor"/> seconds. Never fills every lane.
        /// </summary>
        private void PlanCapsuleWave(float startDelay, float warnFor)
        {
            float laneWidth = (RightWall - LeftWall) / CapsuleLanes;
            int safe = UnityEngine.Random.Range(0, CapsuleLanes);
            for (int lane = 0; lane < CapsuleLanes; lane++)
            {
                if (lane == safe) continue;
                float cx = LeftWall + laneWidth * (lane + 0.5f);
                Telegraph.Show(transform.parent, new Vector3(cx, Floor, 0.05f), new Vector2(laneWidth * 0.9f / (8f / 64f), (Top - Floor) / 1f), startDelay + warnFor);
                float x = cx;
                At(startDelay + warnFor, () =>
                    Hazard.Spawn(transform.parent, "Capsule", ProceduralSprites.Capsule, new Vector3(x, Top - 0.5f, 0f),
                                 Vector2.one, new Vector2(0.5f, 1.2f), Vector2.zero, new Vector2(0f, -17f), 4f,
                                 _layers.EnemyDamage, _definition.Tuning.AttackDamage, LeftWall - 1f, RightWall + 1f, Floor, true));
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
            if (result == HitResult.Stunned || result == HitResult.PhaseChanged || result == HitResult.Defeated)
            {
                _schedule.Clear();
                EndArc();
            }
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
            // Only punching attacks use the fist hitbox; jumps, waves and capsules damage by themselves.
            bool usesFist = attack.Id == AttackCatalog.BasicAttackId || attack.Id == "SaludoCargado";
            PlaceBox(_punch, _definition.Manifest.PunchBox);
            _punch.SetActive(active && usesFist);
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
            _schedule.Clear();
            EndArc();
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
