// Recollections (Log ID 2, Quest ID 67; BLM AF2), ported from the Recollections branches of
// scripts/zones/Heavens_Tower/npcs/Chumimi.lua, scripts/zones/RuLude_Gardens/npcs/Laityn.lua and
// scripts/zones/Castle_Zvahl_Keep/npcs/_4i5.lua (quest rework B3b, Windurst group 30; call-only).
// Chumimi : !pos 0.1 30 21 242; the Ore Door (_4i5) : !pos -14 0 69 162.
// Chumimi, after The Three Magi's arms (its own C#, which her script calls first): The Three Magi completed, this quest
// available and not (BLM at level >= AF2_QUEST_LEVEL): 268; The Three Magi completed, BLM, level >= AF2_QUEST_LEVEL,
// no needToZone, available: 270 (0, Bag of Seeds); its finish, any option: begun. Accepted with Foe Finder MK I: 275;
// its finish: npcUtil.giveItem (Wizard's Sabots); given: recollectionsQuest 0, the key item taken, Windurst fame 20,
// completed. The trade, accepted, recollectionsQuest < 2: one Bag of Seeds and nothing else: 271 (0, 520); its
// finish: the trade taken, the var 2. The Root of the Problem's arms come after these (its own C#).
// Laityn, accepted and the var 0: 10003 (its finish: the var 1); else his 10006 (his script). The Ore Door's trade,
// accepted and the var 2: one Whine Cellar Key and nothing else: 8 (Whine Cellar Key); its finish: the trade taken,
// the var 3, Foe Finder MK I (npcUtil.giveKeyItem). The door's trigger (9) reads nothing: it stays in its script.
// None of the three has a DefaultActions entry or a container.
// Kept as the Lua has it: 270 begins on any option; the finishes of 271, 275, 10003 and 8 read no status; the
// 268 / 270 arms read no other quest than The Three Magi (completed).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class Recollections : QuestScript
    {
        const string Var = "recollectionsQuest";

        public Recollections() : base(QuestLog.Windurst, "RECOLLECTIONS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Chumimi).Called()
                    // Chumimi.lua onTrade, the Recollections arm
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Player.CharVar(Var) < 2 &&
                            q.Trade.HasItemQty(Item.BagOfSeeds, 1) &&
                            q.Trade.ItemCount == 1)
                        {
                            return QuestReply.Event(271, 0, 520);
                        }

                        return null; // The Root of the Problem's arm next
                    })
                    // Chumimi.lua onTrigger, the Recollections arms
                    .OnTrigger(q =>
                    {
                        QuestStatus theThreeMagi = q.Player.QuestStatus(QuestLog.Windurst, "THE_THREE_MAGI");
                        QuestStatus recollections = q.Quest.Status;
                        int mLvl = q.Player.MainLevel;
                        int mJob = q.Player.MainJob;

                        if (theThreeMagi == QuestStatus.Completed &&
                            recollections == QuestStatus.Available &&
                            ((mJob == Job.Blm && mLvl < q.Settings.MainNumber("AF2_QUEST_LEVEL")) || mJob != Job.Blm))
                        {
                            return QuestReply.Event(268); // New standard dialog after "The Three Magi"
                        }
                        else if (theThreeMagi == QuestStatus.Completed &&
                            mJob == Job.Blm &&
                            mLvl >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                            !q.Player.NeedToZone &&
                            recollections == QuestStatus.Available)
                        {
                            return QuestReply.Event(270, 0, Item.BagOfSeeds); // Start Quest "Recollections"
                        }
                        else if (recollections == QuestStatus.Accepted &&
                            q.Player.HasKeyItem(KeyItem.FoeFinderMkI))
                        {
                            return QuestReply.Event(275); // Finish Quest "Recollections"
                        }

                        return null; // The Root of the Problem's arms next
                    })
                // Laityn.lua onTrigger (his 10006 stays in his script)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Laityn).Called()
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Var) == 0)
                        {
                            return QuestReply.Event(10003); // Option CS for "Recollections"
                        }

                        return null;
                    })
                // _4i5.lua onTrade (the Ore Door)
                .Npc(Zone.CastleZvahlKeep, Npc.CastleZvahlKeep._4i5).Called()
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Var) == 2)
                        {
                            if (q.Trade.HasItemQty(Item.WhineCellarKey, 1) && q.Trade.ItemCount == 1)
                            {
                                return QuestReply.Event(8, Item.WhineCellarKey);
                            }
                        }

                        return null;
                    });

            b.AnyState()
                // Chumimi.lua onEventFinish, the Recollections arms
                .OnEventFinish(Zone.HeavensTower, 270, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.HeavensTower, 271, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar(Var, 2);
                })
                .OnEventFinish(Zone.HeavensTower, 275, q =>
                {
                    if (q.Player.GiveItem(Item.WizardsSabots))
                    {
                        q.Player.SetCharVar(Var, 0);
                        q.Player.DelKeyItem(KeyItem.FoeFinderMkI);
                        q.Player.AddFame(FameArea.Windurst, 20);
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][67] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "RECOLLECTIONS");
                    }
                })
                // Laityn.lua onEventFinish
                .OnEventFinish(Zone.RuludeGardens, 10003, q =>
                {
                    q.Player.SetCharVar(Var, 1);
                })
                // _4i5.lua onEventFinish
                .OnEventFinish(Zone.CastleZvahlKeep, 8, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar(Var, 3);
                    q.Player.GiveKeyItem(KeyItem.FoeFinderMkI);
                });
        }
    }
}
