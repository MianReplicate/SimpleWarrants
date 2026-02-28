using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using RimWorld.Planet;

namespace SimpleWarrants
{
    public class LordCollection : IExposable
    {
        public List<Lord> lords;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref lords, "lords", LookMode.Reference);
        }
    }
    public class WarrantsManager : GameComponent
    {
        private const int TYPE_ARTIFACT = 0;
        private const int TYPE_ANIMAL = 1;
        private const int TYPE_PAWN = 2;
        private const int TYPE_TAME = 3;

        public static WarrantsManager Instance;
        private static readonly float[] warrantTypeToWeight = new float[4];
        private static readonly int[] warrantTypes = new int[4];

        static WarrantsManager()
        {
            for (int i = 0; i < warrantTypes.Length; i++)
            {
                warrantTypes[i] = i;
            }
        }

        public List<Warrant> acceptedWarrants; // Warrants accepted by the player, to be completed by the player.
        public List<Warrant> availableWarrants; // Available warrants, created by AI.
        public List<Warrant> createdWarrants; // Warrants created by the player, to be completed by AI.
        public List<Warrant> postponedWarrants; // Warrants accepted by the player, payment postponed by the player.

        public bool initialized;
        public int lastWarrantID;
        public Dictionary<Warrant_Pawn, LordCollection> raidLords;
        public List<Warrant> takenWarrants;
        private List<LordCollection> lordValues;

        private List<Warrant_Pawn> warrantKeys;

        public WarrantsManager()
        {
            Instance = this;
        }

        public WarrantsManager(Game game)
        {
            Instance = this;
        }

        public void PreInit()
        {
            Instance = this;
            availableWarrants ??= new List<Warrant>();
            acceptedWarrants ??= new List<Warrant>();
            createdWarrants ??= new List<Warrant>();
            takenWarrants ??= new List<Warrant>();
            postponedWarrants ??= new List<Warrant>();
            raidLords ??= new Dictionary<Warrant_Pawn, LordCollection>();
        }

        public override void StartedNewGame()
        {
            PreInit();
            base.StartedNewGame();
            if (!initialized && !availableWarrants.Any())
            {
                PopulateWarrants(Rand.RangeInclusive(3, 5));
                initialized = true;
            }
        }

        public override void LoadedGame()
        {
            PreInit();
            base.LoadedGame();
            if (!initialized && !availableWarrants.Any())
            {
                PopulateWarrants(Rand.RangeInclusive(3, 5));
                initialized = true;
            }
        }

        public void PopulateWarrants(int amountToPopulate)
        {
            int num = 0;
            var count = 0;
            while (count < amountToPopulate && num < amountToPopulate * 5)
            {
                num++;

                var warrant = GenerateRandomWarrant();
                if (warrant == null || !warrant.CanPlayerReceive())
                    continue;

                availableWarrants.Add(warrant);
                count++;
            }

            if (count < num)
                Log.Warning($"Generated {count}/{amountToPopulate} warrants in {num} tries. Colony wealth may be too low.");
        }

        private Warrant GenerateRandomWarrant()
        {
            warrantTypeToWeight[TYPE_ARTIFACT] = SimpleWarrantsMod.Settings.enableWarrantsOnArtifact ? 0.25f : 0f;
            warrantTypeToWeight[TYPE_PAWN] = 1f;
            warrantTypeToWeight[TYPE_ANIMAL] = SimpleWarrantsMod.Settings.enableWarrantsOnAnimals ? 0.2f : 0f;
            warrantTypeToWeight[TYPE_TAME] = SimpleWarrantsMod.Settings.enableTamingWarrants ? 0.2f : 0f;

            var type = warrantTypes.RandomElementByWeight(t => warrantTypeToWeight[t]);

            switch (type)
            {
                case TYPE_PAWN:
                    #region PAWN WARRANT
                    var pawnWarrant = new Warrant_Pawn
                    {
                        loadID = GetWarrantID(),
                        createdTick = Find.TickManager.TicksGame,
                        thing = PawnGenerator.GeneratePawn(Utils.AllWorthAnimalDefs.RandomElement())
                    };

                    // Generate a random pawn to give a warrant to.
                    // The pawn is made part of a random human-like faction.
                    var randomKind = DefDatabase<PawnKindDef>.AllDefs.Where(x => x.RaceProps.Humanlike && x.defaultFactionDef != Faction.OfPlayer.def).RandomElement();
                    Faction faction = null;

                    if (randomKind.defaultFactionDef != null)
                        faction = Find.FactionManager.FirstFactionOfDef(randomKind.defaultFactionDef);

                    faction ??= Find.FactionManager.AllFactions.Where(x => x.def.humanlikeFaction && !x.defeated && !x.IsPlayer && !x.Hidden).RandomElement();

                    if (!PickWarrantIssuer(Faction.OfPlayer, false,  out pawnWarrant.issuer))
                    {
                        Log.Error("Failed to find a valid faction to issue warrant (non-hostile humanlike w/ fac base).");
                        return null;
                    }

                    pawnWarrant.thing = PawnGenerator.GeneratePawn(randomKind, faction);
                    pawnWarrant.message = Utils.GenerateTextFromRule(SW_DefOf.SW_Messages, pawnWarrant.Pawn.thingIDNumber);
                    pawnWarrant.reason = Utils.GenerateTextFromRule(SW_DefOf.SW_WantedFor, pawnWarrant.Pawn.thingIDNumber);
                    AssignRewards(pawnWarrant);
                    return pawnWarrant;
                    #endregion

                case TYPE_ANIMAL:
                    #region ANIMAL WARRANT
                    var animalWarrant = new Warrant_Pawn
                    {
                        loadID = GetWarrantID(),
                        createdTick = Find.TickManager.TicksGame,
                        thing = PawnGenerator.GeneratePawn(Utils.AllWorthAnimalDefs.RandomElement())
                    };
                    animalWarrant.message = Utils.GenerateTextFromRule(SW_DefOf.SW_Messages, animalWarrant.Pawn.thingIDNumber);

                    if (!PickWarrantIssuer(Faction.OfPlayer, false, out animalWarrant.issuer))
                    {
                        Log.Error("Failed to find a valid faction to issue warrant (non-hostile humanlike w/ fac base).");
                        animalWarrant.thing.Destroy();
                        return null;
                    }

                    AssignRewards(animalWarrant);
                    return animalWarrant;
                    #endregion

                case TYPE_ARTIFACT:
                    #region ARTIFACT WARRANT
                    var artWarrant = new Warrant_Artifact
                    {
                        loadID = GetWarrantID(),
                        createdTick = Find.TickManager.TicksGame
                    };

                    if (!PickWarrantIssuer(Faction.OfPlayer, false, out artWarrant.issuer))
                    {
                        Log.Error("Failed to find a valid faction to issue warrant (non-hostile humanlike w/ fac base).");
                        return null;
                    }

                    var artifacts = Utils.AllArtifactDefs;
                    var randomArtifact = artifacts.RandomElement();
                    artWarrant.thing = ThingMaker.MakeThing(randomArtifact);
                    artWarrant.reward = (int)(artWarrant.thing.MarketValue * Rand.Range(0.5f, 2f));
                    DoWealthScaling(artWarrant);
                    return artWarrant;
                    #endregion

                case TYPE_TAME:
                    var tameWarrant = new Warrant_TameAnimal()
                    {
                        loadID = GetWarrantID(),
                        createdTick = Find.TickManager.TicksGame
                    };

                    if (!PickWarrantIssuer(Faction.OfPlayer, false, out tameWarrant.issuer))
                    {
                        Log.Error("Failed to find a valid faction to issue warrant (non-hostile humanlike w/ fac base).");
                        return null;
                    }

                    var playerHome = Find.AnyPlayerHomeMap;
                    if (playerHome == null)
                        return null;

                    // Get a random animal kind that can spawn in the player map
                    // in the current season, and is tameable.
                    var allAnimals = (from animal in playerHome.Biome.AllWildAnimals
                                      where playerHome.mapTemperature.SeasonAcceptableFor(animal.race) &&
                                            animal.race.GetStatValueAbstract(StatDefOf.Wildness) < 1f
                                      select animal).ToList();

                    if (!allAnimals.TryRandomElementByWeight(a => a.race.GetStatValueAbstract(StatDefOf.MarketValue), out tameWarrant.AnimalRace))
                    {
                        Log.Error($"Failed to find animal type to spawn for tame warrant. There were {allAnimals.Count} candidates.");
                        return null;
                    }

                    float marketValue = tameWarrant.AnimalRace.race.GetStatValueAbstract(StatDefOf.MarketValue);

                    tameWarrant.Reward = (int)(marketValue * Rand.Range(0.7f, 2.5f));
                    DoWealthScaling(tameWarrant);

                    // Cap reward at 350% of animal market value.
                    tameWarrant.Reward = (int)Mathf.Min(marketValue * 3.5f, tameWarrant.Reward);
                    return tameWarrant;

                default:
                    Log.Error($"Warrant type switch invalid: {type}");
                    return null;
            }
        }

        private static void AssignRewards(Warrant_Pawn warrant)
        {
            var awardForLiving = (int)(warrant.Pawn.MarketValue * Rand.Range(0.5f, 2f));
            int rewardForDead = (int)(awardForLiving * Rand.Range(0.3f, 0.7f));

            if (warrant.Pawn.def.race.Animal)
            {
                warrant.rewardForDead = rewardForDead;
            }
            else
            {
                warrant.rewardForLiving = awardForLiving;
                if (Rand.Chance(0.3f))
                    warrant.rewardForDead = rewardForDead;
            }

            DoWealthScaling(warrant);
        }

        private static void DoWealthScaling(Warrant warrant)
        {
            if (!SimpleWarrantsMod.Settings.warrantRewardScaling)
                return;

            const float SCALING = 0.025f;
            float playerWealth = Find.AnyPlayerHomeMap.wealthWatcher.WealthTotal;
            int increment = (int)(playerWealth * SCALING);

            if (increment <= 0)
                return;

            switch (warrant)
            {
                case Warrant_Pawn pawn:
                    if (pawn.rewardForLiving > 0)
                        pawn.rewardForLiving += increment;
                    if (pawn.rewardForDead > 0)
                        pawn.rewardForDead += increment;
                    return;

                case Warrant_Artifact art:
                    art.reward += increment;
                    return;

                case Warrant_TameAnimal tame:
                    tame.Reward += increment;
                    return;
            }
        }

        private static bool PickWarrantIssuer(Faction targetFaction, bool mustBeHostile, out Faction issuer)
        {
            var validFactions = GetValidWarrantIssuers(targetFaction, mustBeHostile);
            var playerSettlements = Find.WorldObjects.Settlements
                    .Where(s => s.Faction == Faction.OfPlayer && s.Tile >= 0)
                    .ToList();

            // If player has no valid settlement, don't compute by distance
            if (playerSettlements.Count == 0)
                return validFactions.TryRandomElement(out issuer);

            return validFactions.TryRandomElementByWeight(f => 
                {
                    var dist = DistanceToPlayerOrInvalid(f, playerSettlements);
                    if (dist <= 0) return 0f;
                    var weight = Mathf.InverseLerp(100f, 5f, dist);
                    return weight <= 0f ? 0f : weight;
                },
                out issuer);
        }

        private static IEnumerable<Faction> GetValidWarrantIssuers(Faction targetFaction, bool mustBeHostile)
        {
            return Find.FactionManager.AllFactions.Where(faction =>
                faction.def.humanlikeFaction &&
                !faction.defeated &&
                !faction.Hidden &&
                !faction.IsPlayer &&
                (mustBeHostile ? faction.HostileTo(targetFaction) : !faction.HostileTo(targetFaction)) &&
                Find.World.worldObjects.Settlements.Any(settlement => settlement.Faction == faction && settlement.Tile >= 0));
        }

        private static int DistanceToPlayerOrInvalid(Faction faction, List<Settlement> playerSettlements)
        {
            var minDistance = int.MaxValue;
            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction != faction || settlement.Tile < 0)
                    continue;
                var dist = DistanceToNearestPlayerSettlement(settlement, playerSettlements);
                if (dist < minDistance)
                    minDistance = dist;
            }
            return minDistance == int.MaxValue ? -1 : minDistance;
        }

        private static int DistanceToNearestPlayerSettlement(Settlement factionSettlement, List<Settlement> playerSettlements)
        {
            var minDistance = int.MaxValue;
            foreach (var playerSettlement in playerSettlements)
            {
                var dist = Find.WorldGrid.TraversalDistanceBetween(
                    factionSettlement.Tile,
                    playerSettlement.Tile,
                    passImpassable: false);
                if (dist < minDistance)
                    minDistance = dist;
            }
            return minDistance;
        }

        public bool CanPutWarrantOn(Pawn pawn)
        {
            var allWarrants = availableWarrants.OfType<Warrant_Pawn>();
            if (pawn.IsColonist && allWarrants.Any(x => x.Pawn.IsColonist)) // cannot put warrants on more than one colonist
            {
                return false;
            }
            return allWarrants.All(x => x.Pawn != pawn) && (!pawn.IsColonist || SimpleWarrantsMod.Settings.enableWarrantsOnColonists);
        }

        public void PutWarrantOn(Pawn victim, string reason, Faction issuer = null)
        {
            if (issuer == Faction.OfPlayer)
            {
                return;// seems that one of method is calling this with faction player argument, it should prevent the issue
            }
            var warrant = new Warrant_Pawn
            {
                loadID = GetWarrantID(),
                createdTick = Find.TickManager.TicksGame
            };

            float basePoints = StorytellerUtility.DefaultThreatPointsNow(Find.World);
            warrant.threatPoints = (int)(basePoints * Rand.Range(0.85f, 1.15f));

            warrant.thing = victim;
            if (issuer != null)
            {
                warrant.issuer = issuer;
            }
            else
            {
                warrant.issuer = GetValidWarrantIssuers(victim.Faction, true).RandomElement();
                if (warrant.issuer == null)
                {
                    Log.Error("Failed to find a valid faction to issue warrant (hostile humanlike w/ fac base).");
                    return;
                }
            }
            warrant.reason = reason;
            Find.LetterStack.ReceiveLetter("SW.WarrantOnYourColonistReason".Translate(victim.Named("PAWN"), reason),
                "SW.WarrantOnYourColonistDesc".Translate(victim.Named("PAWN")), LetterDefOf.NegativeEvent, victim);
            AssignRewards(warrant);
            availableWarrants.Add(warrant);
        }

        public string GetWarrantID()
        {
            lastWarrantID++;
            return "Warrant" + lastWarrantID;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            HandleAvailableWarrants();
            HandleAcceptedWarrants();
            HandleCreatedWarrants();
            HandleFactionsTakenWarrants();
        }

        private void HandleAvailableWarrants()
        {
            // Remove expired warrants.
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                for (int num = availableWarrants.Count - 1; num >= 0; num--)
                {
                    if ((Find.TickManager.TicksGame - availableWarrants[num].createdTick).TicksToDays() >= 15 || availableWarrants[num].createdTick == -1)
                    {
                        availableWarrants.RemoveAt(num);
                    }
                }
            }

            if (Rand.MTBEventOccurs(SimpleWarrantsMod.Settings.warrantGenMTB, GenDate.TicksPerDay, 1))
            {
                var warrant = GenerateRandomWarrant();

                // Would it be better to re-roll warrant if CanPlayerReceive() is false?
                if (warrant != null && warrant.CanPlayerReceive())
                {
                    availableWarrants.Add(warrant);
                }
            }

            if (Rand.MTBEventOccurs(SimpleWarrantsMod.Settings.bountyHunterMTB, GenDate.TicksPerDay, 1)
                && availableWarrants.Where(x => x.IsThreatForPlayer()).TryRandomElement(out var warrantOnColonist))
            {
                var map = Find.AnyPlayerHomeMap;
                if (map != null)
                {
                    var parameters = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
                    Find.FactionManager.AllFactionsVisible.Where(x => x.def.humanlikeFaction && x.HostileTo(Faction.OfPlayer)).TryRandomElement(out parameters.faction);
                    parameters.points *= SimpleWarrantsMod.Settings.bountyHunterRaidScale;

                    if (parameters.faction != null)
                    {
                        IncidentWorker_Raid_TryGenerateRaidInfo_Patch.huntForWarrant = true;
                        IncidentDefOf.RaidEnemy.Worker.TryExecute(parameters);
                        IncidentWorker_Raid_TryGenerateRaidInfo_Patch.huntForWarrant = false;
                    }
                }
            }
        }

        public void HandleAcceptedWarrants()
        {
            // Update warrants accepted by the player.
            // If the player lets a warrant expire, they may be accused of fraud.

            for (int num = acceptedWarrants.Count - 1; num >= 0; num--)
            {
                var warrant = acceptedWarrants[num];
                if (!warrant.IsWarrantActive())
                {
                    warrant.End();

                    // Always reduce relationship (can be changed in settings)
                    int relationshipDamage = SimpleWarrantsMod.Settings.failedAIWarrantRelationshipDamage;
                    if (relationshipDamage > 0)
                        warrant.issuer.TryAffectGoodwillWith(Faction.OfPlayer, -relationshipDamage);

                    if (SimpleWarrantsMod.Settings.enableWarrantsOnFraud && Rand.Chance(0.25f))
                    {
                        var pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists_NoSlaves.Where(CanPutWarrantOn);
                        if (pawns.TryRandomElement(out var pawn))
                        {
                            PutWarrantOn(pawn, "SW.Fraud".Translate(), warrant.issuer);
                        }
                    }
                    acceptedWarrants.RemoveAt(num);
                }
            }
        }

        public void HandleCreatedWarrants()
        {
            for (int num = createdWarrants.Count - 1; num >= 0; num--)
            {
                var warrant = createdWarrants[num];
                var chance = warrant.AcceptChance() / (GenDate.TicksPerDay * 7);
                var success = Rand.Chance(chance);
                if (success)
                {
                    if (!GetValidWarrantIssuers(Faction.OfPlayer, false).Where(f => f != warrant.issuer).TryRandomElement(out var takerFaction))
                    {
                        Log.ErrorOnce("Failed to find any valid faction to accept player warrant.", warrant.GetHashCode());
                        continue;
                    }

                    warrant.AcceptBy(takerFaction);
                    createdWarrants.RemoveAt(num);
                    takenWarrants.Add(warrant);
                    warrant.tickToBeCompleted = Find.TickManager.TicksGame + (GenDate.TicksPerDay * (int)Rand.Range(3f, 15f));
                    Messages.Message("SW.FactionTookYourWarrant".Translate(takerFaction.Named("FACTION"), warrant.thing.LabelCap), MessageTypeDefOf.PositiveEvent);
                }
            }
        }

        private void HandleFactionsTakenWarrants()
        {
            // Tick player-created warrants taken by other factions.
            for (int num = takenWarrants.Count - 1; num >= 0; num--)
            {
                var warrant = takenWarrants[num];
                if (warrant.accepteer.HostileTo(Faction.OfPlayer))
                {
                    takenWarrants.RemoveAt(num);
                    createdWarrants.Add(warrant);
                    Messages.Message("SW.FactionDroppedWarrant".Translate(warrant.accepteer.Named("FACTION"), warrant.thing.LabelCap), MessageTypeDefOf.NegativeEvent);
                    continue;
                }

                // Wait until the warrant is ready to be completed.
                if (Find.TickManager.TicksGame <= warrant.tickToBeCompleted)
                    continue;

                takenWarrants.RemoveAt(num);
                var chance = warrant.SuccessChance();
                var success = Rand.Chance(chance);
                if (success)
                {
                    MakeWarrantDialog(warrant);
                }
                else
                {
                    Messages.Message("SW.FactionFailedWarrant".Translate(warrant.accepteer.Named("FACTION"), warrant.thing.LabelCap), MessageTypeDefOf.NegativeEvent);
                    int relationshipDamage = SimpleWarrantsMod.Settings.failedPlayerWarrantRelationshipDamage;
                    if (relationshipDamage > 0)
                        warrant.accepteer.TryAffectGoodwillWith(Faction.OfPlayer, -relationshipDamage);
                }
            }

            for (int num = postponedWarrants.Count - 1; num >= 0; num--)
            {
                var warrant = postponedWarrants[num];
                if (Find.TickManager.TicksGame >= warrant.postponedUntilTicks)
                {
                    MakeWarrantDialog(warrant);
                    postponedWarrants.RemoveAt(num);
                }
            }
        }

        private void MakeWarrantDialog(Warrant warrant)
        {
            var reward = 0;
            bool dead = false;
            switch (warrant)
            {
                case Warrant_Pawn wp:
                    {
                        // Chance to be returned alive is the ratio between living and dead reward.
                        float chanceReturnedAlive = Mathf.Clamp01((float)wp.rewardForLiving / (wp.rewardForLiving + wp.rewardForDead));
                        if (wp.rewardForDead > 0 && !Rand.Chance(chanceReturnedAlive))
                        {
                            dead = true;
                        }
                        reward = dead ? wp.rewardForDead : wp.rewardForLiving;
                        break;
                    }

                case Warrant_Artifact wa:
                    reward = wa.reward;
                    break;
            }
            var map = Find.AnyPlayerHomeMap;
            var silvers = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(x => !x.Position.Fogged(x.Map) && (map.areaManager.Home[x.Position] || x.IsInAnyStorage())).ToList();

            string title = "SW.FactionCompletedWarrant".Translate(warrant.accepteer.Named("FACTION"));
            DiaNode diaNode = new DiaNode("SW.FactionCompletedWarrantDesc".Translate(warrant.accepteer.Named("FACTION"), warrant.thing.LabelCap, reward));
            DiaOption payOption = new DiaOption("SW.Pay".Translate(reward));
            payOption.action = delegate
            {
                while (reward > 0)
                {
                    Thing thing = silvers.RandomElement();
                    silvers.Remove(thing);
                    if (thing == null)
                    {
                        break;
                    }
                    int num = Math.Min(reward, thing.stackCount);
                    thing.SplitOff(num).Destroy();
                    reward -= num;
                }

                var parms = StorytellerUtility.DefaultParmsNow(SW_DefOf.FactionArrival, map);
                parms.faction = warrant.accepteer;
                var toDeliver = warrant.thing;
                if (dead)
                {
                    var pawn = warrant.thing as Pawn;
                    pawn.Kill(null);
                    toDeliver = pawn.Corpse;
                }
                else
                {
                    if (warrant.thing is Pawn pawn)
                    {
                        //HealthUtility.DamageUntilDowned(pawn);
                        HealthUtility.DamageLegsUntilIncapableOfMoving(pawn, false);
                        HealthUtility.TryAnesthetize(pawn);
                    }
                }
                ((IncidentWorker_Visitors)SW_DefOf.SW_Visitors.Worker).SpawnVisitors(toDeliver, parms);
            };
            payOption.resolveTree = true;
            if (silvers.Sum(x => x.stackCount) < reward)
            {
                payOption.Disable("SW.NotEnoughSilver".Translate());
            }
            diaNode.options.Add(payOption);

            DiaOption refuseOption = new DiaOption("SW.Refuse".Translate());
            refuseOption.action = delegate
            {
                warrant.accepteer.TryAffectGoodwillWith(Faction.OfPlayer, -100);
                var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
                parms.faction = warrant.accepteer;
                IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            };
            refuseOption.resolveTree = true;
            diaNode.options.Add(refuseOption);
            if (warrant.paymentPostponed is false)
            {
                DiaOption delayOption = new DiaOption("SW.Delay24Hours".Translate());
                delayOption.action = delegate
                {
                    postponedWarrants.Add(warrant);
                    warrant.postponedUntilTicks = Find.TickManager.TicksGame + (GenDate.TicksPerHour * 24);
                    warrant.paymentPostponed = true;
                };
                delayOption.resolveTree = true;
                diaNode.options.Add(delayOption);
            }

            Find.WindowStack.Add(new Dialog_NodeTreeWithFactionInfo(diaNode, warrant.accepteer, delayInteractivity: true, radioMode: false, title));
            Find.Archive.Add(new ArchivedDialog(diaNode.text, title, warrant.accepteer));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref availableWarrants, "warrants", LookMode.Deep);
            Scribe_Collections.Look(ref acceptedWarrants, "acceptedWarrants", LookMode.Deep);
            Scribe_Collections.Look(ref createdWarrants, "givenWarrants", LookMode.Deep);
            Scribe_Collections.Look(ref takenWarrants, "takenWarrants", LookMode.Deep);
            Scribe_Collections.Look(ref postponedWarrants, "delayedWarrants", LookMode.Deep);
            Scribe_Values.Look(ref initialized, "initialized");
            Scribe_Values.Look(ref lastWarrantID, "lastWarrantID");
            Scribe_Collections.Look(ref raidLords, "raidLords", LookMode.Reference, LookMode.Deep, ref warrantKeys, ref lordValues);
            PreInit();
        }
    }
}
