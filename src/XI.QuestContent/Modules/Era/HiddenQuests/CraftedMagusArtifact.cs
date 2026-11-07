// Crafted Magus Artifact's era module, a line-for-line port of modules/era/lua/quests/hiddenquests/
// crafted_magus_artifact.lua (quest rework B3b hidden quests, group 2). It restores the JST midnight wait between the
// Magus pieces (the June 7, 2016 version update shortened the wait to one Vana'diel day;
// https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_magus_attire_commission', xi.pre(xi.expansion.ROV)); its onServerStart override wraps section 1's
// Lathuya onTrigger (no order and 1 or 2 pieces made: a [BLUAF]RestingTimer still set plays the asleep event, else
// RestingDay is 0) and the piece onEventFinish 736 / 744 / 752 (the base, then, when a piece was handed over and
// another remains, [BLUAF]RestingTimer = 1 until JST midnight).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.HiddenQuests
{
    public sealed class CraftedMagusArtifact : QuestModule
    {
        public CraftedMagusArtifact()
            : base("era_magus_attire_commission", "modules/era/lua/quests/hiddenquests/crafted_magus_artifact.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        // utils.mask.countBits(mask, 3)
        static int CountBits(int mask)
        {
            int count = 0;
            for (int bit = 0; bit < 3; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        protected override void Build(QuestModuleBuilder m)
        {
            var state = m.Quest("hiddenQuests.crafted_magus_artifact").State(1);

            state.Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Lathuya).WrapOnTrigger((q, baseOnTrigger) =>
            {
                int remainingBLUAF = q.Player.CharVar("[BLUAF]Remaining");
                int totalCraftedPieces = 3 - CountBits(remainingBLUAF);

                if (q.Player.CharVar("[BLUAF]Current") == 0 &&
                    totalCraftedPieces > 0 &&
                    totalCraftedPieces < 3)
                {
                    // Asleep message, wait until Japanese midnight passes.
                    if (q.Player.CharVar("[BLUAF]RestingTimer") != 0)
                    {
                        return QuestReply.Event(737 + (8 * totalCraftedPieces - 8));
                    }

                    // The base Vana'diel day timer no longer applies.
                    q.Player.SetCharVar("[BLUAF]RestingDay", 0);
                }

                return baseOnTrigger(q);
            });

            // Each piece is handed over by its own event.
            foreach (int eventId in new[] { 736, 744, 752 })
            {
                state.WrapOnEventFinish(Zone.AhtUrhganWhitegate, eventId, (q, baseHandler) =>
                {
                    // The base handler clears the bit for the piece handed over.
                    int remainingBLUAF = q.Player.CharVar("[BLUAF]Remaining");

                    baseHandler(q);

                    // A piece was handed over and another remains.
                    if (q.Player.CharVar("[BLUAF]Remaining") != 0 &&
                        q.Player.CharVar("[BLUAF]Remaining") != remainingBLUAF)
                    {
                        // Module change: Start JST midnight timer
                        q.Player.SetCharVar("[BLUAF]RestingTimer", 1, (uint)q.Player.JstMidnight);
                    }
                });
            }
        }
    }
}
