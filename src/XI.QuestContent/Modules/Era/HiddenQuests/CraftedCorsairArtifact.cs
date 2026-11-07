// Crafted Corsair Artifact's era module, a line-for-line port of modules/era/lua/quests/hiddenquests/
// crafted_corsair_artifact.lua (quest rework B3b hidden quests, group 2). It restores the JST midnight wait before a
// finished piece is handed over (the June 7, 2016 version update shortened the wait to one Vana'diel day;
// https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_corsair_attire_commission', xi.pre(xi.expansion.ROV)); its onServerStart override wraps section 1's
// crafter onTrigger (Windurst Waters' and Bastok Mines' Door_House, Raqtibahl): at Prog 4 for the commission's Option
// a Wait still set plays the wait event (when the base answers at all), else the Timer is 0; and the fee's
// onEventFinish (the base, then Wait = JstMidnight()).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.HiddenQuests
{
    public sealed class CraftedCorsairArtifact : QuestModule
    {
        public CraftedCorsairArtifact()
            : base("era_corsair_attire_commission",
                "modules/era/lua/quests/hiddenquests/crafted_corsair_artifact.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            var state = m.Quest("hiddenQuests.crafted_corsair_artifact").State(1);

            // Corsair's Gants
            Commission(state, 1, Zone.WindurstWaters, Npc.WindurstWaters.DoorHouse, 946, 945);
            // Corsair's Bottes
            Commission(state, 2, Zone.BastokMines, Npc.BastokMines.DoorHouse, 524, 523);
            // Corsair's Frac
            Commission(state, 3, Zone.PortSanDoria, Npc.PortSanDoria.Raqtibahl, 760, 757);
        }

        static void Commission(QuestStateOverrideBuilder state, int option, ushort zone, string npcName, int feeCsid,
            int waitCsid)
        {
            state.Npc(zone, npcName).WrapOnTrigger((q, baseOnTrigger) =>
            {
                if (q.Quest.Step == 4 &&
                    q.Quest.Var("Option") == option)
                {
                    if (q.Quest.Var("Wait") != 0)
                    {
                        // The base handler checks the craftsman being spoken to.
                        if (baseOnTrigger(q) != null)
                        {
                            return QuestReply.Event(waitCsid);
                        }

                        return null;
                    }

                    // The base Vana'diel day timer no longer applies.
                    q.Quest.SetVar("Timer", 0);
                }

                return baseOnTrigger(q);
            });

            state.WrapOnEventFinish(zone, feeCsid, (q, baseHandler) =>
            {
                baseHandler(q);

                // quest:setTimedVar(player, 'Wait', JstMidnight()): 1 until JST midnight
                q.Quest.SetVar("Wait", 1, (uint)q.Player.JstMidnight); // Module change: Start JST midnight timer
            });
        }
    }
}
