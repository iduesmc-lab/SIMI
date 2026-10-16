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
        private WhiteFlash _whiteFlash;
        private EnemyHitEffectsUninfected _hitEffects; // Hollow Knight's own hit sound/particles, if a donor was found
        private float _velocityX;      // target from the state machine
        private float _currentVx;      // smoothed actual speed
        private GameObject _visual;    // sprite child: deformed by the procedural animator, colliders stay on the root
        private readonly ProceduralAnimator _motion = new ProceduralAnimator();
        private float _spin;           // degrees, from spinning jumps
        private float _prevY;
        private float _ghostTimer;
        private int _facing = -1;
        private float _halfWidth;
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
            // Scaling the root grows the sprite and every collider under it together.
            float bossScale = manifest.BossScale > 0 ? manifest.BossScale : 1f;
            transform.localScale = new Vector3(bossScale, bossScale, 1f);

            _visual = new GameObject("Visual");
            _visual.transform.SetParent(transform, false);
            _renderer = _visual.AddComponent<SpriteRenderer>();
            _player = _visual.AddComponent<FramePlayer>();
            _motion.Intensity = manifest.Motion.Intensity;
            _prevY = transform.position.y;
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.isKinematic = true;

            // Hurtbox: what the nail hits.
            var bodyBox = FrameGeometry.ToLocal(manifest, manifest.BodyBox, -1);
            var hurt = gameObject.AddComponent<BoxCollider2D>();
            hurt.isTrigger = true;
            hurt.offset = new Vector2(bodyBox.CenterX, bodyBox.CenterY);
            hurt.size = new Vector2(bodyBox.Width, bodyBox.Height);
            _halfWidth = bodyBox.Width / 2f * bossScale;

            // HeroBox only checks that a DamageHero component exists, so damage areas are switched
            // off by deactivating their GameObject (entrance, phase change, defeat).
            _contact = CreateDamageArea("Contact Damage", bodyBox, definition.Tuning.ContactDamage);
            // No melee hitbox: the boss attacks with projectiles, waves, the dash and its body.
            // hitboxes.punch is only used as the hand position projectiles are thrown from.
            _whiteFlash = WhiteFlash.Create(_renderer);
            _hitEffects = HitEffectsDonor.AttachTo(gameObject, new Vector3(0f, bodyBox.CenterY * bossScale, 0f));
            _log(_hitEffects != null ? "Boss uses Hollow Knight's enemy hit effects."
                                     : "No enemy hit effects found yet (visit a room with normal enemies first); using the white flash only.");

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
            float accel = machine.State == BossState.Attack ? 200f : _definition.Manifest.Motion.Acceleration;
            _currentVx = Mathf.MoveTowards(_currentVx, _velocityX, accel * bdt);
            if (_arc.Active)
            {
                _arc.Time += bdt;
                float p = Mathf.Clamp01(_arc.Time / _arc.Duration);
                position.x = Mathf.Lerp(_arc.FromX, _arc.ToX, _arc.Slam ? Mathf.Min(1f, p / 0.6f) : p);
                float lift = _arc.Slam
                    ? (p < 0.6f ? Mathf.Sin(p / 0.6f * Mathf.PI / 2f) : 1f - (p - 0.6f) / 0.4f)
                    : 4f * p * (1f - p);
                position.y = Floor + _arc.Height * lift;
                _spin = _arc.Spins * 360f * p * -_facing;
                if (p >= 1f)
                {
                    EndArc();
                    _motion.Land(_arc.Slam ? 1.4f : 0.9f);
                }
            }
            else
            {
                position.x += _currentVx * bdt;
                position.y = Floor;
            }
            position.x = _origin.x + _arena.ClampX(position.x - _origin.x, _halfWidth);
            transform.position = position;

            ApplyMotion(machine, bdt, position.y);

            bool contactOn = !machine.IsInvulnerable && machine.State != BossState.Defeated;
            if (_contact.activeSelf != contactOn) _contact.SetActive(contactOn);

            UpdateTint(dt);
        }

        private void UpdateTint(float dt)
        {
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
            if (stage == AttackStage.Active && attack.Id != AttackCatalog.LeapAttackId &&
                attack.Id != "PisotonSimi" && attack.Id != "GiroBotarga")
                _motion.Strike(attack.IsSpecial ? 1.2f : 0.8f);

            var hero = HeroController.instance;
            float heroX = hero != null ? hero.transform.position.x : transform.position.x;
            float targetX = Mathf.Clamp(heroX, LeftWall + _halfWidth, RightWall - _halfWidth);

            switch (attack.Id)
            {
                case AttackCatalog.LeapAttackId:
                    if (stage == AttackStage.Active) StartArc(targetX, 3.2f, attack.Active, false, 0f);
                    break;

                case AttackCatalog.BasicAttackId:
                    if (stage == AttackStage.Active) ThrowLob(heroX);
                    break;

                case "SaludoCargado":
                    if (stage == AttackStage.Active) FanShot(hero);
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
            _spin = 0f;
        }

        /// <summary>Procedural squash/stretch/lean on the sprite child, plus afterimages during fast moves.</summary>
        private void ApplyMotion(BossStateMachine machine, float bdt, float y)
        {
            float vy = (y - _prevY) / bdt;
            _prevY = y;
            var attack = machine.CurrentAttack;
            float duration = attack == null ? 1f
                : machine.AttackStage == AttackStage.Windup ? attack.Windup
                : machine.AttackStage == AttackStage.Active ? attack.Active
                : attack.Recovery;
            float progress = duration > 0 ? (_clock - _stageStart) / duration : 1f;

            _motion.Tick(bdt, machine.State, machine.AttackStage, attack, progress, _currentVx, _arc.Active, vy);
            var pose = _motion.Pose;
            _visual.transform.localScale = new Vector3(pose.ScaleX, pose.ScaleY, 1f);
            _visual.transform.localPosition = new Vector3(0f, pose.OffsetY, 0f);
            // Positive lean tilts the top towards the facing direction.
            _visual.transform.localRotation = Quaternion.Euler(0f, 0f, _spin - _facing * pose.Lean);

            bool fast = attack != null && machine.AttackStage == AttackStage.Active &&
                        (_arc.Active || attack.ActiveSpeedMultiplier >= 3f);
            if (fast && _definition.Manifest.Motion.Afterimages)
            {
                _ghostTimer -= bdt;
                if (_ghostTimer <= 0f)
                {
                    _ghostTimer = 0.045f;
                    Afterimage.Spawn(transform.parent, _renderer, _visual.transform);
                }
            }
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
            var art = _sprites.Get("Shockwave");
            foreach (int dir in new[] { -1, 1 })
            {
                var position = new Vector3(x + dir * 0.6f, Floor, 0f);
                var velocity = new Vector2(dir * speed, 0f);
                if (art != null && art.Length >= 5)
                {
                    // HD wave (about 3.1 x 1.05 units, pivot on the ground): loop its three biggest frames.
                    var loop = new[] { art[2], art[3], art[4] };
                    var scale = new Vector2(width * 1.8f / 3.1f, height / 1.05f);
                    Hazard.Spawn(transform.parent, "Wave", loop, 12f, dir < 0, 0f, position, scale,
                                 new Vector2(1.4f, 0.7f), new Vector2(0f, 0.35f), velocity, life,
                                 _layers.EnemyDamage, _definition.Tuning.AttackDamage, LeftWall, RightWall, float.MinValue, false);
                }
                else
                {
                    Hazard.Spawn(transform.parent, "Wave", new[] { ProceduralSprites.Wave }, 0f, false, 0f, position,
                                 new Vector2(width, height * 2f), new Vector2(0.8f, 0.4f), new Vector2(0f, 0.2f), velocity, life,
                                 _layers.EnemyDamage, _definition.Tuning.AttackDamage, LeftWall, RightWall, float.MinValue, false);
                }
            }
        }

        /// <summary>One-shot effect (impact, burst) from the manifest's effects.</summary>
        private void SpawnEffect(string effectName, Vector3 position, bool flipX, float scale = 1f)
        {
            var frames = _sprites.Get(effectName);
            if (frames == null || !_definition.Manifest.Effects.TryGetValue(effectName, out var clip)) return;
            var go = new GameObject(effectName);
            go.transform.SetParent(transform.parent, true);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.flipX = flipX;
            renderer.sortingOrder = 6;
            var player = go.AddComponent<FramePlayer>();
            player.DestroyWhenDone = true;
            player.Play(frames, clip.Fps, false);
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
                At(startDelay + warnFor, () => DropCapsule(x));
            }
        }

        /// <summary>Where projectiles leave the hand (hitboxes.punch), in world space.</summary>
        private Vector3 HandPosition()
        {
            var box = FrameGeometry.ToLocal(_definition.Manifest, _definition.Manifest.PunchBox, _facing);
            float s = transform.localScale.x;
            return transform.position + new Vector3(box.CenterX * s, box.CenterY * s, 0f);
        }

        private Hazard SpawnCapsule(Vector3 start, Vector2 velocity, float gravity, float spin, float rotation)
        {
            var art = _sprites.Get("Capsule");
            var frames = art ?? new[] { ProceduralSprites.Capsule };
            var collider = art != null ? new Vector2(1.2f, 0.6f) : new Vector2(0.5f, 1.2f);
            var hazard = Hazard.Spawn(transform.parent, "Capsule", frames, 6f, false, rotation, start, Vector2.one,
                                      collider, Vector2.zero, velocity, 6f, _layers.EnemyDamage, _definition.Tuning.AttackDamage,
                                      LeftWall - 0.5f, RightWall + 0.5f, Floor, true,
                                      p => SpawnEffect("CapsuleBurst", p + new Vector3(0f, 0.4f, 0f), false));
            hazard.Gravity = gravity;
            hazard.Spin = spin;
            return hazard;
        }

        /// <summary>Basic attack: lobs a tumbling capsule that lands where the Knight is standing.</summary>
        private void ThrowLob(float targetX)
        {
            const float gravity = 28f, flightTime = 0.85f;
            var start = HandPosition();
            float vx = (targetX - start.x) / flightTime;
            float vy = (Floor - start.y + 0.5f * gravity * flightTime * flightTime) / flightTime;
            SpawnCapsule(start, new Vector2(vx, vy), gravity, 540f * -Mathf.Sign(vx == 0 ? _facing : vx), 0f);
        }

        /// <summary>Saludo cargado: three fast capsules in a fan, aimed at the Knight's chest.</summary>
        private void FanShot(HeroController hero)
        {
            const float speed = 16f;
            var start = HandPosition();
            Vector2 aim = hero != null ? (Vector2)(hero.transform.position + new Vector3(0f, 0.3f, 0f) - start) : new Vector2(_facing, 0f);
            if (aim.sqrMagnitude < 0.01f) aim = new Vector2(_facing, 0f);
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            foreach (float spread in new[] { -12f, 0f, 12f })
            {
                float angle = baseAngle + spread;
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                // The HD capsule is drawn lying horizontally, so it points along its flight direction.
                SpawnCapsule(start, dir * speed, 0f, 0f, angle);
            }
        }

        private void DropCapsule(float x)
        {
            var art = _sprites.Get("Capsule");
            var start = new Vector3(x, Top - 0.5f, 0f);
            var velocity = new Vector2(0f, -17f);
            int damage = _definition.Tuning.AttackDamage;
            if (art != null)
            {
                // HD capsule is drawn lying down (about 1.4 x 0.8 units): rotate it to fall upright.
                Hazard.Spawn(transform.parent, "Capsule", art, 6f, false, -90f, start, Vector2.one,
                             new Vector2(1.2f, 0.6f), Vector2.zero, velocity, 4f, _layers.EnemyDamage, damage,
                             LeftWall - 1f, RightWall + 1f, Floor, true, p => SpawnEffect("CapsuleBurst", p + new Vector3(0f, 0.4f, 0f), false));
            }
            else
            {
                Hazard.Spawn(transform.parent, "Capsule", new[] { ProceduralSprites.Capsule }, 0f, false, 0f, start, Vector2.one,
                             new Vector2(0.5f, 1.2f), Vector2.zero, velocity, 4f, _layers.EnemyDamage, damage,
                             LeftWall - 1f, RightWall + 1f, Floor, true);
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

            _whiteFlash.Flash();
            if (_hitEffects != null) _hitEffects.RecieveHitEffect(damageInstance.GetActualDirection(transform));
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
            if (frames != null) _player.Play(frames, request.Fps, request.Loop, request.FrameWeights);
        }

        public void SetFacing(int direction)
        {
            _facing = direction;
            _renderer.flipX = FrameGeometry.NeedsFlip(_definition.Manifest, direction);
            PlaceBox(_contact, _definition.Manifest.BodyBox);
        }

        public void SetVelocityX(float velocity) { _velocityX = velocity; }

        public void SetAttackActive(AttackDefinition attack, bool active)
        {
            // Nothing to switch: there is no melee hitbox. Projectiles are spawned in OnAttackStage.
        }

        public void PlayEffect(string effectName)
        {
            var box = FrameGeometry.ToLocal(_definition.Manifest, _definition.Manifest.PunchBox, _facing);
            float s = transform.localScale.x;
            SpawnEffect(effectName, transform.position + new Vector3(box.CenterX * s, box.CenterY * s, -0.02f), _renderer.flipX, s);
        }

        public void OnDefeated()
        {
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
