// The Moogle's Picnic! (Log ID 4, Quest ID 101), a line-for-line port of
// scripts/quests/otherAreas/The_Moogles_Picnic.lua (quest rework B3b Other Areas group 9). The Moogle of every Mog
// House zone (xi.moghouse.moghouseZones: one section table each; the Moogle's script, xi.moghouse.moogleTrigger /
// moogleTrade, takes the quest's nil).
// Available, Give a Moogle a Break completed, in the Mog House of the char's home nation
// (xi.moghouse.inMogHouseInHomeNation), nation fame level 5 or more, the quest's local mustZone 0 and questSeen 0,
// bedPlacedTime set and GetSystemTime() > bedPlacedTime + 60 (a Mahogany Bed placed: scripts/items/mahogany_bed.lua
// writes bedPlacedTime and the local mustZone): the Moogle 30009 (0, 0, 0, 4, 0, Shrimp Lure, Stick of Selbina Butter),
// whose finish sets the local questSeen 1 and begins the quest on option 1. Accepted (wherever, a Mog House zone): the
// trade of a Shrimp Lure and a Stick of Selbina Butter (npcUtil.tradeMatches): 30011, whose finish runs tradeComplete,
// sets Prog 1 and Timer = now + 60. The trigger: questSeen set: nil; Prog 0: 30010 (0, 0, 0, 0, 0, the lure, the
// butter), whose finish sets questSeen 1; Prog 1 and Timer < now: 30012, whose finish runs quest:complete (Mog's
// Exceptionally Kind Master) and on true the Mog Safe and Mog Safe 2 grow by 10 each; else nil.
// Kept as the Lua has it: section 2's check reads the status only (accepted in any Mog House zone's Moogle, the home
// nation's or not); questSeen is a local var (a zone change clears it), so the reminder 30010 plays once per zone visit
// and blocks 30012 after it; the Timer edge is strict (< now).
// The era module modules/era/lua/quests/otherareas/the_moogles_picnic.lua (xi.pre(SOA)) replaces section 1's check (the
// bed placed before the last conquest tally) and section 2's 30011 finish (Timer = JstMidnight()):
// Modules/Era/OtherAreas/TheMooglePicnic.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheMooglePicnic : QuestScript
    {
        public TheMooglePicnic() : base(QuestLog.OtherAreas, "THE_MOOGLE_PICNIC") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Moogles_Picnic";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.MogsExceptionallyKindMaster,
            });
            b.Steps(1);

            // Every Mog House zone shares one section table.
            var questAvailable = b.State(QuestStatus.Available, when: q =>
            {
                int bedPlacedTime = q.Quest.Var("bedPlacedTime");

                return q.Player.HasCompletedQuest(QuestLog.OtherAreas, "GIVE_A_MOOGLE_A_BREAK") &&
                    q.Player.InMogHouseInHomeNation &&
                    q.Player.FameLevel(q.Player.Nation) >= 5 &&
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
                        return QuestReply.ProgressEvent(30009, 0, 0, 0, 4, 0, Item.ShrimpLure,
                            Item.StickOfSelbinaButter);
                    })
                    .OnEventFinish(zoneId, 30009, q =>
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
                        if (q.Trade.Matches((Item.ShrimpLure, 1), (Item.StickOfSelbinaButter, 1)))
                        {
                            return QuestReply.ProgressEvent(30011);
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
                            return QuestReply.ProgressEvent(30010, 0, 0, 0, 0, 0, Item.ShrimpLure,
                                Item.StickOfSelbinaButter);
                        }
                        else if (questProgress == 1 &&
                            q.Quest.Var("Timer") < q.Player.SystemTime)
                        {
                            return QuestReply.ProgressEvent(30012);
                        }
                        return null;
                    })
                    .OnEventFinish(zoneId, 30010, q =>
                    {
                        q.Quest.SetLocalVar("questSeen", 1);
                    })
                    .OnEventFinish(zoneId, 30011, q =>
                    {
                        q.Trade.Complete();
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Timer", q.Player.SystemTime + 60);
                    })
                    .OnEventFinish(zoneId, 30012, q =>
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
