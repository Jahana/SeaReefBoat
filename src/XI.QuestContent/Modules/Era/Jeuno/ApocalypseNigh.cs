// Apocalypse Nigh's era module, a line-for-line port of modules/era/lua/quests/jeuno/apocalypse_nigh.lua (quest rework
// B3b Jeuno, P4). It restores the JST midnight
// waits: Module:new('era_quest_apocalypse_nigh', xi.pre(xi.expansion.TVR)); its onServerStart override replaces section
// 1's check (the Timer compared with GetSystemTime()), section 2's Empyreal Paradox 32001 finish (Timer = JstMidnight())
// and section 2's Norg _700 and Gilgamesh onTriggers (GetSystemTime() against the Timer).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class ApocalypseNigh : QuestModule
    {
        public ApocalypseNigh() : base("era_quest_apocalypse_nigh", "modules/era/lua/quests/jeuno/apocalypse_nigh.lua") { }

        // xi.pre(xi.expansion.TVR)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Tvr);

        // Norg's Oaken Door (no Npc constant: its name has no letter)
        const string NorgDoor700 = "_700";

        protected override void Build(QuestModuleBuilder m)
        {
            var quest = m.Quest(QuestKey.Jeuno.ApocalypseNigh);

            // Copy of the base check with the timer compared as a timestamp.
            quest.State(1).ReplaceCheck(q =>
                q.Quest.Status == XI.Quests.QuestStatus.Available &&
                q.Player.HasCompletedQuest(XI.Quests.QuestLog.Jeuno, "SHADOWS_OF_THE_DEPARTED") &&
                q.Player.SystemTime >= q.Quest.Var("Timer") && // Module change: this quest opens at JST midnight.
                !q.Quest.MustZone);

            // quest.sections[2][xi.zone.EMPYREAL_PARADOX] / [xi.zone.NORG]
            quest.State(2)
                // Copy of the base handler with the one Vana'diel day wait extended to JST midnight.
                .ReplaceOnEventFinish(Zone.EmpyrealParadox, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.ApocalypseNigh &&
                        q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                        q.Quest.SetVar("Timer", q.Player.JstMidnight); // Module change: the wait runs to JST midnight.
                    }
                })
                // Copy of the base handler with the timer compared as a timestamp.
                .Npc(Zone.Norg, NorgDoor700).ReplaceOnTrigger(q =>
                {
                    if (q.Quest.Step == 5 &&
                        q.Player.SystemTime < q.Quest.Var("Timer")) // Module change: Gilgamesh is unavailable until JST midnight.
                    {
                        return QuestReply.ProgressEvent(235);
                    }
                    return null;
                })
                // Copy of the base handler with the timer compared as a timestamp.
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).ReplaceOnTrigger(q =>
                {
                    if (q.Player.SystemTime >= q.Quest.Var("Timer") && // Module change: Gilgamesh waits until JST midnight.
                        !q.Quest.MustZone)
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 5)
                        {
                            return QuestReply.ProgressEvent(232, 252, 6);
                        }
                        else if (questProgress == 6)
                        {
                            return QuestReply.ProgressEvent(234, 252);
                        }
                    }
                    return null;
                });
        }
    }
}
