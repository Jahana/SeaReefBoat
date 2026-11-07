// Crafted Puppetry Artifact's era module, a line-for-line port of modules/era/lua/quests/hiddenquests/
// crafted_puppetry_artifact.lua (quest rework B3b hidden quests, group 2). It restores the JST midnight wait before a
// puppetry piece is handed over (the June 7, 2016 version update shortened the wait to one Vana'diel day;
// https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_puppetry_attire_commission', xi.pre(xi.expansion.ROV)); its onServerStart override wraps section 1's
// Dhima_Polevhia onTrigger ([PUP]orderStage 2: a [PUP]orderWait still set plays 796, else [PUP]orderTime is 0) and the
// materials' onEventFinish 795 (the base, then [PUP]orderWait = 1 until JST midnight).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.HiddenQuests
{
    public sealed class CraftedPuppetryArtifact : QuestModule
    {
        public CraftedPuppetryArtifact()
            : base("era_puppetry_attire_commission",
                "modules/era/lua/quests/hiddenquests/crafted_puppetry_artifact.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            var state = m.Quest("hiddenQuests.crafted_puppetry_artifact").State(1);

            state.Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.DhimaPolevhia).WrapOnTrigger((q, baseOnTrigger) =>
            {
                if (q.Player.CharVar("[PUP]orderStage") == 2)
                {
                    if (q.Player.CharVar("[PUP]orderWait") != 0)
                    {
                        return QuestReply.Event(796); // Order is not ready.
                    }

                    // The base Vana'diel day timer no longer applies.
                    q.Player.SetCharVar("[PUP]orderTime", 0);
                }

                return baseOnTrigger(q);
            });

            state.WrapOnEventFinish(Zone.AhtUrhganWhitegate, 795, (q, baseHandler) =>
            {
                baseHandler(q);

                // Work starts when the materials are traded.
                // Module change: Start JST midnight timer
                q.Player.SetCharVar("[PUP]orderWait", 1, (uint)q.Player.JstMidnight);
            });
        }
    }
}
