// Copyright (C) 2011-2016 Bossland GmbH
// See the file LICENSE for the source code's detailed license

using Buddy.BehaviorTree;
using Buddy.CommonBot;
using DefaultCombat.Core;
using DefaultCombat.Helpers;
using Targeting = DefaultCombat.Core.Targeting;
using System.Linq;
using Buddy.Swtor;
using Buddy.Swtor.Objects;

namespace DefaultCombat.Routines
{
	internal class Infiltration : RotationBase
	{
	    private const string BreachingShadows = "Breaching Shadows";
	    private const string Clairvoyance = "Clairvoyance";
	    private const string CirclingShadows = "Circling Shadows";
	    private const string WhirlingBlow = "Whirling Blow";
	    private const string LowSlash = "Low Slash";
	    private const string ClairvoyantStrike = "Clairvoyant Strike";
	    private const string SpinningStrike = "Spinning Strike";
	    private const string ForceBreach = "Force Breach";
	    private const string ShadowStrike = "Shadow Strike";
	    private const string Stealth = "Stealth";
	    private const string InfiltrationTactics = "Infiltration Tactics";
	    private const string PsychokineticBlast = "Psychokinetic Blast";
	    private const string SaberStrike = "Saber Strike";
	    private const string ForcePotency = "Force Potency";
	    private const string Blackout = "Blackout";
	    private const string ShadowsRespite = "Shadow's Respite";

	    public override string Name
		{
			get { return "Shadow Infiltration"; }
		}

		public override Composite Buffs
		{
			get
			{
				return new PrioritySelector(
					Spell.Buff("Shadow Technique"),
					Spell.Buff("Force Valor"),
					Spell.Cast("Guard", on => Me.Companion,
						ret => Me.Companion != null && !Me.Companion.IsDead && !Me.Companion.HasBuff("Guard")),
					Spell.Buff(Stealth, ret => !Rest.KeepResting() && !DefaultCombat.MovementDisabled)
					);
			}
		}

		public override Composite Cooldowns
		{
		    get
		    {
		        return new PrioritySelector(
		            Spell.Buff("Force of Will"),
		            Spell.Buff("Battle Readiness", ret => Me.HealthPercent <= 85),
		            Spell.Buff("Deflection", ret => Me.HealthPercent <= 60),
		            Spell.Buff("Resilience", ret => Me.HealthPercent <= 50));
		    }
		}

		public override Composite SingleTarget
		{
		    get
		    {
		        return new LockSelector(
		            Spell.Buff("Force Speed",
		                ret =>
		                    !DefaultCombat.MovementDisabled && Me.CurrentTarget.Distance >= 1.5f &&
		                    Me.CurrentTarget.Distance <= 3f),

		            //Movement
		            CombatMovement.CloseDistance(Distance.Melee),

		            //Interrupts
		            Spell.Cast("Mind Snap", ret => Me.CurrentTarget.IsCasting),
		            //Spell.Cast("Force Stun", ret => Me.CurrentTarget.IsCasting),
		            //Spell.Cast(LowSlash, ret => Me.CurrentTarget.IsCasting),

		            //Rotation
                    //new Action(context =>
                    //{
                    //    Logger.Write(
                    //        Me.KnownAbilitiesContainer.Single(ability => ability.Name == PsychokineticBlast)
                    //            .GlobalCooldownTime.ToString(CultureInfo.InvariantCulture));
                    //    return RunStatus.Failure;
                    //}),
                    DuringGC,
		            UseFB,
		            RefreshClairvoyance,
                    UsePB,
		            new Action(delegate
		            {
		                PBLast = false;
		                return RunStatus.Failure;
		            }),
                    Spell.Cast(Blackout, reqs => !Me.HasBuff(ShadowsRespite) && Me.Force <= 40),
                    FillBreachingShadows,
                    ForceCloakCombo,
                    BuildBreachingShadows,
		            Spell.Cast("Force Speed", ret => Me.CurrentTarget.Distance >= 1.5f && Me.IsMoving && Me.InCombat),
                    Spell.Cast(SaberStrike)
		            );
		    }
		}

		public override Composite AreaOfEffect
		{
			get
			{
				return new PrioritySelector(
					Spell.Cast(WhirlingBlow, ret => Me.ForcePercent >= 60 && Targeting.ShouldPbaoe)
					);
			}
		}

	    private bool PBLast { get; set; }

	    private static bool IsGC
	    {
	        get
	        {
	            TorAbility saberStrike = Me.KnownAbilitiesContainer.Single(ability => ability.Name == SaberStrike);
	            EffectResult isReady = Me.IsAbilityReady(saberStrike, Me.CurrentTarget);

	            return isReady == EffectResult.NotReady;
	        }
	    }

