// Give a Moogle a Break (Log ID 4, Quest ID 100), a line-for-line port of
// scripts/quests/otherAreas/Give_a_Moogle_a_Break.lua (quest rework B3b Other Areas group 9). The Moogle of every Mog
// House zone (xi.moghouse.moghouseZones: one section table each; the Moogle's script, xi.moghouse.moogleTrigger /
// moogleTrade, takes the quest's nil).
// Available, in the Mog House of the char's home nation (xi.moghouse.inMogHouseInHomeNation), nation fame level 3 or
// more, the quest's local mustZone 0 and questSeen 0, bedPlacedTime set and GetSystemTime() > bedPlacedTime + 60 (a
// Bronze Bed placed: scripts/items/bronze_bed.lua writes bedPlacedTime and the local mustZone): the Moogle 30005 (0, 0,
// 0, 5, 0, Power Bow, Beetle Ring), whose finish sets the local questSeen 1 and begins the quest on option 1. Accepted
// (wherever, a Mog House zone): the trade of a Power Bow and a Beetle Ring (npcUtil.tradeMatches): 30007, whose finish
// runs tradeComplete, sets Prog 1 and Timer = now + 60. The trigger: questSeen set: nil; Prog 0: 30006 (0, 0, 0, 0, 0,
// the bow, the ring), whose finish sets questSeen 1; Prog 1 and Timer < now: 30008, whose finish runs quest:complete
// (Mog's Kind Master) and on true the Mog Safe and Mog Safe 2 grow by 10 each; else nil.
// Kept as the Lua has it: section 2's check reads the status only (accepted in any Mog House zone's Moogle, the home
// nation's or not); questSeen is a local var (a zone change clears it), so the reminder 30006 plays once per zone visit
// and blocks 30008 after it; the Timer edge is strict (< now).
// The era module modules/era/lua/quests/otherareas/give_a_moogle_a_break.lua (xi.pre(SOA)) replaces section 1's check
// (the bed placed before the last conquest tally) and section 2's 30007 finish (Timer = JstMidnight()):
// Modules/Era/OtherAreas/GiveAMoogleABreak.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class GiveAMoogleABreak : QuestScript
    {
        public GiveAMoogleABreak() : base(QuestLog.OtherAreas, "GIVE_A_MOOGLE_A_BREAK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Give_a_Moogle_a_Break";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.MogsKindMaster,
            });
            b.Steps(1);

            // Every Mog House zone shares one section table.
            var questAvailable = b.State(QuestStatus.Available, when: q =>
            {
                int bedPlacedTime = q.Quest.Var("bedPlacedTime");

                return q.Player.InMogHouseInHomeNation &&
                    q.Player.FameLevel(q.Player.Nation) >= 3 &&
                    q.Quest.LocalVar("mustZone") == 0 &&
                    q.Quest.LocalVar("questSeen") == 0 &&
                    bedPlacedTime != 0 &&
                    q.Player.SystemTime > bedPlacedTime + 60;
            });
            foreach (var (zoneId, moogle) in MogHouse.Moogles)
            {
                questAvailable
                    .Npc(zoneId, moogle).OnTrigger(q =>
                    {
                        return QuestReply.ProgressEvent(30005, 0, 0, 0, 5, 0, Item.PowerBow, Item.BeetleRing);
                    })
                    .OnEventFinish(zoneId, 30005, q =>
                    {
                        q.Quest.SetLocalVar("questSeen", 1);

                        if (q.Option == 1)
                        {
                            q.Quest.Begin();
                        }
                    });
            }

            var questAccepted = b.State(QuestStatus.Accepted);
            foreach (var (zoneId, moogle) in MogHouse.Moogles)
            {
                questAccepted
                    .Npc(zoneId, moogle)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.PowerBow, 1), (Item.BeetleRing, 1)))
                        {
                            return QuestReply.ProgressEvent(30007);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;
                        int questSeen = q.Quest.LocalVar("questSeen");

                        if (questSeen != 0)
                        {
                            return null;
                        }

                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(30006, 0, 0, 0, 0, 0, Item.PowerBow, Item.BeetleRing);
                        }
                        else if (questProgress == 1 &&
                            q.Quest.Var("Timer") < q.Player.SystemTime)
                        {
                            return QuestReply.ProgressEvent(30008);
                        }
                        return null;
                    })
                    .OnEventFinish(zoneId, 30006, q =>
                    {
                        q.Quest.SetLocalVar("questSeen", 1);
                    })
                    .OnEventFinish(zoneId, 30007, q =>
                    {
                        q.Trade.Complete();
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Timer", q.Player.SystemTime + 60);
                    })
                    .OnEventFinish(zoneId, 30008, q =>
                    {
                        if (q.Quest.Complete())
                        {
                            q.Player.ChangeContainerSize(MogHouse.MogSafe, 10);
                            q.Player.ChangeContainerSize(MogHouse.MogSafe2, 10);
                        }
                    });
            }
        }
    }
}
