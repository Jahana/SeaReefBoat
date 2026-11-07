// Olduum's era module, a line-for-line port of modules/era/lua/quests/ahturhgan/olduum.lua (quest rework B3b Aht
// Urhgan, P4; group 10). It restores the 24-hour wait for another Olduum Ring (the June 7, 2016 version update reduced
// it to one minute; https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update,
// https://wiki.ffo.jp/html/7472.html). Module:new('era_quest_olduum', xi.pre(xi.expansion.ROV)); its onServerStart
// override wraps section 3's Wajaom Woodlands onEventFinish 2: the base runs, and when it changed Wait (the ring
// given), Wait = now + 86400.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class Olduum : QuestModule
    {
        public Olduum() : base("era_quest_olduum", "modules/era/lua/quests/ahturhgan/olduum.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[3][xi.zone.WAJAOM_WOODLANDS].onEventFinish[2]: the quest's state 3 (completed)
            m.Quest(QuestKey.AhtUrhgan.Olduum)
                .State(3).WrapOnEventFinish(Zone.WajaomWoodlands, 2, (q, baseEventFinish) =>
                {
                    int previousWait = q.Quest.Var("Wait");
                    baseEventFinish(q);

                    if (q.Quest.Var("Wait") != previousWait)
                    {
                        q.Quest.SetVar("Wait", q.Player.SystemTime + 86400); // Module change
                    }
                });
        }
    }
}