	    private static int BreachingShadowsCount
	    {
	        get { return Me.BuffCount(BreachingShadows); }
	    }

	    private int ClairvoyanceCount
	    {
            get { return Me.BuffCount(Clairvoyance); }
	    }

	    private int CirclingShadowsCount
	    {
            get { return Me.BuffCount(CirclingShadows); }
	    }

	    private Decorator BuildBreachingShadows
	    {
	        get
	        {
	            return new Decorator(ctx => BreachingShadowsCount < 3 || BreachingShadowsDowntime,
	                new PrioritySelector(
                        Spell.Cast(ShadowStrike, ret => Me.HasBuff(InfiltrationTactics)),
	                    Spell.Cast(SpinningStrike, ret => CanExecute),
	                    Spell.Cast(ClairvoyantStrike),
	                    Spell.Cast(LowSlash, reqs => Me.CurrentTarget.Distance > 0.4f),
	                    Spell.Cast(WhirlingBlow,
	                        reqs =>
	                            Me.CurrentTarget.Distance > 0.4f && !AbilityManager.CanCast(LowSlash, Me.CurrentTarget) &&
	                            Me.Force > 45)
	                    ));
	        }
	    }

	    private static Decorator FillBreachingShadows
	    {
	        get
	        {
	            return new Decorator(reqs => BreachingShadowsCount == 0,
	                new PrioritySelector(
                        Spell.Cast(ForcePotency),
	                    Spell.Cast("Shadow Stride")));
	        }
	    }

	    private static TorAbility ForcePotencyAbility
	    {
	        get { return Me.KnownAbilitiesContainer.Single(ability => ability.Name == ForcePotency); }
	    }

	    private static TorAbility BlackoutAbility
	    {
	        get { return Me.KnownAbilitiesContainer.Single(ability => ability.Name == Blackout); }
	    }

	    private Decorator UsePB
	    {
	        get
	        {
	            return
	                new Decorator(
	                    ctx =>
	                        CirclingShadowsCount > 1 && ClairvoyanceCount > 1 && BreachingShadowsCount < 3,
	                    new PrioritySelector(
	                        new Action(delegate
	                        {
	                            PBLast = true;
	                            return RunStatus.Failure;
	                        }),
	                        Spell.Cast(PsychokineticBlast)));
	        }
	    }

	    private Decorator UseFB
	    {
	        get
	        {
	            return new Decorator(ctc => BreachingShadowsCount > 2,
	                new PrioritySelector(
                        Spell.Cast(ForceBreach, ret => !BreachingShadowsDowntime),
	                    Spell.Cast(ForceBreach, ret => PBLast || !CanExecute),
	                    Spell.Cast(SpinningStrike, ret => !PBLast && BreachingShadowsDowntime),
                        Spell.Cast(ForceBreach)
	                    ));
	        }
	    }

	    private static double BreachingShadowsTime
	    {
	        get { return Me.BuffTimeLeft(BreachingShadows); }
	    }

	    private static bool CanExecute
	    {
	        get { return Me.CurrentTarget.HealthPercent < 30; }
	    }

	    private Decorator RefreshClairvoyance
	    {
	        get
	        {
	            return new Decorator(ctx => ClairvoyanceCount == 0 || ClairvoyanceTime < 2,
	                new PrioritySelector(
                        Spell.Cast(ClairvoyantStrike),
                        Spell.Cast(WhirlingBlow)));
	        }
	    }

	    private static double ClairvoyanceTime
	    {
	        get { return Me.BuffTimeLeft(Clairvoyance); }
	    }

	    private Decorator ForceCloakCombo
	    {
	        get
	        {
	            return
	                new Decorator(
	                    reqs =>
	                        !Me.HasBuff(ShadowsRespite) && BreachingShadowsCount == 0 &&
	                        Me.IsAbilityReady(BlackoutAbility, Me) == EffectResult.NotReady &&
	                        Me.IsAbilityReady(ForcePotencyAbility, Me) == EffectResult.NotReady && Me.Force <= 40,
	                    new Action(ret =>
	                    {
                            AbilityManager.Cast("Force Cloak", Me);
	                        AbilityManager.Cast(ForcePotency, Me);
                            AbilityManager.Cast(WhirlingBlow, Me.CurrentTarget);
	                    }));
	        }
	    }

	    private static bool BreachingShadowsDowntime
	    {
	        get { return BreachingShadowsTime > 24; }
	    }

	    private static Decorator DuringGC
	    {
	        get
	        {
	            return new Decorator(reqs => IsGC,
	                new PrioritySelector(
	                    FillBreachingShadows,
	                    Spell.Cast(Blackout, reqs => !Me.HasBuff(ShadowsRespite) && Me.Force < 40),
	                    new Action(ret => RunStatus.Success)));
	        }
	    }
	}
}