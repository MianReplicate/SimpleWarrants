using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SimpleWarrants
{

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class HotSwappableAttribute : Attribute { }

	[HotSwappable]
	[StaticConstructorOnStartup]
	public class MainTabWindow_Warrants : MainTabWindow
	{
		private readonly List<TabRecord> tabs = new List<TabRecord>();
		private string buffCurCapturePayment;
		private string buffCurDeathPayment;
		private string buffCurReward;

		private bool capturePaymentEnabled;
		private Pawn curAnimal;
		private Thing curArtifact;
		private int curCapturePayment;
		private int curDeathPayment;
		private string curMessage = null;
		private Pawn curPawn;
		private string curReason = null;
		private int curReward;
		private WarrantsTab curTab;
		private TargetType curType;
		private bool deathPaymentEnabled;
		private Vector2 scrollPosition;
		private Warrant selectedWarrant;
		private float UI_WIDTH => 1280f;
		private float UI_HEIGHT => 700f;

		public override Vector2 InitialSize => new Vector2(UI_WIDTH, UI_HEIGHT);

		public override void PreOpen()
		{
			base.PreOpen();
			selectedWarrant = null;
			tabs.Clear();
			tabs.Add(new TabRecord("SW.PublicWarrants".Translate(), delegate
			{
				curTab = WarrantsTab.PublicWarrants;
			}, () => curTab == WarrantsTab.PublicWarrants));
			tabs.Add(new TabRecord("SW.RelatedWarrants".Translate(), delegate
			{
				curTab = WarrantsTab.RelatedWarrants;
			}, () => curTab == WarrantsTab.RelatedWarrants));
			tabs.Add(new TabRecord("SW.CreateWarrant".Translate(), delegate
			{
				curTab = WarrantsTab.CreateWarrant;
			}, () => curTab == WarrantsTab.CreateWarrant));
		}

		public override void DoWindowContents(Rect rect)
		{
			Rect mainRect = rect;
			mainRect.yMin += 45f;
			TabDrawer.DrawTabs(mainRect, tabs);
			switch (curTab)
			{
				case WarrantsTab.PublicWarrants:
				case WarrantsTab.RelatedWarrants:
					{
						Rect leftPanelRect = new Rect(mainRect.x, mainRect.y, 720, mainRect.height);
						if (curTab == WarrantsTab.PublicWarrants)
						{
							DoPublicWarrants(leftPanelRect);
						}
						else
						{
							DoRelatedWarrants(leftPanelRect);
						}
						Rect menuSectionRect = new Rect(leftPanelRect.xMax + 10f, mainRect.y, 530, mainRect.height);
						DoMenuSection(menuSectionRect);
						break;
					}
				case WarrantsTab.CreateWarrant:
					{
						DoWarrantCreation(mainRect);
						break;
					}
			}
		}

		private void DoMenuSection(Rect rect)
		{
			if (selectedWarrant != null)
			{
				var sectionRect = new Rect(rect.x, rect.y, rect.width, rect.height);
				var currentY = sectionRect.y;
				float portraitSize = 150;
				float topSectionHeight = portraitSize;
				var leftColRect = new Rect(sectionRect.x, currentY, portraitSize, topSectionHeight);
				bool showAcceptDeclineButtons = curTab == WarrantsTab.PublicWarrants;
				var pawnWarrant = selectedWarrant as Warrant_Pawn;
				if (pawnWarrant != null)
				{
					Text.Font = GameFont.Medium;
					GUI.color = Color.red;
					Text.Anchor = TextAnchor.UpperCenter;
					if (pawnWarrant.Pawn.RaceProps.Humanlike)
					{
						Widgets.Label(new Rect(leftColRect.x, leftColRect.y, leftColRect.width, 45f), "SW.Wanted".Translate());
					}
					else
					{
						Widgets.Label(new Rect(leftColRect.x, leftColRect.y, leftColRect.width, 45f), "SW.Warrant".Translate());
					}
					leftColRect.y += 30f;

					Text.Font = GameFont.Medium;
					GUI.color = Color.yellow;
					var text = pawnWarrant.reason;
					if (pawnWarrant.Pawn.RaceProps.Humanlike is false)
					{
						text = "SW.HuntReason".Translate();
					}
					var height = Text.CalcHeight(text, leftColRect.width + 30);
					var labelRect = new Rect(leftColRect.x + 20, leftColRect.y, leftColRect.width + 30, height);
					Text.Anchor = TextAnchor.UpperLeft;
					Widgets.Label(labelRect, "SW.WantedFor".Translate(text));
					Text.Font = GameFont.Small;
					GUI.color = Color.white;
					leftColRect.y += height;
				}
				else
				{
					Text.Font = GameFont.Medium;
					GUI.color = Color.red;
					Text.Anchor = TextAnchor.UpperCenter;
					Widgets.Label(new Rect(leftColRect.x, leftColRect.y, leftColRect.width, 45f), "SW.Warrant".Translate());
					Text.Font = GameFont.Small;
					GUI.color = Color.white;
					leftColRect.y += 30f;

					Text.Font = GameFont.Medium;
					GUI.color = Color.yellow;
					if (selectedWarrant is Warrant_Artifact)
					{
						Widgets.Label(new Rect(leftColRect.x, leftColRect.y, leftColRect.width, 32f), "SW.ForRetrieval".Translate());
					}
					else if (selectedWarrant is Warrant_TameAnimal)
					{
						Widgets.Label(new Rect(leftColRect.x, leftColRect.y, leftColRect.width, 32f), "SW.ForTaming".Translate());
					}
					Text.Anchor = TextAnchor.UpperLeft;
					Text.Font = GameFont.Small;
					GUI.color = Color.white;
					leftColRect.y += 30f;
				}

				if (showAcceptDeclineButtons)
				{
					float buttonWidth = (leftColRect.width - 10f) / 2f;
					var acceptButtonRect = new Rect(leftColRect.x, leftColRect.y, buttonWidth, 30f);
					if (Widgets.ButtonText(acceptButtonRect, "Accept".Translate()))
					{
						selectedWarrant.DoAcceptAction();
						selectedWarrant = null;
						return;
					}
					var declineButtonRect = new Rect(acceptButtonRect.xMax + 30f, acceptButtonRect.y, buttonWidth, 30f);
					if (Widgets.ButtonText(declineButtonRect, "SW.Decline".Translate()))
					{
						WarrantsManager.Instance.availableWarrants.Remove(selectedWarrant);
						selectedWarrant = null;
						return;
					}
				}

				var portraitRect = new Rect(leftColRect.xMax + 10f, currentY, portraitSize, portraitSize);
				var portraitSizeVec = new Vector2(portraitSize, portraitSize);
				if (pawnWarrant != null)
				{
					GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawnWarrant.Pawn, portraitSizeVec, Rot4.South, default(Vector3), 1.5f));
				}
				else
				{
					if (selectedWarrant is Warrant_Artifact)
					{
						Widgets.ThingIcon(portraitRect, selectedWarrant.thing);
					}
					else if (selectedWarrant is Warrant_TameAnimal tameAnimal)
					{
						Widgets.ThingIcon(portraitRect, tameAnimal.AnimalRace.race, null, null);
					}
					currentY += 30f + 5f;
				}

				if (pawnWarrant != null && pawnWarrant.Pawn.RaceProps.Humanlike)
				{
					var infoRectX = portraitRect.xMax + 15f;
					var infoRect = new Rect(infoRectX, currentY, sectionRect.xMax - infoRectX, topSectionHeight);
					var infoY = infoRect.y;
					DrawInfoRow(ref infoY, infoRect, "SW.NameLabel", pawnWarrant.Pawn.LabelCap);
					DrawInfoRow(ref infoY, infoRect, "SW.RaceLabel", pawnWarrant.Pawn.genes?.XenotypeLabel ?? pawnWarrant.Pawn.def.label);
					DrawInfoRow(ref infoY, infoRect, "SW.AgeLabel", pawnWarrant.Pawn.ageTracker.AgeBiologicalYears.ToString());
					GUI.color = ColorLibrary.RedReadable;
					DrawInfoRow(ref infoY, infoRect, "SW.AllianceLabel", pawnWarrant.Pawn.Faction != null ? pawnWarrant.Pawn.Faction.Name : "None".Translate().Resolve());
					GUI.color = Color.white;
					currentY += topSectionHeight + 20f;

					Text.Font = GameFont.Medium;
					Widgets.Label(new Rect(sectionRect.x, currentY, sectionRect.width, 30f), "SW.MessageFromFaction".Translate(selectedWarrant.issuer.NameColored));
					currentY += 30f + 5f;

					Text.Font = GameFont.Small;
					string messageText = $"\"{selectedWarrant.message}\"";
					float messageHeight = Text.CalcHeight(messageText, sectionRect.width);
					Widgets.Label(new Rect(sectionRect.x, currentY, sectionRect.width, messageHeight), messageText);
					currentY += messageHeight + 15f;
				}
				else
				{
					currentY += 70;
					if (pawnWarrant != null)
					{
						currentY += 35f;
					}
				}

				Text.Font = GameFont.Medium;
				Widgets.Label(new Rect(sectionRect.x, currentY, sectionRect.width, 30f), "SW.Rewards".Translate());
				currentY += 30f + 5f;
				Text.Font = GameFont.Small;
				if (pawnWarrant != null)
				{
					if (pawnWarrant.rewardForDead > 0)
					{
						var rewardRect = new Rect(sectionRect.x, currentY, sectionRect.width, 25f);
						GUI.DrawTexture(new Rect(rewardRect.x, rewardRect.y + 2.5f, 20f, 20f), Warrant_Pawn.IconDeath);
						Widgets.Label(new Rect(rewardRect.x + 25f, rewardRect.y, rewardRect.width - 25f, rewardRect.height), pawnWarrant.rewardForDead + " " + ThingDefOf.Silver.label);
						currentY += 25f;
					}
					if (pawnWarrant.rewardForLiving > 0)
					{
						var rewardRect = new Rect(sectionRect.x, currentY, sectionRect.width, 25f);
						GUI.DrawTexture(new Rect(rewardRect.x, rewardRect.y + 2.5f, 20f, 20f), Warrant_Pawn.IconCapture);
						Widgets.Label(new Rect(rewardRect.x + 25f, rewardRect.y, rewardRect.width - 25f, rewardRect.height), pawnWarrant.rewardForLiving + " " + ThingDefOf.Silver.label);
						currentY += 25f;
					}
				}
				else if (selectedWarrant is Warrant_Artifact artifact)
				{
					var rewardRect = new Rect(sectionRect.x, currentY, sectionRect.width, 25f);
					GUI.DrawTexture(new Rect(rewardRect.x, rewardRect.y + 2.5f, 20f, 20f), Warrant_Artifact.IconRetrieve);
					Widgets.Label(new Rect(rewardRect.x + 25f, rewardRect.y, rewardRect.width - 25f, rewardRect.height), artifact.reward + " " + ThingDefOf.Silver.label);
					currentY += 25f;
				}
				else if (selectedWarrant is Warrant_TameAnimal tame)
				{
					if (tame.Reward > 0)
					{
						var rewardRect = new Rect(sectionRect.x, currentY, sectionRect.width, 25f);
						GUI.DrawTexture(new Rect(rewardRect.x, rewardRect.y + 2.5f, 20f, 20f), Warrant_Pawn.IconCapture);
						Widgets.Label(new Rect(rewardRect.x + 25f, rewardRect.y, rewardRect.width - 25f, rewardRect.height), tame.Reward + " " + ThingDefOf.Silver.label);
						currentY += 25f;
					}
				}

				currentY += 15f;
				if (pawnWarrant != null && pawnWarrant.Pawn.RaceProps.Humanlike)
				{
					Text.Font = GameFont.Medium;
					Widgets.Label(new Rect(sectionRect.x, currentY, sectionRect.width, 30f), "SW.EstimatedThreatLevel".Translate());
					currentY += 30f + 5f;

					GUI.color = Color.red;
					Text.Font = GameFont.Medium;
					Widgets.Label(new Rect(sectionRect.x, currentY, sectionRect.width, 30f), "SW.ThreatLevelPoints".Translate(pawnWarrant.ThreatPoints));
					GUI.color = Color.white;
					Text.Font = GameFont.Small;
				}
			}
			else
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(rect, "SW.WarrantDetails".Translate());
				Text.Anchor = TextAnchor.UpperLeft;
			}
		}

		private void DrawInfoRow(ref float currentY, Rect containingRect, string labelKey, string value)
		{
			string format = labelKey.Translate();
			string[] parts = format.Split(new[] { "{0}" }, StringSplitOptions.None);
			string labelPart = parts[0];

			int colonIndex = labelPart.LastIndexOf(':');
			string boldText = (colonIndex > -1) ? labelPart.Substring(0, colonIndex) : labelPart;

			var boldStyle = new GUIStyle(Text.CurFontStyle)
			{
				fontStyle = FontStyle.Bold
			};

			float boldWidth = boldStyle.CalcSize(new GUIContent(boldText)).x;

			var boldRect = new Rect(containingRect.x, currentY, boldWidth, Text.LineHeight);
			GUI.Label(boldRect, boldText, boldStyle);

			var valueText = ": " + value;
			var valueRect = new Rect(boldRect.xMax, currentY, containingRect.width - boldWidth, Text.LineHeight);
			Widgets.Label(valueRect, valueText);

			currentY += Text.LineHeight;
		}

		private void DoPublicWarrants(Rect rect)
		{
			var warrants = WarrantsManager.Instance.availableWarrants.Where(x => x.thing?.Faction != Faction.OfPlayer).OrderByDescending(x => x.createdTick).ToList();
			if (selectedWarrant != null && !warrants.Contains(selectedWarrant))
			{
				selectedWarrant = null;
			}
			var posY = rect.y + 10;
			var sectionWidth = rect.width;
			var outRect = new Rect(rect.x, posY, sectionWidth, rect.height - posY);
			var viewRect = new Rect(outRect.x, posY, sectionWidth - 16, warrants.Count * 165);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			if (warrants.Count > 0)
			{
				for (var i = 0; i < warrants.Count; i++)
				{
					var warrantBox = new Rect(rect.x, posY, sectionWidth - 30, 150);
					if (selectedWarrant == warrants[i])
					{
						Widgets.DrawHighlightSelected(warrantBox);
					}
					warrants[i].Draw(warrantBox);
					if (Widgets.ButtonInvisible(warrantBox))
					{
						selectedWarrant = warrants[i];
					}
					posY = warrantBox.yMax + 15;
				}
			}
			else
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Medium;
				Widgets.Label(outRect, "SW.NoPublicWarrantsAvailable".Translate());
				Text.Anchor = TextAnchor.UpperLeft;
				Text.Font = GameFont.Small;
			}
			Widgets.EndScrollView();
		}

		private void DoRelatedWarrants(Rect rect)
		{
			var warrants = WarrantsManager.Instance.acceptedWarrants.Concat(WarrantsManager.Instance.createdWarrants).Concat(WarrantsManager.Instance.takenWarrants)
				.Concat(WarrantsManager.Instance.availableWarrants.Where(x => x.thing?.Faction == Faction.OfPlayer)).OrderByDescending(x => x.createdTick).ToList();
			if (selectedWarrant != null && !warrants.Contains(selectedWarrant))
			{
				selectedWarrant = null;
			}
			var posY = rect.y + 10;
			var sectionWidth = rect.width;
			var outRect = new Rect(rect.x, posY, sectionWidth, rect.height - posY);
			var viewRect = new Rect(outRect.x, posY, sectionWidth - 16, warrants.Count * 165);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			if (warrants.Count > 0)
			{
				for (var i = 0; i < warrants.Count; i++)
				{
					var warrantBox = new Rect(rect.x, posY, sectionWidth - 30, 150);
					if (selectedWarrant == warrants[i])
					{
						Widgets.DrawHighlightSelected(warrantBox);
					}
					warrants[i].Draw(warrantBox, false, true);
					if (Widgets.ButtonInvisible(warrantBox))
					{
						selectedWarrant = warrants[i];
					}
					posY = warrantBox.yMax + 15;
				}
			}
			else
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Medium;
				Widgets.Label(outRect, "SW.NoRelatedWarrantsAvailable".Translate());
				Text.Anchor = TextAnchor.UpperLeft;
				Text.Font = GameFont.Small;
			}
			Widgets.EndScrollView();
		}

		private void DoWarrantCreation(Rect rect)
		{
			var posY = rect.y + 10;
			var createWarrant = new Rect(rect.x, posY, 200, 30);

			if (Widgets.ButtonText(createWarrant, "SW.CreateWarrant".Translate()))
			{
				var warrant = CreateWarrant(out string failReason);

				if (warrant is Warrant_Pawn warrantPawn && warrantPawn.Pawn.Faction is not null
					&& warrantPawn.Pawn.Faction != Faction.OfPlayer && warrantPawn.Pawn.Faction.HostileTo(Faction.OfPlayer) is false)
				{
					Find.WindowStack.Add(new Dialog_MessageBox("SW.ConfirmationPrompt".Translate(warrantPawn.Pawn.Named("PAWN"),
						warrantPawn.Pawn.Faction.Name), "Confirm".Translate(), delegate
						{
							TryAddWarrant(warrant, failReason);
						}, "Cancel".Translate()));
				}
				else
				{
					TryAddWarrant(warrant, failReason);
				}
			}

			Text.Font = GameFont.Medium;
			var warrantSubject = new Rect(createWarrant.x, createWarrant.yMax + 20, createWarrant.width, createWarrant.height);
			Widgets.Label(warrantSubject, "SW.WarrantSubject".Translate());

			var dropdownRect = new Rect(createWarrant.x, warrantSubject.yMax, createWarrant.width, createWarrant.height);
			if (Widgets.ButtonTextSubtle(dropdownRect, GetLabel(curType)))
			{
				var floatList = new List<FloatMenuOption>();
				foreach (var value in Enum.GetValues(typeof(TargetType)).Cast<TargetType>())
				{
					floatList.Add(new FloatMenuOption(GetLabel(value), delegate
					{
						curType = value;
					}));
				}
				Find.WindowStack.Add(new FloatMenu(floatList));
			}

			if (curType == TargetType.Human || curType == TargetType.Animal)
			{
				if (curType == TargetType.Human && curPawn is null)
				{
					if (!Find.WorldPawns.AllPawnsAlive.Where(pawn => pawn?.story != null && pawn.RaceProps.Humanlike
						&& !WarrantsManager.Instance.createdWarrants.Any(warrant => pawn == warrant.thing)).TryRandomElement(out curPawn))
					{
						var randomKind = DefDatabase<PawnKindDef>.AllDefs.Where(x => x.RaceProps.Humanlike).RandomElement();
						Faction faction = null;
						if (randomKind.defaultFactionDef != null)
						{
							faction = Find.FactionManager.FirstFactionOfDef(randomKind.defaultFactionDef);
						}
						faction ??= Find.FactionManager.AllFactions.Where(x => x.def.humanlikeFaction && !x.defeated && !x.IsPlayer && !x.Hidden).RandomElement();
						curPawn = PawnGenerator.GeneratePawn(randomKind, faction);
					}
				}
				else if (curType == TargetType.Animal && curAnimal is null)
				{
					curAnimal = PawnGenerator.GeneratePawn(Utils.AllWorthAnimalDefs.RandomElement());
				}

				DrawPawnWarrant(curType == TargetType.Human ? curPawn : curAnimal, createWarrant, dropdownRect);
			}
			else
			{
				if (curArtifact is null)
				{
					var artifactDef = Utils.AllArtifactDefs.RandomElement();
					curArtifact = ThingMaker.MakeThing(artifactDef);
				}

				var thingRect = new Rect(new Vector2(createWarrant.x + 40, dropdownRect.yMax + 10), new Vector2(100 * 0.722f, 100 * 0.722f));
				Widgets.ThingIcon(thingRect, curArtifact);
				Widgets.InfoCardButton(thingRect.xMax, thingRect.y, curArtifact);

				var nameRect = new Rect(createWarrant.x, thingRect.yMax, createWarrant.width, createWarrant.height);
				Widgets.Label(nameRect, curArtifact.LabelCap);

				dropdownRect = new Rect(createWarrant.x, nameRect.yMax, createWarrant.width, createWarrant.height);
				if (Widgets.ButtonTextSubtle(dropdownRect, "SW.Select".Translate()))
				{
					var selectArtifact = new Dialog_SelectArtifact(this);
					Find.WindowStack.Add(selectArtifact);
				}

				Text.Font = GameFont.Small;
				var rewardPayment = new Rect(createWarrant.x, dropdownRect.yMax + 10, 100, 24);
				Widgets.Label(rewardPayment, "SW.RewardPayment".Translate());
				var rewardPaymentInput = new Rect(rewardPayment.xMax, rewardPayment.y, 100, 24);
				Widgets.TextFieldNumeric(rewardPaymentInput, ref curReward, ref buffCurReward);
			}
			Text.Font = GameFont.Small;
		}

		private void TryAddWarrant(Warrant warrant, string failReason)
		{
			if (!failReason.NullOrEmpty())
			{
				Find.WindowStack.Add(new Dialog_MessageBox(failReason));
			}
			else
			{
				warrant.OnCreate();
				WarrantsManager.Instance.createdWarrants.Add(warrant);
			}
			curMessage = "";
		}

		private void DrawPawnWarrant(Pawn pawn, Rect createWarrant, Rect dropdownRect)
		{
			var pawnRect = new Rect(new Vector2(createWarrant.x + 40, dropdownRect.yMax + 10), new Vector2(100 * 0.722f, 100));
			Vector2 pos = new Vector2(pawnRect.width, pawnRect.height);
			GUI.DrawTexture(pawnRect, PortraitsCache.Get(pawn, pos, Rot4.South, new Vector3(0f, 0f, 0f), 1.2f));
			Widgets.InfoCardButton(pawnRect.xMax, pawnRect.y, pawn);

			var nameRect = new Rect(createWarrant.x, pawnRect.yMax, createWarrant.width, createWarrant.height);
			if (curType == TargetType.Human)
			{
				Widgets.Label(nameRect, pawn.Name.ToString());
			}
			else
			{
				Widgets.Label(nameRect, pawn.def.LabelCap);
			}

			dropdownRect = new Rect(createWarrant.x, nameRect.yMax, createWarrant.width, createWarrant.height);
			if (Widgets.ButtonTextSubtle(dropdownRect, "SW.Select".Translate()))
			{
				if (curType == TargetType.Human)
				{
					Find.WindowStack.Add(new Dialog_SelectPawn(this));
				}
				else
				{
					Find.WindowStack.Add(new Dialog_SelectAnimal(this));
				}
			}

			var reasonRect = new Rect(dropdownRect.x, dropdownRect.yMax + 10, 60, createWarrant.height);
			if (curType == TargetType.Human)
			{
				if (curReason is null)
				{
					curReason = Utils.GenerateTextFromRule(SW_DefOf.SW_WantedFor, pawn.thingIDNumber);
				}
				Widgets.Label(reasonRect, "SW.Reason".Translate());
				var reasonAreaRect = new Rect(reasonRect.xMax, reasonRect.y, 130, 24);
				curReason = Widgets.TextArea(reasonAreaRect, curReason);
			}


			Text.Font = GameFont.Small;
			var capturePayment = new Rect(reasonRect.x, reasonRect.yMax + 10, 120, 24);

			Widgets.Label(capturePayment, "SW.CapturePayment".Translate());
			var capturePaymentInput = new Rect(capturePayment.xMax, capturePayment.y, 60, 24);
			if (capturePaymentEnabled)
			{
				Widgets.TextFieldNumeric(capturePaymentInput, ref curCapturePayment, ref buffCurCapturePayment);
			}
			Widgets.Checkbox(capturePaymentInput.xMax + 5, capturePaymentInput.y, ref capturePaymentEnabled);

			var deathPayment = new Rect(capturePayment.x, capturePayment.yMax + 5, capturePayment.width, capturePayment.height);
			Widgets.Label(deathPayment, "SW.DeathPayment".Translate());
			var deathPaymentInput = new Rect(deathPayment.xMax, deathPayment.y, 60, 24);
			if (deathPaymentEnabled)
			{
				Widgets.TextFieldNumeric(deathPaymentInput, ref curDeathPayment, ref buffCurDeathPayment);
			}
			Widgets.Checkbox(deathPaymentInput.xMax + 5, deathPaymentInput.y, ref deathPaymentEnabled);
			if (curType == TargetType.Human)
			{
				var messageRect = new Rect(deathPayment.x, deathPayment.yMax + 5, 120, 24);
				Widgets.Label(messageRect, "SW.Message".Translate());
				var messageAreaRect = new Rect(messageRect.x, messageRect.yMax, 210, 80);
				curMessage = Widgets.TextArea(messageAreaRect, curMessage);
			}
		}

		public void AssignPawn(Pawn pawn)
		{
			curPawn = pawn;
		}

		public void AssignArtifact(Thing artifact)
		{
			curArtifact = artifact;
		}

		public void AssignAnimal(Pawn animal)
		{
			curAnimal = animal;
		}

		private Warrant CreateWarrant(out string failReason)
		{
			failReason = "";

			const int MAX_WARRANT_COUNT = 10;

			if (WarrantsManager.Instance.createdWarrants.Count >= MAX_WARRANT_COUNT)
			{
				failReason = "SW.TooManyPlayerWarrants".Translate(MAX_WARRANT_COUNT);
				return null;
			}

			switch (curType)
			{
				case TargetType.Human:
				case TargetType.Animal:
					return CreatePawnWarrant(ref failReason);
				case TargetType.Artifact: return CreateArtifactWarrant(ref failReason);
			}
			return null;
		}

		private Warrant CreateArtifactWarrant(ref string failReason)
		{
			var warrant = new Warrant_Artifact
			{
				loadID = WarrantsManager.Instance.GetWarrantID(),
				issuer = Faction.OfPlayer,
				createdTick = Find.TickManager.TicksGame
			};
			warrant.thing = curArtifact;
			warrant.reward = curReward;
			warrant.message = Utils.GenerateTextFromRule(SW_DefOf.SW_Messages);
			if (curReward <= 0)
			{
				failReason = "SW.YouMustFillAmountForReward".Translate();
			}
			else
			{
				curArtifact = null;
				curMessage = null;
			}
			return warrant;
		}

		private Warrant CreatePawnWarrant(ref string failReason)
		{
			var warrant = new Warrant_Pawn
			{
				loadID = WarrantsManager.Instance.GetWarrantID(),
				issuer = Faction.OfPlayer,
				createdTick = Find.TickManager.TicksGame
			};

			warrant.thing = curType == TargetType.Human ? curPawn : curAnimal;

			warrant.rewardForLiving = curCapturePayment;
			warrant.rewardForDead = curDeathPayment;
			warrant.reason = curReason;
			if (curMessage.NullOrEmpty() is false)
			{
				warrant.message = curMessage;
			}
			else
			{
				warrant.message = Utils.GenerateTextFromRule(SW_DefOf.SW_Messages);
			}
			curReason = null;
			if (deathPaymentEnabled && curDeathPayment <= 0)
			{
				failReason = "SW.YouMustFillAmountForDeadReward".Translate();
			}
			else if (capturePaymentEnabled && curCapturePayment <= 0)
			{
				failReason = "SW.YouMustFillAmountForCaptureReward".Translate();
			}
			if (failReason.NullOrEmpty())
			{
				if (curType == TargetType.Human)
				{
					curPawn = null;
				}
				else
				{
					curAnimal = null;
				}
			}
			return warrant;
		}

		public string GetLabel(TargetType targetType)
		{
			switch (targetType)
			{
				case TargetType.Human: return "SW.Pawn".Translate();
				case TargetType.Artifact: return "SW.Artifact".Translate();
				case TargetType.Animal: return "SW.Animal".Translate();
			}
			return null;
		}
	}
}
