// Trust: Zeid II (hidden quest TrustZeidII), ported from scripts/quests/hiddenQuests/Trust_Zeid_II.lua (quest rework
// B3b hidden quests). The door _700, Norg (default messageSpecial DOOR_IS_LOCKED). One section: ENABLE_TRUST_QUESTS
// 1, Zeid II not learned, no cipher of his held, RoV Volto Oscuro completed (no permit test). The door: the cipher
// (npcUtil.giveItem), then noAction (no event). Kept as the Lua has it: the stopgap (its NOTE); no trust permit asked
// for (the only trust quest here without it).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustZeidII : HiddenQuestScript
    {
        public TrustZeidII() : base("TrustZeidII") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Zeid_II";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    !q.Player.HasSpell(Spell.ZeidIi) &&
                    !q.Player.FindItem(Item.CipherOfZeidsAlterEgoIi) &&
                    q.Player.HasCompletedMission(MissionLog.Rov, Mission.Rov.VoltoOscuro))
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(q =>
                {
                    // NOTE: No cap has been provided to determine if there is separate dialogue.  This
                    // exists as a stopgap to ensure that the player can receive the item in case of full
                    // inventory, or having already progressed.

                    q.Player.GiveItem(Item.CipherOfZeidsAlterEgoIi);
                    return QuestReply.NoAction();
                });
        }
    }
}
