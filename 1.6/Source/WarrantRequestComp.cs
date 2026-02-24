using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SimpleWarrants
{
	public class WorldObjectCompProperties_WarrantRequest : WorldObjectCompProperties
	{
		public WorldObjectCompProperties_WarrantRequest()
		{
			compClass = typeof(WarrantRequestComp);
		}
	}

	[StaticConstructorOnStartup]
	public class WarrantRequestComp : WorldObjectComp
	{
		private static readonly Texture2D TradeCommandTex;

		static WarrantRequestComp()
		{
			TradeCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/FulfillTradeRequest");
		}

		public bool ActiveRequest => ActiveWarrants.Any();
		public IEnumerable<Warrant> ActiveWarrants => WarrantsManager.Instance.acceptedWarrants?.Where(x => x.issuer == parent?.Faction && x.IsWarrantActive()) ?? Array.Empty<Warrant>();

		public override string CompInspectStringExtra()
		{
			if (!ActiveRequest)
				return null;

			var requestInfo = string.Join(", ", ActiveWarrants.Select(x => x is Warrant_TameAnimal tame ? (string)tame.AnimalRace.LabelCap : x.thing.LabelCap ?? x.thing.def.label));
			return "SW.CaravanRequestInfo".Translate(requestInfo);
		}

		public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
		{
			if (ActiveRequest && CaravanVisitUtility.SettlementVisitedNow(caravan) == parent)
			{
				yield return FulfillRequestCommand(caravan);
			}
		}

		private Command FulfillRequestCommand(Caravan caravan)
		{
			Command_Action command_Action = new Command_Action();
			command_Action.defaultLabel = "SW.CommandFulfillWarrant".Translate();
			command_Action.defaultDesc = "SW.CommandFulfillWarrantDesc".Translate();
			command_Action.icon = TradeCommandTex;
			command_Action.action = delegate
			{
				Fulfill(caravan);
			};
			if (ActiveWarrants.All(x => TryGetWarrantTargetInCaravan(x, caravan) == null))
			{
				command_Action.Disable("SW.CommandFulfillWarrantFailInsufficient".Translate(ActiveWarrants.Select(x => x is Warrant_TameAnimal t ? (string)t.AnimalRace.LabelCap : x.thing.LabelCap).First()));
			}
			return command_Action;
		}

		private void Fulfill(Caravan caravan)
		{
			foreach (var warrant in ActiveWarrants.ToList())
			{
				var target = TryGetWarrantTargetInCaravan(warrant, caravan);
				if (target == null)
					continue;

				target.holdingOwner.Remove(target);
				warrant.GiveReward(caravan, target);
				var questTarget = target is Corpse corpse ? corpse.InnerPawn : target;
				QuestUtility.SendQuestTargetSignals(questTarget.questTags, "WarrantRequestFulfilled", parent.Named("SUBJECT"), caravan.Named("CARAVAN"));

				// Force quest to end. Only necessary with animal quests because reasons.
				if (warrant.relatedQuest is { State: <= QuestState.Ongoing })
					warrant.relatedQuest.End(QuestEndOutcome.Success);

				WarrantsManager.Instance.acceptedWarrants.Remove(warrant);
				target.Destroy();
				Messages.Message("Warrant completed. Your caravan has received the payment.", MessageTypeDefOf.PositiveEvent, false);
			}
		}

		/**
         * Separated method to get all potential targets to allow other modders to easily patch this method to add other potential sources.
         * For example allowing pawns to be taken from pawn storage mods or potentially remote locations rather than directly in the caravan.
         */
		protected virtual IEnumerable<Thing> GetPotentialTargetsInCaravan(Warrant warrant, Caravan caravan)
		{
			return CaravanInventoryUtility.AllInventoryItems(caravan).Concat(caravan.PawnsListForReading);
		}

		private Thing TryGetWarrantTargetInCaravan(Warrant warrant, Caravan caravan)
		{
			try
			{
				var tame = warrant as Warrant_TameAnimal;

				foreach (var thing in GetPotentialTargetsInCaravan(warrant, caravan))
				{
					// Tame warrant requires any pawn of the required type.
					if (tame != null && thing is Pawn p && p.RaceProps.Animal && p.kindDef == tame.AnimalRace && p.health != null)
					{
						// Check tameness.
						bool isTame = p.training?.HasLearned(TrainableDefOf.Tameness) ?? false;

						// Check health.
						float healthPct = p.health.summaryHealth.SummaryHealthPercent;

						if (isTame && healthPct >= 0.9f)
							return thing;
					}

					// Corpse for animal-pawn warrant.
					if (warrant is Warrant_Pawn pw && pw.Pawn.RaceProps.Animal)
					{
						if (thing is Pawn p2 && p2.kindDef == pw.Pawn.kindDef && p2.Dead)
							return thing;

						if (thing is Corpse c && c.InnerPawn?.kindDef == pw.Pawn.kindDef)
							return thing;
					}

					// Corpse for pawn warrant.
					if (warrant.thing is Pawn pawn && thing is Corpse corpse && corpse.InnerPawn == pawn)
					{
						return thing;
					}

					// Living pawn for pawn warrant.
					if (thing == warrant.thing)
					{
						return thing;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Error in TryGetWarrantTargetInCaravan: " + ex);
			}
			return null;
		}
	}
}
