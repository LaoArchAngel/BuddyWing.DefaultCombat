// Copyright (C) 2011-2016 Bossland GmbH
// See the file LICENSE for the source code's detailed license

using System.Linq;
using Buddy.BehaviorTree;
using Buddy.CommonBot;
using DefaultCombat.Core;
using DefaultCombat.Helpers;
using Targeting = DefaultCombat.Core.Targeting;

namespace DefaultCombat.Routines
{
	internal class KeneticCombat : RotationBase
	{
	    private const string TelekineticThrow = "Telekinetic Throw";
	    private const string CascadingDebris = "Cascading Debris";
	    private const string SlowTime = "Slow Time";
	    private const string ForceBreach = "Force Breach";
	    private const string WhirlingBlow = "Whirling Blow";
	    private const string Project = "Project";
	    private const string HarnessedShadows = "Harnessed Shadows";
	    private const string SaberStrike = "Saber Strike";
	    private const string KineticWard = "Kinetic Ward";

	    public override string Name
		{
			get { return "Shadow Kenetic Combat"; }
		}

		public override Composite Buffs
		{
			get
			{
				return new PrioritySelector(
					Spell.Buff("Combat Technique"),
					Spell.Buff("Force Valor"),
					Spell.Cast("Guard", on => Me.Companion,
						ret => Me.Companion != null && !Me.Companion.IsDead && !Me.Companion.HasBuff("Guard")),
					Spell.Buff("Stealth", ret => !Rest.KeepResting() && !DefaultCombat.MovementDisabled)
					);
			}
		}

		public override Composite Cooldowns
		{
		    get
		    {
		        return new PrioritySelector(
		            Spell.Cast(KineticWard, ret => Me.BuffCount(KineticWard) <= 1 || Me.BuffTimeLeft(KineticWard) < 3),
		            Spell.Buff("Force of Will"),
		            Spell.Buff("Battle Readiness", ret => Me.HealthPercent <= 85),
		            Spell.Buff("Deflection", ret => Me.HealthPercent <= 60),
		            Spell.Buff("Resilience", ret => Me.HealthPercent <= 50),
		            Spell.Buff("Force Potency")
		            );
		    }
		}

	    private Decorator UseHarnessedShadows
	    {
	        get
	        {
	            return new Decorator(
	                ctx => HSCount > 2,
	                new PrioritySelector(
	                    Spell.Cast(CascadingDebris, context => Me.Energy >= 30),
	                    Spell.Cast(TelekineticThrow, context => Me.Energy >= 30),
                        Spell.Cast(SaberStrike)
	                    )
	                );
	        }
	    }

	    private Decorator BuildingHarnessedShadows
	    {
	        get
	        {
	            return new Decorator(
	                ret => HSCount < 3 || !CanUseHS,
	                new PrioritySelector(
	                    Spell.Cast(Project),
	                    Spell.Cast(SlowTime)
	                    )
	                );
	        }
	    }

	    private Decorator DownTime
	    {
	        get
	        {
	            return new Decorator(
	                ctx => HSCount < 3 && !CanStackHS,
	                new PrioritySelector(
	                    Spell.Cast(ForceBreach),
	                    StandardAttacks,
	                    Spell.Cast("Spinning Strike", ret => Me.CurrentTarget.HealthPercent <= 30f),
	                    Spell.Cast(SaberStrike)
	                    )
	                );
	        }
	    }

	    private Decorator StandardAttacks
	    {
	        get
	        {
	            return new Decorator(
	                ret => Me.CurrentTarget != null && Me.CurrentTarget.HealthPercent > 30f,
	                new PrioritySelector(
	                    Spell.Cast("Shadow Strike", ret => Me.HasBuff("Shadow Wrap")),
	                    Spell.Cast("Double Strike")
	                    )
	                );
	        }
	    }

	    public override Composite SingleTarget
		{
	        get
	        {
	            return new LockSelector(
	                Spell.Buff("Force Speed",
	                    ret =>
	                        !DefaultCombat.MovementDisabled && Me.CurrentTarget.Distance >= 1f &&
	                        Me.CurrentTarget.Distance <= 3f),

	                //Movement
	                CombatMovement.CloseDistance(Distance.Melee),

	                //Rotation
	                Spell.Cast("Mind Snap", ret => Me.CurrentTarget.IsCasting && !DefaultCombat.MovementDisabled),
	                Spell.Cast("Force Stun", ret => Me.CurrentTarget.IsCasting && !DefaultCombat.MovementDisabled),
	                UseHarnessedShadows,
	                BuildingHarnessedShadows,
	                DownTime,
	                Spell.Cast("Force Speed", ret => Me.CurrentTarget.Distance >= 1.1f && Me.IsMoving && Me.InCombat)
	                );
	        }
		}

	    public override Composite AreaOfEffect
	    {
	        get
	        {
	            return new Decorator(ret => Targeting.ShouldPbaoe,
	                new PrioritySelector(
	                    RefreshDarkProtection,
	                    BuildingHarnessedShadows,
	                    Spell.Cast(ForceBreach),
	                    Spell.Cast(WhirlingBlow),
	                    Spell.Cast(SaberStrike)
	                    ));
	        }
	    }

	    private static Decorator RefreshDarkProtection
	    {
	        get
	        {
	            return new Decorator(
	                ctx => Me.BuffTimeLeft("Dark Protection") < 2 && HSCount == 3,
	                new PrioritySelector(
	                    Spell.Cast(TelekineticThrow),
	                    Spell.Cast(CascadingDebris),
                        Spell.Cast(SaberStrike)));
	        }
	    }

	    private static bool CanStackHS
	    {
	        get
	        {
	            return AbilityManager.CanCast(Project, Me.CurrentTarget) ||
	                   AbilityManager.CanCast(SlowTime, Me.CurrentTarget);
	        }
	    }

	    private static bool CanUseHS
	    {
	        get
	        {
	            return AbilityManager.CanCast(CascadingDebris, Me.CurrentTarget) ||
	                   AbilityManager.CanCast(TelekineticThrow, Me.CurrentTarget);
	        }
	    }

	    private static int HSCount
	    {
	        get
	        {
	            var hsBuff =
	                Me.Buffs.SingleOrDefault(
	                    effect =>
	                        effect.Name.Equals(HarnessedShadows) &&
	                        effect.EffectNumber == 2);

	            return hsBuff == null ? 0 : hsBuff.GetStacks();
	        }
	    }
	}
}