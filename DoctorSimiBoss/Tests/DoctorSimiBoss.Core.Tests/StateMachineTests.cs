using System;
using System.Collections.Generic;
using System.Linq;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;
using DoctorSimiBoss.Core.Hosting;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class StateMachineTests
    {
        private const float Far = 10f;
        private const float Near = 1f;

        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        /// <summary>Fight-loop tests skip the entrance; IntroTests cover it.</summary>
        private BossStateMachine Create(IBossBrain brain, Action<BossTuning> tweak = null)
        {
            // Fixed behaviour-test tuning, independent of the balance values in the manifest.
            var t = _def.Tuning;
            t.IntroDuration = 0;
            t.MaxHealth = 900; // thresholds below are written for 900 HP
            t.MoveSpeed = 4f;
            t.AttackRange = 3f;
            t.IdleBetweenActions = 0.8f;
            t.MoveDuration = 1.2f;
            t.HurtDuration = 0.25f;
            t.StunHitThreshold = 12;
            t.StunDuration = 2.5f;
            t.PhaseTransitionDuration = 1.5f;
            tweak?.Invoke(t);
            return _def.CreateStateMachine(brain);
        }

        private static void Run(BossStateMachine m, float seconds, float playerOffset, float step = 1f / 60f)
        {
            for (float t = 0; t < seconds; t += step) m.Tick(step, playerOffset);
        }

        [Fact]
        public void StartsIdleInPhaseOneWithFullHealth()
        {
            var m = Create(new ScriptedBrain());
            m.Start();
            Assert.Equal(BossState.Idle, m.State);
            Assert.Equal(1, m.Phase);
            Assert.Equal(900, m.Health);
            Assert.False(m.IsInvulnerable);
        }

        [Theory]
        [InlineData(100f, 1)]
        [InlineData(66f, 1)]
        [InlineData(65.9f, 2)]
        [InlineData(33f, 2)]
        [InlineData(32.9f, 3)]
        [InlineData(0f, 3)]
        public void PhaseThresholdsFollowTheManifest(float percent, int expected)
        {
            Assert.Equal(expected, Create(new ScriptedBrain()).PhaseForPercent(percent));
        }

        [Fact]
        public void IdleThenMovesTowardsADistantPlayer()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => BossAction.Move(2f));
            var m = Create(brain);

            Run(m, 0.5f, Far);
            Assert.Equal(BossState.Idle, m.State);
            Assert.Equal(1, m.Facing);

            Run(m, 0.5f, Far);
            Assert.Equal(BossState.Move, m.State);
            Assert.Equal(_def.Tuning.MoveSpeed, m.VelocityX);

            Run(m, 0.1f, -Far);
            Assert.Equal(-1, m.Facing);
            Assert.Equal(-_def.Tuning.MoveSpeed, m.VelocityX);
        }

        [Fact]
        public void MovementStopsToDecideOnceInRange()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => BossAction.Move(5f));
            brain.Script.Enqueue(ctx => BossAction.Perform(ctx.BasicAttack));
            var m = Create(brain);
            Run(m, 0.9f, Far);
            Assert.Equal(BossState.Move, m.State);

            m.Tick(0.016f, Near);
            Assert.Equal(BossState.Attack, m.State);
            Assert.Equal(AttackCatalog.BasicAttackId, m.CurrentAttack.Id);
        }

        [Fact]
        public void BasicAttackTimingMatchesTheAttackClipDamageFrame()
        {
            var basic = _def.Attacks.Basic;
            var clip = _def.Manifest.Animations["Attack"];
            Assert.Equal(clip.WeightBefore(3) / 12f, basic.Windup, 4);
            Assert.Equal(clip.WeightOf(3) / 12f, basic.Active, 4);
            Assert.Equal((clip.TotalWeight - clip.WeightBefore(4)) / 12f, basic.Recovery, 4);
        }

        [Fact]
        public void AttackRunsWindupActiveRecoveryThenIdles()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => BossAction.Perform(ctx.BasicAttack));
            var m = Create(brain);
            var stages = new List<AttackStage>();
            m.AttackStageChanged += (a, s) => stages.Add(s);

            m.Tick(0.8f, Near);
            Assert.Equal(BossState.Attack, m.State);
            Assert.Equal(AttackStage.Windup, m.AttackStage);
            Assert.False(m.IsAttackHitboxActive);

            m.Tick(_def.Attacks.Basic.Windup + 0.01f, Near);
            Assert.Equal(AttackStage.Active, m.AttackStage);
            Assert.True(m.IsAttackHitboxActive);

            m.Tick(_def.Attacks.Basic.Active, Near);
            Assert.Equal(AttackStage.Recovery, m.AttackStage);
            Assert.False(m.IsAttackHitboxActive);

            m.Tick(_def.Attacks.Basic.Recovery, Near);
            Assert.Equal(BossState.Idle, m.State);
            Assert.Null(m.CurrentAttack);
            Assert.Equal(new[] { AttackStage.Windup, AttackStage.Active, AttackStage.Recovery, AttackStage.None }, stages);
        }

        [Fact]
        public void FacingIsLockedDuringAnAttack()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => BossAction.Perform(ctx.BasicAttack));
            var m = Create(brain);
            m.Tick(0.8f, Near);
            Assert.Equal(1, m.Facing);
            m.Tick(0.05f, -Near);
            Assert.Equal(1, m.Facing);
        }

        [Fact]
        public void LargeTickSkipsThroughStagesWithoutLosingTime()
        {
            var brain = new ScriptedBrain();
            var saludo = _def.Attacks.Get("SaludoCargado");
            brain.Script.Enqueue(ctx => BossAction.Perform(saludo));
            var m = Create(brain);
            var stages = new List<AttackStage>();
            m.AttackStageChanged += (a, s) => stages.Add(s);
            m.Tick(0.8f, Near);
            // Advancing 0.1 s past the whole attack ends it with 0.1 s already spent idling.
            m.Tick(saludo.TotalDuration + 0.1f, Near);
            Assert.Equal(BossState.Idle, m.State);
            Assert.Equal(_def.Tuning.IdleBetweenActions - 0.1f, m.TimeRemaining, 3);
            Assert.Equal(new[] { AttackStage.Windup, AttackStage.Active, AttackStage.Recovery, AttackStage.None }, stages);
        }

        [Fact]
        public void HitWhileIdleFlinchesButAttacksHaveSuperArmour()
        {
            var brain = new ScriptedBrain();
            var m = Create(brain);
            m.Start();
            Assert.Equal(HitResult.Flinched, m.ReceiveHit(10));
            Assert.Equal(BossState.Hurt, m.State);
            Run(m, 0.3f, Far);
            Assert.Equal(BossState.Idle, m.State);

            brain.Script.Enqueue(ctx => BossAction.Perform(ctx.BasicAttack));
            m.Tick(1f, Near);
            Assert.Equal(BossState.Attack, m.State);
            Assert.Equal(HitResult.Damaged, m.ReceiveHit(10));
            Assert.Equal(BossState.Attack, m.State);
            Assert.Equal(880, m.Health);
        }

        [Fact]
        public void EnoughHitsStunTheBossAndCancelTheAttack()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => BossAction.Perform(ctx.BasicAttack));
            var m = Create(brain);
            m.Tick(0.8f, Near);
            Assert.Equal(BossState.Attack, m.State);

            HitResult last = HitResult.Ignored;
            for (int i = 0; i < _def.Tuning.StunHitThreshold; i++) last = m.ReceiveHit(1);
            Assert.Equal(HitResult.Stunned, last);
            Assert.Equal(BossState.Stun, m.State);
            Assert.Null(m.CurrentAttack);

            // Further hits during the stun do damage but do not restart it.
            Assert.Equal(HitResult.Damaged, m.ReceiveHit(1));
            Run(m, _def.Tuning.StunDuration + 0.05f, Near);
            Assert.Equal(BossState.Idle, m.State);
        }

        [Fact]
        public void CrossingAThresholdTriggersAnInvulnerablePhaseTransition()
        {
            var brain = new ScriptedBrain();
            var m = Create(brain, t => t.StunHitThreshold = 0);
            var phaseChanges = new List<(int, int)>();
            m.PhaseChanged += (a, b) => phaseChanges.Add((a, b));
            m.Start();

            m.ReceiveHit(300); // 600/900 = 66.7% -> still phase 1
            Assert.Equal(1, m.Phase);
            Assert.Equal(HitResult.PhaseChanged, m.ReceiveHit(10)); // 590/900 = 65.6%
            Assert.Equal(2, m.Phase);
            Assert.Equal(BossState.PhaseTransition, m.State);
            Assert.True(m.IsInvulnerable);
            Assert.Equal(HitResult.Ignored, m.ReceiveHit(50));
            Assert.Equal(590, m.Health);

            Run(m, _def.Tuning.PhaseTransitionDuration + 0.05f, Far);
            Assert.Equal(BossState.Idle, m.State);
            Assert.False(m.IsInvulnerable);
            Assert.Equal(new[] { (1, 2) }, phaseChanges);
        }

        [Fact]
        public void PhaseTransitionDoesNotAttack()
        {
            var brain = new ScriptedBrain();
            var m = Create(brain);
            m.Start();
            m.ReceiveHit(310);
            int callsBefore = brain.Calls.Count;
            Run(m, _def.Tuning.PhaseTransitionDuration - 0.1f, Near);
            Assert.Equal(BossState.PhaseTransition, m.State);
            Assert.Equal(callsBefore, brain.Calls.Count);
        }

        [Fact]
        public void ReachingZeroHealthDefeatsTheBossForGood()
        {
            var brain = new ScriptedBrain();
            var m = Create(brain);
            m.Start();
            Assert.Equal(HitResult.Defeated, m.ReceiveHit(5000));
            Assert.Equal(BossState.Defeated, m.State);
            Assert.Equal(0, m.Health);
            Assert.Equal(HitResult.Ignored, m.ReceiveHit(10));
            Run(m, 5f, Near);
            Assert.Equal(BossState.Defeated, m.State);
            Assert.Equal(0, m.VelocityX);
        }

        [Fact]
        public void CarreraDePasilloDashesThenStunsItself()
        {
            var brain = new ScriptedBrain();
            var dash = _def.Attacks.Get("CarreraPasillo");
            brain.Script.Enqueue(ctx => BossAction.Perform(dash));
            var m = Create(brain);
            m.Tick(0.8f, -Far);
            Assert.Equal(-1, m.Facing);
            m.Tick(dash.Windup + 0.01f, -Far);
            Assert.Equal(AttackStage.Active, m.AttackStage);
            Assert.Equal(-_def.Tuning.MoveSpeed * dash.ActiveSpeedMultiplier, m.VelocityX, 3);
            m.Tick(dash.Active + dash.Recovery, -Far);
            Assert.Equal(BossState.Stun, m.State);
            Assert.Equal(0, m.VelocityX);
        }
    }

    public class BrainAndCatalogTests
    {
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        [Fact]
        public void PhasesOfferTheDesignedSpecials()
        {
            // Each phase adds new attacks and keeps the earlier ones (includesPreviousSpecials on phases 2 and 3).
            Assert.Equal(new[] { "SaludoCargado", "PisotonSimi" }, _def.Attacks.SpecialsForPhase(1).Select(a => a.Id));
            Assert.Equal(new[] { "SaludoCargado", "PisotonSimi", "CarreraPasillo", "GiroBotarga", "JeringasGuiadas" },
                         _def.Attacks.SpecialsForPhase(2).Select(a => a.Id));
            Assert.Equal(new[] { "SaludoCargado", "PisotonSimi", "CarreraPasillo", "GiroBotarga", "JeringasGuiadas",
                                 "LluviaCapsulas", "AplausoSalida", "EspiralVendas" },
                         _def.Attacks.SpecialsForPhase(3).Select(a => a.Id));
        }

        [Fact]
        public void TimingsComeFromTheManifest()
        {
            var aplauso = _def.Attacks.Get("AplausoSalida");
            Assert.Equal("Aplauso de salida", aplauso.DisplayName);
            Assert.Equal(1.5f, aplauso.Recovery, 3);
            Assert.True(aplauso.Recovery > _def.Attacks.Get("GiroBotarga").Recovery);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void DefaultBrainNeverChainsSpecialsAndOnlyUsesThePhaseSet(int phase)
        {
            var brain = new DefaultBossBrain(new Random(1234));
            var tuning = new BossTuning { SpecialChance = 1f };
            AttackDefinition last = null;
            var allowed = new HashSet<string>(_def.Attacks.SpecialsForPhase(phase).Select(a => a.Id));
            var used = new HashSet<string>();
            for (int i = 0; i < 500; i++)
            {
                var action = brain.Decide(new BrainContext
                {
                    Phase = phase,
                    PlayerOffsetX = 1f,
                    Tuning = tuning,
                    BasicAttack = _def.Attacks.Basic,
                    PhaseSpecials = _def.Attacks.SpecialsForPhase(phase),
                    LastAttack = last,
                });
                Assert.Equal(BossActionKind.Attack, action.Kind);
                if (last != null && last.IsSpecial) Assert.False(action.Attack.IsSpecial);
                if (action.Attack.IsSpecial) { Assert.Contains(action.Attack.Id, allowed); used.Add(action.Attack.Id); }
                last = action.Attack;
            }
            Assert.True(allowed.SetEquals(used), "every phase special should eventually be chosen");
        }

        [Fact]
        public void DefaultBrainThrowsFromAfarInsteadOfWalking()
        {
            var brain = new DefaultBossBrain(new Random(7));
            var ctx = new BrainContext
            {
                Phase = 1,
                PlayerOffsetX = 10f,
                Tuning = new BossTuning { SpecialChance = 0f },
                BasicAttack = _def.Attacks.Basic,
                PhaseSpecials = _def.Attacks.SpecialsForPhase(1),
            };
            // The basic attack is a thrown capsule, so from far away the boss throws (or leaps) instead of walking.
            var far = brain.Decide(ctx);
            Assert.Equal(BossActionKind.Attack, far.Kind);
            Assert.Contains(far.Attack.Id, new[] { AttackCatalog.BasicAttackId, AttackCatalog.LeapAttackId });

            ctx.Tuning = new BossTuning { SpecialChance = 1f };
            var action = brain.Decide(ctx);
            Assert.True(action.Attack.IsSpecial && action.Attack.Ranged, "both phase 1 specials are ranged now");
        }
    }

    public class AnimationAndDriverTests
    {
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        [Fact]
        public void SpecialsUseTheirDedicatedClips()
        {
            var resolver = _def.CreateAnimationResolver(PlaceholderPolicy.PreferAvailable);
            var saludo = _def.Attacks.Get("SaludoCargado");
            var punch = resolver.Resolve(BossState.Attack, saludo);
            Assert.Equal("SpecialPunch", punch.Clip);
            Assert.False(punch.IsPlaceholder);
            var punchClip = _def.Manifest.Animations["SpecialPunch"];
            Assert.Equal(punchClip.WeightBefore(3) / saludo.Windup, punch.Fps, 3); // damage frame 03 lands when the active part starts

            var slamAttack = _def.Attacks.Get("PisotonSimi");
            var slam = resolver.Resolve(BossState.Attack, slamAttack);
            Assert.Equal("SpecialSlam", slam.Clip);
            // The slam's impact frame (activeEndFrame 4) starts exactly when the jump ends.
            Assert.Equal(_def.Manifest.Animations["SpecialSlam"].WeightBefore(4) / (slamAttack.Windup + slamAttack.Active), slam.Fps, 3);

            Assert.Equal("SpecialDash", resolver.Resolve(BossState.Attack, _def.Attacks.Get("CarreraPasillo")).Clip);
            Assert.Equal("Attack", _def.CreateAnimationResolver(PlaceholderPolicy.IdleAndAttackOnly).Resolve(BossState.Attack, saludo).Clip);
        }

        [Fact]
        public void WithoutSpecialSpritesSpecialsUseTheAttackClipStretchedToTheirWindup()
        {
            var (m, report, attacks) = TestSupport.WithoutSpecialSprites();
            var resolver = new BossAnimationResolver(m, report, PlaceholderPolicy.PreferAvailable);
            var saludo = attacks.Get("SaludoCargado");
            var request = resolver.Resolve(BossState.Attack, saludo);
            Assert.Equal("Attack", request.Clip);
            Assert.True(request.IsPlaceholder);
            Assert.Equal(m.Animations["Attack"].WeightBefore(3) / saludo.Windup, request.Fps, 3);

            var basic = resolver.Resolve(BossState.Attack, attacks.Basic);
            Assert.Equal("Attack", basic.Clip);
            Assert.False(basic.IsPlaceholder);
            Assert.Equal(12f, basic.Fps, 3);
        }

        [Theory]
        [InlineData(PlaceholderPolicy.PreferAvailable, BossState.Intro, "Spawn")]
        [InlineData(PlaceholderPolicy.IdleAndAttackOnly, BossState.Intro, "Idle")]
        [InlineData(PlaceholderPolicy.PreferAvailable, BossState.Move, "Walk")]
        [InlineData(PlaceholderPolicy.PreferAvailable, BossState.Hurt, "Hurt")]
        [InlineData(PlaceholderPolicy.PreferAvailable, BossState.Stun, "Stun")]
        [InlineData(PlaceholderPolicy.PreferAvailable, BossState.Defeated, "Defeat")]
        [InlineData(PlaceholderPolicy.PreferAvailable, BossState.PhaseTransition, "Idle")]
        [InlineData(PlaceholderPolicy.IdleAndAttackOnly, BossState.Move, "Idle")]
        [InlineData(PlaceholderPolicy.IdleAndAttackOnly, BossState.Stun, "Idle")]
        [InlineData(PlaceholderPolicy.IdleAndAttackOnly, BossState.Defeated, "Idle")]
        public void StatesMapToClips(PlaceholderPolicy policy, BossState state, string clip)
        {
            Assert.Equal(clip, _def.CreateAnimationResolver(policy).Resolve(state, null).Clip);
        }

        [Fact]
        public void DriverPushesAnimationHitboxAndEffectToTheBody()
        {
            _def.Tuning.IntroDuration = 0;
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => BossAction.Perform(ctx.BasicAttack));
            var machine = _def.CreateStateMachine(brain);
            var body = new FakeBody { PlayerOffsetX = 1f };
            var driver = new BossDriver(machine, _def.CreateAnimationResolver(PlaceholderPolicy.PreferAvailable), body);

            driver.Tick(0.016f);
            Assert.Equal("Idle", body.Animations.Last().Clip);
            Assert.Equal(1, body.Facing);

            driver.Tick(0.8f);
            Assert.Equal("Attack", body.Animations.Last().Clip);
            driver.Tick(_def.Attacks.Basic.Windup + 0.01f);
            Assert.True(body.HitboxActive);
            driver.Tick(_def.Attacks.Basic.Active);
            Assert.False(body.HitboxActive);

            Assert.Equal(new[] { "anim:Idle", "anim:Attack", "hitbox-on:Lanzamiento", "effect:PunchImpact", "hitbox-off:Lanzamiento" },
                         body.Log.ToArray());

            driver.ReceiveHit(5000);
            Assert.Equal("Defeat", body.Animations.Last().Clip);
            Assert.Equal("defeated", body.Log.Last());
            Assert.Equal(0f, body.Velocity);
        }
    }
}
