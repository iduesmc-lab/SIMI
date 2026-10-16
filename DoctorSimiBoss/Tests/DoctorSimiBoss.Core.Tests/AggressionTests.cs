using System;
using System.Collections.Generic;
using System.Linq;
using DoctorSimiBoss.Core.Animation;
using DoctorSimiBoss.Core.Combat;
using Xunit;

namespace DoctorSimiBoss.Core.Tests
{
    public class AggressionTests
    {
        private readonly BossDefinition _def = TestSupport.LoadRealDefinition();

        private BossStateMachine Create(ScriptedBrain brain)
        {
            _def.Tuning.IntroDuration = 0;
            _def.Tuning.IdleBetweenActions = 0.5f;
            return _def.CreateStateMachine(brain);
        }

        [Fact]
        public void ComboChainsPunchesWithoutReturningToIdle()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => { var a = BossAction.Perform(ctx.BasicAttack); a.ComboExtra = 2; return a; });
            var m = Create(brain);
            var windups = 0;
            var states = new List<BossState>();
            m.AttackStageChanged += (a, s) => { if (s == AttackStage.Windup) windups++; };
            m.StateChanged += (a, b) => states.Add(b);

            m.Tick(0.5f, 1f);
            for (int i = 0; i < 120; i++) m.Tick(1f / 60f, 1f);

            Assert.Equal(3, windups);
            Assert.Equal(new[] { BossState.Idle, BossState.Attack, BossState.Attack, BossState.Attack, BossState.Idle },
                         states.Take(5).ToArray());
        }

        [Fact]
        public void StunCancelsTheRestOfTheCombo()
        {
            var brain = new ScriptedBrain();
            brain.Script.Enqueue(ctx => { var a = BossAction.Perform(ctx.BasicAttack); a.ComboExtra = 2; return a; });
            var m = Create(brain);
            m.Tick(0.5f, 1f);
            for (int i = 0; i < _def.Tuning.StunHitThreshold; i++) m.ReceiveHit(1);
            Assert.Equal(BossState.Stun, m.State);
            m.Tick(_def.Tuning.StunDuration + 0.01f, 1f);
            Assert.Equal(BossState.Idle, m.State);
            Assert.Null(m.CurrentAttack);
        }

        [Fact]
        public void FarAwayTheBrainLeapsOrWalksAndNeverChainsSpecials()
        {
            var brain = new DefaultBossBrain(new Random(3));
            var tuning = new BossTuning { SpecialChance = 0f, MaxCombo = 3 };
            var kinds = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                var action = brain.Decide(new BrainContext
                {
                    Phase = 1, PlayerOffsetX = 12f, Tuning = tuning,
                    BasicAttack = _def.Attacks.Basic, LeapAttack = _def.Attacks.Leap,
                    PhaseSpecials = _def.Attacks.SpecialsForPhase(1),
                });
                kinds.Add(action.Kind == BossActionKind.Attack ? action.Attack.Id : action.Kind.ToString());
            }
            Assert.Equal(new HashSet<string> { "Brinco", "Move" }, kinds);
        }

        [Fact]
        public void ComboLengthGrowsWithThePhase()
        {
            var brain = new DefaultBossBrain(new Random(5));
            var tuning = new BossTuning { SpecialChance = 0f, MaxCombo = 3 };
            int MaxExtra(int phase) => Enumerable.Range(0, 300).Select(_ => brain.Decide(new BrainContext
            {
                Phase = phase, PlayerOffsetX = 1f, Tuning = tuning, BasicAttack = _def.Attacks.Basic,
                PhaseSpecials = _def.Attacks.SpecialsForPhase(phase),
            }).ComboExtra).Max();
            Assert.Equal(1, MaxExtra(1));
            Assert.Equal(2, MaxExtra(2));
            Assert.Equal(2, MaxExtra(3));
        }

        [Fact]
        public void SpecialsUseTheirOwnPlaceholderClips()
        {
            var r = _def.CreateAnimationResolver(PlaceholderPolicy.PreferAvailable);
            var pisoton = _def.Attacks.Get("PisotonSimi");
            var slam = r.Resolve(BossState.Attack, pisoton);
            Assert.Equal("Spawn", slam.Clip);
            Assert.Equal(6f / (pisoton.Windup + pisoton.Active), slam.Fps, 3);

            var dash = r.Resolve(BossState.Attack, _def.Attacks.Get("CarreraPasillo"));
            Assert.Equal("Walk", dash.Clip);
            Assert.True(dash.Fps > _def.Manifest.Animations["Walk"].Fps);

            Assert.Equal("Attack", r.Resolve(BossState.Attack, _def.Attacks.Get("SaludoCargado")).Clip);
            Assert.Equal("Spawn", r.Resolve(BossState.Attack, _def.Attacks.Leap).Clip);
            Assert.Equal("Attack", _def.CreateAnimationResolver(PlaceholderPolicy.IdleAndAttackOnly).Resolve(BossState.Attack, pisoton).Clip);
        }
    }
}
