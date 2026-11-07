// Moogles in the Wild (Log ID 4, Quest ID 102), a line-for-line port of
// scripts/quests/otherAreas/Moogles_in_the_Wild.lua (quest rework B3b Other Areas group 9). The Moogle of every Mog
// House zone (xi.moghouse.moghouseZones: one section table each; the Moogle's script, xi.moghouse.moogleTrigger /
// moogleTrade, takes the quest's nil).
// Available, The Moogle's Picnic! completed, in the Mog House of the char's home nation
// (xi.moghouse.inMogHouseInHomeNation), nation fame level 7 or more, the quest's local mustZone 0 and questSeen 0,
// bedPlacedTime set and GetSystemTime() > bedPlacedTime + 60 (a Noble's Bed placed: scripts/items/nobles_bed.lua writes
// bedPlacedTime and the local mustZone): the Moogle 30013 (0, 0, 0, 6, 0, Raptor Mantle, Wool Hat), whose finish sets
// the local questSeen 1 and begins the quest on option 1. Accepted (wherever, a Mog House zone): the trade of a Raptor
// Mantle and a Wool Hat (npcUtil.tradeMatches): 30015, whose finish runs tradeComplete, sets Prog 1 and Timer = now +
// 60. The trigger: questSeen set: nil; Prog 0: 30014 (0, 0, 0, 0, 0, the mantle, the hat), whose finish sets questSeen
// 1; Prog 1 and Timer < now: 30016, whose finish runs quest:complete (Mog's Loving Master) and on true the Mog Safe and
// Mog Safe 2 grow by 10 each; else nil.
// Kept as the Lua has it: section 2's check reads the status only (accepted in any Mog House zone's Moogle, the home
// nation's or not); questSeen is a local var (a zone change clears it), so the reminder 30014 plays once per zone visit
// and blocks 30016 after it; the Timer edge is strict (< now).
// The era module modules/era/lua/quests/otherareas/moogles_in_the_wild.lua (xi.pre(SOA)) replaces section 1's check
// (the bed placed before the last conquest tally) and section 2's 30015 finish (Timer = JstMidnight()):
// Modules/Era/OtherAreas/MooglesInTheWild.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class MooglesInTheWild : QuestScript
    {
        public MooglesInTheWild() : base(QuestLog.OtherAreas, "MOOGLES_IN_THE_WILD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Moogles_in_the_Wild";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.MogsLovingMaster,
            });
            b.Steps(1);

            // Every Mog House zone shares one section table.
            var questAvailable = b.State(QuestStatus.Available, when: q =>
            {
                int bedPlacedTime = q.Quest.Var("bedPlacedTime");

                return q.Player.HasCompletedQuest(QuestLog.OtherAreas, "THE_MOOGLE_PICNIC") &&
                    q.Player.InMogHouseInHomeNation &&
                    q.Player.FameLevel(q.Player.Nation) >= 7 &&
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
                        return QuestReply.ProgressEvent(30013, 0, 0, 0, 6, 0, Item.RaptorMantle, Item.WoolHat);
                    })
                    .OnEventFinish(zoneId, 30013, q =>
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
                        if (q.Trade.Matches((Item.RaptorMantle, 1), (Item.WoolHat, 1)))
                        {
                            return QuestReply.ProgressEvent(30015);
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
                            return QuestReply.ProgressEvent(30014, 0, 0, 0, 0, 0, Item.RaptorMantle, Item.WoolHat);
                        }
                        else if (questProgress == 1 &&
                            q.Quest.Var("Timer") < q.Player.SystemTime)
                        {
                            return QuestReply.ProgressEvent(30016);
                        }
                        return null;
                    })
                    .OnEventFinish(zoneId, 30014, q =>
                    {
                        q.Quest.SetLocalVar("questSeen", 1);
                    })
                    .OnEventFinish(zoneId, 30015, q =>
                    {
                        q.Trade.Complete();
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Timer", q.Player.SystemTime + 60);
                    })
                    .OnEventFinish(zoneId, 30016, q =>
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
