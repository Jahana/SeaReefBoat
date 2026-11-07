// Beneath a Blood Red Sky (Log ID 8, Quest ID 183), a line-for-line port of
// scripts/quests/abyssea/Beneath_a_Blood_Red_Sky.lua (quest rework B3b Abyssea). Abyssea - Empyreal Paradox: its
// zone-in. Added by Emissaries of God's 8 (no available section). Accepted: the Abyssea - Empyreal Paradox zone-in
// plays 202, whose finish completes it and adds The Wyrm God (accepted). Qufim Island's Transcendental Radiance reads
// its status (a read, unchanged). Kept as the Lua has it: 202 is returned as a number (an Event), from any zone; no
// completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class BeneathABloodRedSky : QuestScript
    {
        public BeneathABloodRedSky() : base(QuestLog.Abyssea, "BENEATH_A_BLOOD_RED_SKY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Beneath_a_Blood_Red_Sky";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted)
                .OnZoneIn(Zone.AbysseaEmpyrealParadox, q =>
                {
                    return QuestReply.Event(202);
                })
                .OnEventFinish(Zone.AbysseaEmpyrealParadox, 202, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "THE_WYRM_GOD");
                    }
                });
        }
    }
}
