// Altana's Sorrow (Log ID 1, Quest ID 49), a line-for-line port of scripts/quests/bastok/Altanas_Sorrow.lua (quest rework
// B3b). Virnage : !pos 0 0 51 234; qm1 : !pos -282.339 0.001 261.707 200; Eperdur : !pos 129 -6 96 231. Virnage's 141
// (main level 10, Bastok fame level 4) begins it on option 0; Garlaige Citadel's qm1 gives the KI Bucket of Divine Paint;
// Virnage's 143 trades it for the KI Letter from Virnage; Eperdur's 679 completes it (Scroll of Teleport-Dem, Pilgrim to
// Dem, fame +40) and deletes the letter. Once completed Virnage plays 145 (replaceDefault). A Question of Faith (C#) also
// names Virnage.
// Kept as the Lua file has it: Virnage's accepted handler starts its events with player:startEvent and returns nothing,
// so the lookup sees no action from it and goes on to his DefaultActions 140, which the engine queues behind the open
// event: 142 / 143 / 144, then 140 when that one ends (bastok.altanas_sorrow's traces). Player.StartEvent and a null
// reply keep that; a QuestReply would be the lookup's action and end it, with no 140.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class AltanasSorrow : QuestScript
    {
        public AltanasSorrow() : base(QuestLog.Bastok, "ALTANAS_SORROW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Altanas_Sorrow";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 40,
                FameArea = FameArea.Bastok,
                Item = Item.ScrollOfTeleportDem,
                Title = Title.PilgrimToDem,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 10 &&
                    q.Player.FameLevel(FameArea.Bastok) >= 4)
                .Npc(Zone.BastokMines, Npc.BastokMines.Virnage).OnTrigger(QuestReply.ProgressEvent(141))
                .OnEventFinish(Zone.BastokMines, 141, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Virnage).OnTrigger(q =>
                {
                    // player:startEvent, then nil: the Lua starts the event itself and the lookup goes on to Virnage's default
                    // 140, which the engine queues behind it. A QuestReply would be the lookup's action and end it (no 140)
                    if (q.Player.HasKeyItem(KeyItem.BucketOfDivinePaint))
                    {
                        q.Player.StartEvent(143);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.LetterFromVirnage))
                    {
                        q.Player.StartEvent(144);
                    }
                    else
                    {
                        q.Player.StartEvent(142);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 143, q =>
                {
                    q.Player.DelKeyItem(KeyItem.BucketOfDivinePaint);
                    q.Player.GiveKeyItem(KeyItem.LetterFromVirnage);
                })
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm1).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.BucketOfDivinePaint) && !q.Player.HasKeyItem(KeyItem.LetterFromVirnage)
                        ? QuestReply.KeyItem(KeyItem.BucketOfDivinePaint)
                        : null)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Eperdur).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.LetterFromVirnage) ? QuestReply.ProgressEvent(679) : null)
                .OnEventFinish(Zone.NorthernSanDoria, 679, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.LetterFromVirnage);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Virnage).OnTrigger(QuestReply.Event(145).ReplaceDefault());
        }
    }
}
