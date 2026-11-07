// Full Speed Ahead! (Log ID 3, Quest ID 179), ported from the Full Speed Ahead! branches of
// scripts/zones/Upper_Jeuno/npcs/Mapitoto.lua (quest rework B3b, Jeuno group 20; call-only: his script now calls this
// quest at the branches' spot). Mapitoto : !pos -54.310 8.200 85.940 244.
// Mapitoto: completed: 10226. Else the char var [QUEST]FullSpeedAhead 4 (the race won), whatever the status: 10225
// (Trainer's Whistle, 15533, ID.npc.MAPITOTO) (its finish: the var 0, completed, fame +7 in San d'Oria, Bastok and
// Windurst, Trainer's Whistle and Raptor Companion by npcUtil.giveKeyItem). Accepted: 10224 (1). Available with the
// Chocobo License and the Map of the Jeuno Area at main level 20 or more: 10223 (0, 0, 4). Else nothing (his default
// 10222 plays). 10223 / 10224 option 1: begun (addQuest: again when accepted), the var 1, setPos(475, 8.8, -159, 128,
// Batallia Downs): the race starts at the zone-in; 10223 option 2: begun.
// The race (scripts/quests/full_speed_ahead.lua, a helper with no Quest:new container; Batallia Downs Zone.lua's zone-in,
// trigger areas and events 24 / 26; effects/full_speed_ahead.lua) moves the var 1 / 2 -> 3 -> 4 and never names the
// quest: it stays in Lua. Mapitoto's mount trades (the Trainer's Whistle held: 10227 and its finish) never name the
// quest: they stay in his script.
// Kept as the Lua has it: 10225 plays at the var 4 whatever the status (before the accepted arm); 10224 option 1 begins
// an accepted quest again; no finish reads the status (10223 option 2 over a completed quest sets its current bit too;
// 10225 completes from any status).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class FullSpeedAhead : QuestScript
    {
        const string Var = "[QUEST]FullSpeedAhead";

        public FullSpeedAhead() : base(QuestLog.Jeuno, "FULL_SPEED_AHEAD") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Mapitoto.lua onTrigger: completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Mapitoto).Called().OnTrigger(QuestReply.Event(10226));

            // the var 4 (the race won), any other status
            b.AnyState(when: q => q.Player.CharVar(Var) == 4)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Mapitoto).Called().OnTrigger(q =>
                    QuestReply.Event(10225, KeyItem.TrainersWhistle, Item.ChocoboWhistle, q.Player.NpcIdOf("MAPITOTO")));

            // accepted (retry)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Mapitoto).Called().OnTrigger(QuestReply.Event(10224, 1));

            // available: the Chocobo License, main level 20 or more, the Map of the Jeuno Area
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasKeyItem(KeyItem.ChocoboLicense) &&
                    q.Player.MainLevel >= 20 &&
                    q.Player.HasKeyItem(KeyItem.MapOfTheJeunoArea))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Mapitoto).Called().OnTrigger(QuestReply.Event(10223, 0, 0, 4));

            b.AnyState()
                // Mapitoto.lua onEventFinish 10223 / 10224 / 10225
                .OnEventFinish(Zone.UpperJeuno, 10223, q =>
                {
                    if (q.Option == 1)
                    {
                        StartRace(q);
                    }
                    else if (q.Option == 2)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10224, q =>
                {
                    if (q.Option == 1)
                    {
                        StartRace(q);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10225, q =>
                {
                    q.Player.SetCharVar(Var, 0);
                    // the raw player:completeQuest, which keeps the quest's own Quest[3][179] vars
                    q.Player.CompleteQuest(QuestLog.Jeuno, "FULL_SPEED_AHEAD");
                    q.Player.AddFame(FameArea.Sandoria, 7);
                    q.Player.AddFame(FameArea.Bastok, 7);
                    q.Player.AddFame(FameArea.Windurst, 7);
                    q.Player.GiveKeyItem(KeyItem.TrainersWhistle);
                    q.Player.GiveKeyItem(KeyItem.RaptorCompanion);
                });
        }

        // 10223 / 10224 option 1: addQuest, the var 1 (the race's flag), setPos to Batallia Downs
        static void StartRace(IQuestContext q)
        {
            q.Quest.Begin();
            q.Player.SetCharVar(Var, 1);
            q.Player.SetPos(475, 8.8, -159, 128, Zone.BatalliaDowns);
        }
    }
}
