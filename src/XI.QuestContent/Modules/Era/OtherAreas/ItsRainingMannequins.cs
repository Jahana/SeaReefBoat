// It's Raining Mannequins!'s era module, a line-for-line port of modules/era/lua/quests/otherareas/its_raining_mannequins.lua
// (quest rework B3b, Other Areas group 4; P4). It
// restores the JST midnight wait while Fyi Chalmwoh assembles the mannequin (the June 17, 2014 version update shortened the
// wait to one minute; https://forum.square-enix.com/ffxi/threads/42614-Jun-17-2014-(JST)-Version-Update,
// https://wiki.ffo.jp/html/7101.html). Module:new('era_quest_its_raining_mannequins', xi.pre(xi.expansion.SOA)); its
// onServerStart override wraps section 4's Mhaura onEventFinish 309 (the base, then Wait = JstMidnight()) and replaces
// section 5's Fyi_Chalmwoh onTrigger with a copy of the quest's whose test is GetSystemTime() >= Wait (no + 60): 'Wait' now
// holds the due time rather than the time of the trade.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.OtherAreas
{
    public sealed class ItsRainingMannequins : QuestModule
    {
        public ItsRainingMannequins() : base("era_quest_its_raining_mannequins", "modules/era/lua/quests/otherareas/its_raining_mannequins.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.OtherAreas.ItsRainingMannequins)
                // quest.sections[4][xi.zone.MHAURA].onEventFinish[309]: the quest's state 4 (accepted, Prog 2)
                // The parts are assembled overnight. The mannequin is collected at JST midnight.
                .State(4).WrapOnEventFinish(Zone.Mhaura, 309, (q, basePartsCS) =>
                {
                    basePartsCS(q);
                    q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                })
                // quest.sections[5][xi.zone.MHAURA]['Fyi_Chalmwoh'].onTrigger: the quest's state 5 (accepted, Prog 3)
                // Copy of the base handler. 'Wait' now holds the due time rather than the time of the trade.
                .State(5).Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).ReplaceOnTrigger(q =>
                {
                    if (q.Player.SystemTime >= q.Quest.Var("Wait")) // Module change
                    {
                        return QuestReply.ProgressEvent(311);
                    }
                    else
                    {
                        return QuestReply.Event(310); // Please wait
                    }
                });
        }
    }
}
