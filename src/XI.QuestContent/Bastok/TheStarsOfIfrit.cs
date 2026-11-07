// The Stars of Ifrit (Log ID 1, Quest ID 42), a line-for-line port of scripts/quests/bastok/The_Stars_of_Ifrit.lua (quest
// rework B3b). Agapito : !pos -72.093 -3.097 9.309 236; qm1 : !pos -9 -5 -13 223, shown only at night under a full moon
// (onGameHour in the San d'Oria-Jeuno Airship's Zone.lua). The Carrier Pigeon Letter is a permanent key item: the
// completion keeps it. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheStarsOfIfrit : QuestScript
    {
        public TheStarsOfIfrit() : base(QuestLog.Bastok, "THE_STARS_OF_IFRIT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Stars_of_Ifrit";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Bastok, Gil = 2100, Title = Title.StarOfIfrit });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasKeyItem(KeyItem.AirshipPass) && q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.PortBastok, Npc.PortBastok.Agapito).OnTrigger(QuestReply.ProgressEvent(180))
                .OnEventFinish(Zone.PortBastok, 180, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Agapito).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.CarrierPigeonLetter) ? QuestReply.ProgressEvent(181) : null)
                .OnEventFinish(Zone.PortBastok, 181, q => q.Quest.Complete())
                .Npc(Zone.SanDoriaJeunoAirship, Npc.SanDoriaJeunoAirship.qm1).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.CarrierPigeonLetter) ? QuestReply.KeyItem(KeyItem.CarrierPigeonLetter) : null);
        }
    }
}
