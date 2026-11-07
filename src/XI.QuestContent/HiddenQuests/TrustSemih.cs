// Trust: Semih Lafihna (hidden quest TrustSemih), ported from scripts/quests/hiddenQuests/Trust_Semih.lua (quest rework
// B3b hidden quests). Kupipi, Heaven's Tower (her script: npcs/Kupipi.lua).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Semih not learned, no cipher of his held, RoV The Path Untraveled
// completed. Kupipi: the cipher (npcUtil.giveItem), then noAction (no event).
// Kept as the Lua has it: the stopgap (its NOTE); with no room giveItem fails with its message and the talk ends in
// noAction, which also keeps Kupipi's other quests and her script from answering while the check holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustSemih : HiddenQuestScript
    {
        public TrustSemih() : base("TrustSemih") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Semih";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.SemihLafihna) &&
                    !q.Player.FindItem(Item.CipherOfSemihsAlterEgo) &&
                    q.Player.HasCompletedMission(MissionLog.Rov, Mission.Rov.ThePathUntraveled))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    // NOTE: No cap has been provided to determine if there is separate dialogue.
                    // This exists as a stopgap to ensure that the player can receive the item in
                    // case of full inventory, or having already progressed.

                    q.Player.GiveItem(Item.CipherOfSemihsAlterEgo);
                    return QuestReply.NoAction();
                });
        }
    }
}
