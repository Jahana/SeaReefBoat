// Guest of Hauteur (Log ID 1, Quest ID 3), a line-for-line port of scripts/quests/bastok/Guest_of_Hauteur.lua (quest
// rework B3b). Powhatan : !pos -152.135 -7.48 19.014 236; Steel Bones : !pos -185.766 1.999 -57.631 236. Steel Bones
// gives the Letter from Domien to a char with a Maul or a Replica Maul in the main hand. The Lua file has no known bug
// to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class GuestOfHauteur : QuestScript
    {
        public GuestOfHauteur() : base(QuestLog.Bastok, "GUEST_OF_HAUTEUR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Guest_of_Hauteur";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Bastok, Item = Item.Targe });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Bastok) >= 3 &&
                    q.Player.MainLevel >= 31 &&
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "WELCOME_TO_BASTOK"))
                .Npc(Zone.PortBastok, Npc.PortBastok.Powhatan).OnTrigger(QuestReply.ProgressEvent(55))
                .OnEventFinish(Zone.PortBastok, 55, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Powhatan).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.LetterFromDomien) ? QuestReply.Event(56) : QuestReply.ProgressEvent(58))
                .Npc(Zone.PortBastok, Npc.PortBastok.SteelBones).OnTrigger(q =>
                {
                    int mainSlot = q.Player.EquipId(Slot.Main);

                    if (!q.Player.HasKeyItem(KeyItem.LetterFromDomien) &&
                        (mainSlot == Item.Maul || mainSlot == Item.ReplicaMaul))
                    {
                        return QuestReply.ProgressEvent(57);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 57, q => q.Player.GiveKeyItem(KeyItem.LetterFromDomien))
                .OnEventFinish(Zone.PortBastok, 58, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.LetterFromDomien);
                    }
                });
        }
    }
}
