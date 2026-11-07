// Trust: Halver (hidden quest TrustHalver), ported from scripts/quests/hiddenQuests/Trust_Halver.lua (quest rework B3b
// hidden quests). Halver, Chateau d'Oraguille (his script: npcs/Halver.lua; default text HALVER_OFFSET + 1092).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Halver not learned, no cipher of his held, RoV The Path
// Untraveled completed. Halver: the cipher (npcUtil.giveItem), then noAction (no event). Kept as the Lua has it: the
// stopgap (its NOTE: no cap to tell the dialogue apart): every talk while the check holds tries to give the cipher;
// with no room giveItem fails with its message and the talk still ends in noAction.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustHalver : HiddenQuestScript
    {
        public TrustHalver() : base("TrustHalver") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Halver";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Halver) &&
                    !q.Player.FindItem(Item.CipherOfHalversAlterEgo) &&
                    q.Player.HasCompletedMission(MissionLog.Rov, Mission.Rov.ThePathUntraveled))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    // NOTE: No cap has been provided to determine if there is separate dialogue.
                    // This exists as a stopgap to ensure that the player can receive the item in
                    // case of full inventory, or having already progressed.

                    q.Player.GiveItem(Item.CipherOfHalversAlterEgo);
                    return QuestReply.NoAction();
                });
        }
    }
}
