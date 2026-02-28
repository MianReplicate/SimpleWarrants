using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace SimpleWarrants
{
    [HotSwappable]
    [StaticConstructorOnStartup]
    public class Warrant_Pawn : Warrant
    {
        public static readonly Texture2D IconCapture;
        public static readonly Texture2D IconDeath;

        public Pawn Pawn
        {
            get
            {
                if (thing is Corpse corpse)
                {
                    return corpse.InnerPawn;
                }
                return thing as Pawn;
            }
        }

        public float ThreatPoints
        {
            get
            {
                if (threatPoints <= 0)
                {
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(Find.World);
                }
                return threatPoints;
            }
        }

        public string reason;

        public int rewardForDead;

        public int rewardForLiving;

        public float threatPoints;

        static Warrant_Pawn()
        {
            IconDeath = ContentFinder<Texture2D>.Get("UI/Warrants/IconDeath");
            IconCapture = ContentFinder<Texture2D>.Get("UI/Warrants/IconCapture");
        }

        public override void Draw(Rect rect, bool doAcceptAndDeclineButtons = true, bool doCompensateWarrantButton = false)
        {
            base.Draw(rect, doAcceptAndDeclineButtons, doCompensateWarrantButton);
            var pawnRect = new Rect(new Vector2(rect.x + 100, rect.y + 10), new Vector2(rect.height * 0.7f, rect.height));
            Vector2 pos = new Vector2(pawnRect.width, pawnRect.height);
            var portraitColor = Pawn.RaceProps.Animal ? Pawn.def.uiIconColor : Color.white;
            var portrait = Pawn.RaceProps.Animal ? (Texture)Pawn.def.uiIcon : PortraitsCache.Get(Pawn, pos, Rot4.South, new Vector3(0f, 0f, 0f), 1.2f);
            GUI.color = portraitColor;
            GUI.DrawTexture(pawnRect, portrait, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            Widgets.InfoCardButton(pawnRect.xMax - 24, pawnRect.yMax - 40, Pawn);

            Text.Font = GameFont.Medium;
            var pawnName = Pawn.RaceProps.Animal ? $"<color=#f0898e>{"SW.Hunt".Translate()}</color> {Pawn.def.LabelCap}" : Pawn.Name.ToString();
            var textSize = Text.CalcSize(pawnName);
            var nameInfoBox = new Rect(pawnRect.xMax, pawnRect.y, textSize.x, 30);
            Widgets.Label(nameInfoBox, pawnName);

            if (issuer.IsPlayer && MaxRewardValue() < (Pawn.MarketValue * 0.75f))
            {
                var insufficientRewardBox = new Rect(nameInfoBox.xMax + 5, nameInfoBox.y + 3, 24, 24);
                GUI.DrawTexture(insufficientRewardBox, InsufficientRewardIcon);
                TooltipHandler.TipRegion(insufficientRewardBox, "SW.InsufficientReward".Translate());
            }

            var wantedForInfoBox = new Rect(nameInfoBox.x, nameInfoBox.yMax, rect.width - pawnRect.width, nameInfoBox.height);
            if (!Pawn.RaceProps.Animal)
            {
                Widgets.Label(wantedForInfoBox, "SW.WantedFor".Translate(reason.Colorize(Color.yellow), issuer.NameColored));
            }
            else
            {
                Widgets.Label(wantedForInfoBox, "SW.PostedBy".Translate(issuer.NameColored));
            }

            var rewardsForDeadIconBox = new Rect(wantedForInfoBox.x, wantedForInfoBox.yMax, 24, 24);
            GUI.DrawTexture(rewardsForDeadIconBox, IconDeath);

            var rewardsForDeadInfoBox = new Rect(rewardsForDeadIconBox.xMax + 5, wantedForInfoBox.yMax, wantedForInfoBox.width / 3, wantedForInfoBox.height);
            if (rewardForDead > 0)
            {
                Widgets.Label(rewardsForDeadInfoBox, rewardForDead + " " + ThingDefOf.Silver.LabelCap);
            }
            else
            {
                Widgets.Label(rewardsForDeadInfoBox, "SW.NoReward".Translate());
            }

            var rewardsForLivingIconBox = new Rect(rewardsForDeadInfoBox.xMax, wantedForInfoBox.yMax, 24, 24);
            var rewardsForLivingInfoBox = new Rect(rewardsForLivingIconBox.xMax + 5, wantedForInfoBox.yMax, wantedForInfoBox.width / 3, wantedForInfoBox.height);

            if (!Pawn.RaceProps.Animal || issuer.IsPlayer)
            {
                GUI.DrawTexture(rewardsForLivingIconBox, IconCapture);
                Widgets.Label(rewardsForLivingInfoBox, rewardForLiving + " " + ThingDefOf.Silver.LabelCap);
            }

            var infoBox = new Rect(rect.width - 250, rewardsForLivingInfoBox.yMax + 30, 250, 24);
            Text.Font = GameFont.Tiny;
            if (issuer != Faction.OfPlayer)
            {
                var expireDate = (relatedQuest != null ? acceptedTick : createdTick) + (GenDate.TicksPerDay * 15) - Find.TickManager.TicksGame;
                Widgets.Label(infoBox, "SW.WillExpireIn".Translate(expireDate.ToStringTicksToDays()));
            }
            else
            {
                if (accepteer != null)
                {
                    Widgets.Label(infoBox, "SW.ApproximateComplectionDate".Translate(ApproximateCompletionDate.ToStringTicksToDays()));
                }
                else
                {
                    Widgets.Label(infoBox, "SW.ApproximateAcceptionDate".Translate(ApproximateAcceptionDate.ToStringTicksToDays()));
                }
            }
            Text.Font = GameFont.Small;
        }

        public override void DoAcceptAction()
        {
            base.DoAcceptAction();
            Slate slate = new Slate();
            slate.Set("points", ThreatPoints);
            slate.Set("asker", issuer.leader);
            slate.Set("victim", Pawn);
            slate.Set("reason", reason);
            slate.Set("warrant", this);
            slate.Set("rewardForLiving", rewardForLiving);
            slate.Set("rewardForDead", rewardForDead);
            var questDef = Pawn.RaceProps.Animal ? SW_DefOf.SW_Warrant_Animal : SW_DefOf.SW_Warrant_Pawn;
            var quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            QuestUtility.SendLetterQuestAvailable(quest);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref rewardForLiving, "rewardForLiving");
            Scribe_Values.Look(ref rewardForDead, "rewardForDead");
            Scribe_Values.Look(ref threatPoints, "threatPoints");
        }

        public override bool IsWarrantActive()
        {
            bool isPawnDead = Pawn?.Dead ?? false;
            if (rewardForDead == 0 && isPawnDead)
            {
                return false;
            }

            if (Pawn?.Corpse is { } corpse)
            {
                thing = corpse;

                if (rewardForDead > 0 && corpse.ParentHolder is null && !corpse.Spawned)
                {
                    return false;
                }
            }
            else if (Pawn == null || Pawn.Destroyed)
            {
                return false;
            }
            return true;
        }

        public override void GiveReward(Caravan caravan, Thing thingHandedIn)
        {
            base.GiveReward(caravan, thingHandedIn);

            bool isPawnDead = thingHandedIn is Corpse or Pawn { Dead: true };
            var rewardAmount = isPawnDead ? rewardForDead : rewardForLiving;

            if (rewardAmount <= 0)
                return;

            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = rewardAmount;
            GiveThing(caravan, silver);
        }

        public override void DoCompensateAction()
        {
            var silvers = Utils.AllPlayerSilver();
            var toCompensate = Mathf.Max(rewardForDead, rewardForLiving);
            if (silvers.Sum(x => x.stackCount) >= toCompensate)
            {
                Pay(silvers, toCompensate);
                WarrantsManager.Instance.availableWarrants.Remove(this);
            }
            else
            {
                Messages.Message("SW.NoEnoughMoneyToCompensate".Translate(toCompensate), MessageTypeDefOf.CautionInput);
            }
        }

        public override float AcceptChance()
        {
            if (!acceptChanceCached.HasValue)
            {
                var reward = Mathf.Max(rewardForDead, rewardForLiving);
                acceptChanceCached = reward / thing.MarketValue;
            }
            return acceptChanceCached.Value;
        }

        public override float SuccessChance()
        {
            if (!successChanceCached.HasValue)
            {
                var reward = Mathf.Max(rewardForDead, rewardForLiving);
                successChanceCached = reward / thing.MarketValue;
            }
            return successChanceCached.Value;
        }

        public override bool ShouldShowCompensateButton()
        {
            return issuer != Faction.OfPlayer && accepteer != Faction.OfPlayer;
        }

        public override bool IsThreatForPlayer()
        {
            return Pawn.Faction == Faction.OfPlayer;
        }

        public override void OnCreate()
        {
            base.OnCreate();

            if (Pawn.Faction != null && !Pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                Pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -80);
            }
        }

        public override int MaxRewardValue()
        {
            return Mathf.Max(rewardForDead, rewardForLiving);
        }
    }
}
