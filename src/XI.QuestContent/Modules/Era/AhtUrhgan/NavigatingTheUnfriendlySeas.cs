// Navigating the Unfriendly Seas' era module, a line-for-line port of
// modules/era/lua/quests/ahturhgan/navigating_the_unfriendly_seas.lua (quest rework B3b Aht Urhgan, group 2). It restores the JST midnight wait before
// the Leypoint finishes with the hydrogauge (the June 7, 2016 version update shortened the wait to one minute;
// https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_quest_navigating_the_unfriendly_seas', xi.pre(xi.expansion.ROV)); its onServerStart override wraps
// section 2's Wajaom Woodlands Leypoint onTrade: the base handler runs (its confirm, Prog 2 and Wait = now + 60); when it
// returned an action, Wait = JstMidnight(); the action is returned as the base gave it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class NavigatingTheUnfriendlySeas : QuestModule
    {
        public NavigatingTheUnfriendlySeas() : base("era_quest_navigating_the_unfriendly_seas", "modules/era/lua/quests/ahturhgan/navigating_the_unfriendly_seas.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.AhtUrhgan.NavigatingTheUnfriendlySeas)
                // The Leypoint finishes measuring at JST midnight.
                .State(2).Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.Leypoint).WrapOnTrade((q, baseTrade) =>
                {
                    var action = baseTrade(q);
                    if (action != null)
                    {
                        q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                    }

                    return action;
                });
        }
    }
}
