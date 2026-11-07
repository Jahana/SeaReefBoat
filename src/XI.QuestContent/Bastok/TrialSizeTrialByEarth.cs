// Trial-Size Trial by Earth (Log ID 1, Quest ID 72), a line-for-line port of
// scripts/quests/bastok/Trial_Size_Trial_by_Earth.lua (quest rework B3b). Ferrol : !pos 33.708 6.499 -39.425 236.
// A SMN of level 20 or more at Bastok fame level 2 gets 297 (option 1: the Mini Tuning Fork of Earth, begun). Accepted:
// Ferrol's 301 gives a new fork when the char has none (option 1), 251 otherwise; a SMN's trade of the fork plays 298,
// whose option 1 teleports the char to the Cloister of Tremors. The battlefield
// (scripts/battlefields/Cloister_of_Tremors/trial-size_trial_by_earth.lua) wins with event 32001. Its finish runs the
// handlers in the zone's 32001 order, the battlefield's container first: its onEventFinishWin teaches Titan, gives the
// Scroll of Instant Warp, adds 20 Bastok fame and calls player:completeQuest itself (a status write outside the quest,
// left in the battlefield script). So this quest's section-2 check fails by the time its 32001 handler would run, and
// the handler's battlefieldWin = 578 side (Titan, the scroll, quest:complete) is dead code on Lua; it is kept as the
// Lua has it, in the Lua's container slot (LegacyPath). The handler runs only on another fight's win while this quest
// is accepted (Trial by Earth's, 576: nothing). Completed: Ferrol's 300 (replaceDefault).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TrialSizeTrialByEarth : QuestScript
    {
        // xi.teleport.id.CLOISTER_OF_TREMORS (scripts/globals/teleports.lua)
        const int TeleportCloisterOfTremors = 47;

        public TrialSizeTrialByEarth() : base(QuestLog.Bastok, "TRIAL_SIZE_TRIAL_BY_EARTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Trial_Size_Trial_by_Earth";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.ScrollOfInstantWarp,
            });

            // the event names the zone (1, Quicksand Caves) and the level the Mini Tuning Fork caps the fight at (20)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 &&
                    q.Player.MainJob == Job.Smn &&
                    q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ferrol).OnTrigger(
                    QuestReply.ProgressEvent(297, 0, Item.MiniTuningForkOfEarth, 1, 20))
                .OnEventFinish(Zone.PortBastok, 297, q =>
                {
                    if (q.Option == 1 && q.Player.GiveItem(Item.MiniTuningForkOfEarth))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ferrol)
                    .OnTrade(q =>
                    {
                        if (q.Player.MainJob == Job.Smn && q.Trade.HasItemQty(Item.MiniTuningForkOfEarth, 1))
                        {
                            return QuestReply.ProgressEvent(298, 0, Item.MiniTuningForkOfEarth, 1, 20);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (!q.Player.HasItem(Item.MiniTuningForkOfEarth))
                        {
                            return QuestReply.ProgressEvent(301, 0, Item.MiniTuningForkOfEarth, 1, 20);
                        }
                        else
                        {
                            return QuestReply.Event(251);
                        }
                    })
                .OnEventFinish(Zone.PortBastok, 298, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.TeleportTo(TeleportCloisterOfTremors);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 301, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveItem(Item.MiniTuningForkOfEarth);
                    }
                })
                .OnEventFinish(Zone.CloisterOfTremors, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.TrialSizeTrialByEarth)
                    {
                        if (!q.Player.HasSpell(Spell.Titan))
                        {
                            q.Player.AddSpell(Spell.Titan);
                            q.Text.Special(Text.CloisterOfTremors.TITAN_UNLOCKED, 0, 0, 1);
                        }

                        if (!q.Player.HasItem(Item.ScrollOfInstantWarp))
                        {
                            q.Player.GiveItem(Item.ScrollOfInstantWarp);
                        }

                        q.Quest.Complete();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ferrol).OnTrigger(QuestReply.Event(300).ReplaceDefault());
        }
    }
}
