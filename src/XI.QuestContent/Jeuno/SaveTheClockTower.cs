// Save the Clock Tower (Log ID 3, Quest ID 3), a line-for-line port of scripts/quests/jeuno/Save_the_Clock_Tower.lua
// (quest rework B3b). Derrick : !pos -32 -1 -7 245. A Clock Most Delicate completed and Jeuno fame level 5: Derrick's
// 230 (8, or 10 with the Airship Pass; 10) gives the Clock Tower Petition on option 20 and begins it (not when the
// petition cannot be given). Accepted: ten NPCs sign the petition, each once (bit n of Prog): a petition traded alone to
// one whose bit is clear plays its event with 9 minus the bits set; the event's finish sets the bit (the petition is
// not taken). Derrick's 230 (4, or 6 with the pass) gives a new petition on option 30 and clears Prog (a petition
// still held is rare: the give fails and Prog stays). The petition with all ten bits to Derrick plays 231: completed
// (Clock Tower Preservationist, fame +17 in San d'Oria, Bastok and Windurst), the petition taken (confirmTrade).
// Collet answers 164 while it is offered, Souren 120 while accepted and 181 (replaceDefault) once completed.
// Lua: petitionNpcData[npc:getName()] picks the bit and csid by name; here each NPC's handlers carry their own.
using System.Numerics;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class SaveTheClockTower : QuestScript
    {
        public SaveTheClockTower() : base(QuestLog.Jeuno, "SAVE_THE_CLOCK_TOWER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Save_the_Clock_Tower";

        // utils.mask.countBits(quest:getVar(player, 'Prog'))
        static int CountBits(int mask) => BitOperations.PopCount((uint)mask);

        // petitionOnTrade, for the NPC whose petitionNpcData entry is { bit, csid }
        static System.Func<IQuestContext, QuestReply> PetitionOnTrade(int bit, int csid) => q =>
        {
            // npcUtil.tradeHasExactly
            if (q.Trade.HasExactly(Item.ClockTowerPetition) && !q.Quest.IsVarBitsSet("Prog", bit))
            {
                return QuestReply.ProgressEvent(csid, 9 - CountBits(q.Quest.Step));
            }
            return null;
        };

        // petitionOnEventFinish
        static System.Action<IQuestContext> PetitionOnEventFinish(int bit) => q => q.Quest.SetVarBit("Prog", bit);

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.ClockTowerPreservationist });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "A_CLOCK_MOST_DELICATE") &&
                    q.Player.FameLevel(FameArea.Jeuno) >= 5)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Derrick).OnTrigger(q =>
                {
                    int hasAirshipPass = q.Player.HasKeyItem(KeyItem.AirshipPass) ? 2 : 0;

                    return QuestReply.ProgressEvent(230, 8 + hasAirshipPass, 10);
                })
                .OnEventFinish(Zone.LowerJeuno, 230, q =>
                {
                    if (q.Option == 20 && q.Player.GiveItem(Item.ClockTowerPetition))
                    {
                        q.Quest.Begin();
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Collet).OnTrigger(QuestReply.Event(164));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Derrick)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly
                        if (q.Trade.HasExactly(Item.ClockTowerPetition) && CountBits(q.Quest.Step) == 10)
                        {
                            return QuestReply.ProgressEvent(231);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int hasAirshipPass = q.Player.HasKeyItem(KeyItem.AirshipPass) ? 2 : 0;

                        return QuestReply.ProgressEvent(230, 4 + hasAirshipPass, 10);
                    })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).OnTrade(PetitionOnTrade(1, 74))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Zauko).OnTrade(PetitionOnTrade(2, 50))
                .OnEventFinish(Zone.LowerJeuno, 50, PetitionOnEventFinish(2))
                .OnEventFinish(Zone.LowerJeuno, 74, PetitionOnEventFinish(1))
                .OnEventFinish(Zone.LowerJeuno, 230, q =>
                {
                    // TODO (Lua): Verify if petition should be reset if the player still has one in their inventory.
                    if (q.Option == 30 && q.Player.GiveItem(Item.ClockTowerPetition))
                    {
                        q.Quest.SetStep(0);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 231, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 17);
                        q.Player.AddFame(FameArea.Bastok, 17);
                        q.Player.AddFame(FameArea.Windurst, 17);
                        q.Trade.Confirm();
                    }
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Pitantimand).OnTrade(PetitionOnTrade(0, 64))
                .OnEventFinish(Zone.PortJeuno, 64, PetitionOnEventFinish(0))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Baudin).OnTrade(PetitionOnTrade(3, 177))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Collet).OnTrade(PetitionOnTrade(4, 115))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Constance).OnTrade(PetitionOnTrade(5, 180))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).OnTrade(PetitionOnTrade(6, 91))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Zuber).OnTrade(PetitionOnTrade(8, 126))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Souren)
                    .OnTrade(PetitionOnTrade(7, 182))
                    .OnTrigger(QuestReply.Event(120))
                .OnEventFinish(Zone.UpperJeuno, 91, PetitionOnEventFinish(6))
                .OnEventFinish(Zone.UpperJeuno, 115, PetitionOnEventFinish(4))
                .OnEventFinish(Zone.UpperJeuno, 126, PetitionOnEventFinish(8))
                .OnEventFinish(Zone.UpperJeuno, 177, PetitionOnEventFinish(3))
                .OnEventFinish(Zone.UpperJeuno, 180, PetitionOnEventFinish(5))
                .OnEventFinish(Zone.UpperJeuno, 182, PetitionOnEventFinish(7))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Radeivepart).OnTrade(PetitionOnTrade(9, 160))
                .OnEventFinish(Zone.RuludeGardens, 160, PetitionOnEventFinish(9));

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Souren).OnTrigger(QuestReply.Event(181).ReplaceDefault());
        }
    }
}
