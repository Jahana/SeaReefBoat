// Monstrosity (Log ID 4, Quest ID 34), a line-for-line port of scripts/quests/otherAreas/Monstrosity.lua (quest rework
// B3b Other Areas group 9). Pashhow Marshlands: Suspicious Hume !pos -491.817 24.988 -618.552 (script:
// xi.monstrosity.suspiciousHumeOnTrigger, nothing until the quest is completed); Northern San d'Oria's Suspicious
// Elvaan, Port Bastok's Suspicious Galka, Port Windurst's Suspicious Tarutaru (script:
// xi.monstrosity.gladiatorOnTrigger, the CHATTER event until completed); the Feretory's zone-in.
// Every section needs xi.settings.main.ENABLE_MONSTROSITY == 1. Available: the Suspicious Hume 40, whose finish begins
// the quest. Accepted: the Suspicious Hume: no Ring of Supernal Disjunction: 41, else 42 (priorityEvent: Progress). The
// three city NPCs (xi.monstrosity.cityEvents: Northern San d'Oria 882, Port Bastok 418, Port Windurst 880): the
// trigger: no ring: base + INTRO (0), else base + HAS_RING (2) (priorityEvent); a trade with the ring held: nil;
// without it, the first of tradeItems the trade matches exactly (npcUtil.tradeMatches({ { item, 1 } })): Lizard Tail
// (species LIZARD 43), Rabbit Hide (RABBIT 1), Two-Leaf Mandragora Bud (MANDRAGORA 18): the local MONSTROSITY_UNLOCK =
// the species, then base + TRADE (1) (883, 419, 881) with the item; else nil. That event's finish (the zone's base +
// 1): the species read and the local cleared; species 0 or tradeComplete false: nothing; else the ring
// (npcUtil.giveKeyItem) and setMonstrosityData({ monstrosityId = species, species = species }). The Feretory: onZoneIn:
// 2; 2's update on option 1: xi.monstrosity.updateIntroEvent(player); 2's finish: quest:complete (no reward).
// Kept as the Lua has it: the trade's finish reads the local var, not the csid's zone (any of 883 / 419 / 881 in its
// own zone); the Feretory's 2 plays on every zone-in while accepted (the ring held or not); the reward table is empty
// (quest:complete only completes the quest).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class Monstrosity : QuestScript
    {
        public Monstrosity() : base(QuestLog.OtherAreas, "MONSTROSITY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Monstrosity";

        // tradeItems: the item and its xi.monstrositySpecies (scripts/enum/monstrosity_species.codegen.lua)
        static readonly (int ItemId, int Species)[] TradeItems =
        {
            (Item.LizardTail, 43),            // xi.monstrositySpecies.LIZARD
            (Item.RabbitHide, 1),             // xi.monstrositySpecies.RABBIT
            (Item.TwoLeafMandragoraBud, 18),  // xi.monstrositySpecies.MANDRAGORA
        };

        // xi.monstrosity.cityEvents (scripts/globals/monstrosity/constants.lua)
        static int CityEvent(ushort zone) => zone switch
        {
            Zone.PashhowMarshlands => 40,
            Zone.NorthernSanDoria => 882,
            Zone.PortBastok => 418,
            Zone.PortWindurst => 880,
            _ => throw new System.InvalidOperationException($"no xi.monstrosity.cityEvents entry for zone {zone}"),
        };

        // xi.monstrosity.cityEvent
        const int Intro = 0;
        const int Trade = 1;
        const int HasRing = 2;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q => q.Settings.MainEquals("ENABLE_MONSTROSITY", 1))
                .Npc(Zone.PashhowMarshlands, Npc.PashhowMarshlands.SuspiciousHume)
                .OnTrigger(QuestReply.ProgressEvent(40))
                .OnEventFinish(Zone.PashhowMarshlands, 40, q =>
                {
                    q.Quest.Begin();
                });

            var accepted = b.State(QuestStatus.Accepted, when: q => q.Settings.MainEquals("ENABLE_MONSTROSITY", 1));

            SuspiciousCityNpc(accepted, Zone.NorthernSanDoria, Npc.NorthernSanDoria.SuspiciousElvaan, 883);

            accepted
                .Npc(Zone.PashhowMarshlands, Npc.PashhowMarshlands.SuspiciousHume).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.RingOfSupernalDisjunction))
                    {
                        return QuestReply.PriorityEvent(41);
                    }
                    else
                    {
                        return QuestReply.PriorityEvent(42);
                    }
                });

            SuspiciousCityNpc(accepted, Zone.PortBastok, Npc.PortBastok.SuspiciousGalka, 419);
            SuspiciousCityNpc(accepted, Zone.PortWindurst, Npc.PortWindurst.SuspiciousTarutaru, 881);

            accepted
                .OnZoneIn(Zone.Feretory, q => QuestReply.Event(2)) // onZoneIn returns the cutscene id 2
                .OnEventUpdate(Zone.Feretory, 2, q =>
                {
                    if (q.Option == 1)
                    {
                        // the Lua global: player:updateEvent(race, face, the starter cutscene of getMonstrosityData())
                        q.Player.MonstrosityUpdateIntroEvent();
                    }
                })
                .OnEventFinish(Zone.Feretory, 2, q =>
                {
                    q.Quest.Complete();
                });
        }

        // suspiciousCityNpc, and its zone's tradeEventFinish
        static void SuspiciousCityNpc(QuestStateBuilder state, ushort zone, string npc, int tradeCsid)
        {
            state
                .Npc(zone, npc)
                .OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RingOfSupernalDisjunction))
                    {
                        return null;
                    }

                    foreach (var entry in TradeItems)
                    {
                        int itemId = entry.ItemId;
                        if (q.Trade.Matches((itemId, 1)))
                        {
                            q.Player.SetLocalVar("MONSTROSITY_UNLOCK", entry.Species);
                            return QuestReply.ProgressEvent(CityEvent(q.Zone) + Trade, itemId);
                        }
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int baseEvent = CityEvent(q.Zone);

                    if (!q.Player.HasKeyItem(KeyItem.RingOfSupernalDisjunction))
                    {
                        return QuestReply.PriorityEvent(baseEvent + Intro);
                    }
                    else
                    {
                        return QuestReply.PriorityEvent(baseEvent + HasRing);
                    }
                })
                .OnEventFinish(zone, tradeCsid, TradeEventFinish);
        }

        static void TradeEventFinish(IQuestContext q)
        {
            int species = q.Player.LocalVar("MONSTROSITY_UNLOCK");
            q.Player.SetLocalVar("MONSTROSITY_UNLOCK", 0);

            if (species == 0 ||
                !q.Trade.CompleteChecked())
            {
                return;
            }

            q.Player.GiveKeyItem(KeyItem.RingOfSupernalDisjunction);
            q.Player.SetMonstrosityData(species, species);
        }
    }
}
