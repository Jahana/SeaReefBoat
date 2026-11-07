// Her Memories: Operation Cupid (Log ID 7, Quest ID 68), a line-for-line port of
// scripts/quests/crystalWar/Her_Memories_Operation_Cupid.lua (quest rework B3b Crystal War, draft). Sauromugue
// Champaign [S]: Bulwark Gate !pos -447.174 -1.831 342.417 (default DOOR_FIRMLY_SEALED). Vunkerl Inlet [S]: Leadavox
// !pos 206 -32 316 (default 100).
// Available, the WotG mission Her Memories current: the Batallia Downs [S] zone-in from Jugner Forest [S]: 23, whose
// finish begins it. Accepted: Bulwark Gate: the Pot of Martial Relish: 13, whose finish sets Prog 2 and takes the pot;
// else Prog 0: 11 (-> Prog 1); Prog 2: 14; else 12 (Events, oncePerZone). Leadavox: no pot, a Bottle of Rice Vinegar, a
// Jar of Ground Wasabi and a Sprig of Holy Basil alone, Prog 1: 4, whose finish confirms the trade and gives the pot; a
// talk with the pot: 5 (an Event, oncePerZone). The Batallia Downs [S] zone-in from Rolanberry Fields [S] at Prog 2:
// 24, whose finish completes it (the Large Memory Fragment) and runs xi.wotg.helpers.checkMemoryFragments.
// Kept as the Lua has it: 23 begins on any option; the Bulwark Gate's 13 answers the pot at any Prog; Leadavox's trade
// at Prog 2 (the pot given back by a GM) is refused only by the Prog; no completed section.
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): upstream's Batallia Downs [S] IDs.lua has no
// REPORT_TO_CAIT_SITH, so with all four fragments the helper raised there (the quest completed, Her Memories not). The
// zone's IDs.lua now has it (6997, the client's text for the zone), so the helper says it, completes Her Memories and
// adds Forget Me Not.
// Rival containers: Knot Quite There at the Bulwark Gate (105, Progress at its Prog 0): golden flow #rivals. Better
// Part of Valor at Leadavox (101, Progress, over the talk's 5; 102 ties it): crystalWar.better_part_of_valor#rivals.
// Leadavox's other trade lines (Better Part of Valor, Beneath the Mask) take other items exactly: 4's kind is pinned in
// tests/XI.Verify.Quests/CrystalWarGroup10.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HerMemoriesOperationCupid : QuestScript
    {
        public HerMemoriesOperationCupid() : base(QuestLog.CrystalWar, "HER_MEMORIES_OPERATION_CUPID") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Her_Memories_Operation_Cupid";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.LargeMemoryFragment3,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Wotg) == Mission.Wotg.HerMemories)
                .OnZoneIn(Zone.BatalliaDownsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.JugnerForestS)
                    {
                        return QuestReply.Event(23);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BatalliaDownsS, 23, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (q.Player.HasKeyItem(KeyItem.PotOfMartialRelish))
                    {
                        return QuestReply.ProgressEvent(13);
                    }
                    else if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(11);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(14).OncePerZone();
                    }
                    else
                    {
                        return QuestReply.Event(12).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 11, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 13, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.DelKeyItem(KeyItem.PotOfMartialRelish);
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.Leadavox).OnTrade(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PotOfMartialRelish) &&
                        q.Trade.HasExactly(Item.BottleOfRiceVinegar, Item.JarOfGroundWasabi, Item.SprigOfHolyBasil) &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.PotOfMartialRelish))
                    {
                        return QuestReply.Event(5).OncePerZone();
                    }
                    return null;
                })
                .OnEventFinish(Zone.VunkerlInletS, 4, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.PotOfMartialRelish);
                })
                .OnZoneIn(Zone.BatalliaDownsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.RolanberryFieldsS &&
                        q.Quest.Step == 2)
                    {
                        return QuestReply.Event(24);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BatalliaDownsS, 24, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.CheckMemoryFragments();
                    }
                });
        }
    }
}
