// Soul Searching (Log ID 5, Quest ID 162), a line-for-line port of scripts/quests/outlands/Soul_Searching.lua (quest
// rework B3b Outlands, group 5). The Sanctuary of Zi'Tah: Cermet_Headstone !pos 235 0 280 (no script; default
// CANNOT_REMOVE_FRAG; Rhapsodies of Zilart 5 Headstone Pilgrimage's monument: its 201 finish adds this quest; with the
// Light Fragment held it answers nil until this quest is completed, then ALREADY_OBTAINED_FRAG; completed, its
// replaceDefault ZILART_MONUMENT ranks below this quest's message: #kinds).
// Accepted only (the mission begins it): the headstone with the Prismatic Fragment plays 202 (the fragment), whose
// finish runs quest:complete (the Bat Earring, Guider of Souls to the Sanctuary); without it, AIR_REMAINS_STAGNANT.
// Kept as the Lua has it: 202's finish ignores complete's result (the fragment is not taken).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class SoulSearching : QuestScript
    {
        public SoulSearching() : base(QuestLog.Outlands, "SOUL_SEARCHING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Soul_Searching";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.BatEarring,
                Title = Title.GuiderOfSoulsToTheSanctuary,
            });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.CermetHeadstone).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.PrismaticFragment))
                    {
                        return QuestReply.ProgressEvent(202, KeyItem.PrismaticFragment);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.AIR_REMAINS_STAGNANT);
                    }
                })
                .OnEventFinish(Zone.TheSanctuaryOfZitah, 202, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
