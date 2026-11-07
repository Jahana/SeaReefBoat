// A Hard Day's Knight (Log ID 4, Quest ID 64), a line-for-line port of scripts/quests/otherAreas/A_Hard_Days_Knight.lua
// (quest rework B3b, Other Areas group 7). Tavnazian Safehold: Quelveuiat !pos -3.177 -22.750 -25.970 (a script that
// only walks him between two nodes, 35 s at each: no onTrigger; default 122); Lufaise Meadows: qm_hard_days_knight !pos
// -38.605 -9.022 -290.700 (default NOTHING_OUT_OF_ORDINARY) and the NM Splinterspine Grukjuk.
// Available (no other condition): Quelveuiat 119, whose finish begins the quest on option 3. Accepted: Quelveuiat at
// Prog 0: 120 (event); at Prog 1: 121, whose finish runs quest:complete (2100 gil, Tavnazian Squire). The ???,
// Splinterspine not up and Prog below 1: SURVEY_THE_SURROUNDINGS, Splinterspine spawned and claimed, the reply
// MURDEROUS_PRESENCE. Splinterspine's death: Prog 1 (whatever the Prog). Completed: Quelveuiat's trade, without the
// Temple Knight Key: exactly the Sealion Crest Key or exactly the Coral Crest Key: 631 (the item traded:
// trade:getItemId(), slot 0); both: Prog 2, then 631 (both ids); his trigger: 631 with the crest keys found in the
// char's containers (player:findItem, Sealion first). 631 finished at Prog 2: 632 started (player:startEvent); 632
// finished: confirmTrade, Prog 0, the Temple Knight Key.
// Kept as the Lua has it: 121's quest:complete is not read (gil and a title: it cannot fail); the trade's Prog 2 is set
// before 631 plays, so 631 / 632 run whatever the char answers; the completed trigger answers 631 even with the key
// held or no crest key (0, 0); 632's confirmTrade reads the trade of the earlier 631 (the char's trade container: after
// a trigger's 631 it confirms nothing). Its era module (Modules/Era/OtherAreas/AHardDaysKnight.cs) removes the
// completed state before SoA.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class AHardDaysKnight : QuestScript
    {
        public AHardDaysKnight() : base(QuestLog.OtherAreas, "A_HARD_DAYS_KNIGHT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/A_Hard_Days_Knight";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 2100,
                Title = Title.TavnazianSquire,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Quelveuiat).OnTrigger(QuestReply.ProgressEvent(119))
                .OnEventFinish(Zone.TavnazianSafehold, 119, q =>
                {
                    if (q.Option == 3)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Quelveuiat).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(120);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(121);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 121, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.qmharddaysknight).OnTrigger(q =>
                {
                    if (!q.Player.MobIsSpawned(Mob.LufaiseMeadows.SPLINTERSPINE_GRUKJUK) &&
                        q.Quest.Step < 1)
                    {
                        q.Text.Special(Text.LufaiseMeadows.SURVEY_THE_SURROUNDINGS);
                        q.Player.SpawnMob(Mob.LufaiseMeadows.SPLINTERSPINE_GRUKJUK, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.LufaiseMeadows.MURDEROUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.SplinterspineGrukjuk).OnMobDeath(q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Quelveuiat).OnTrade(q =>
                {
                    // TODO: Needs verification for single-trade events
                    if (!q.Player.HasKeyItem(KeyItem.TempleKnightKey))
                    {
                        if (q.Trade.HasExactly(Item.SealionCrestKey) ||
                            q.Trade.HasExactly(Item.CoralCrestKey))
                        {
                            // trade:getItemId(): slot 0
                            return QuestReply.ProgressEvent(631, q.Trade.ItemId(0));
                        }
                        else if (q.Trade.HasExactly((Item.SealionCrestKey, 1), (Item.CoralCrestKey, 1)))
                        {
                            q.Quest.SetStep(2);
                            return QuestReply.ProgressEvent(631, Item.SealionCrestKey, Item.CoralCrestKey);
                        }
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int itemCount = 0;
                    var itemTable = new[] { 0, 0 };

                    for (int itemId = Item.SealionCrestKey; itemId <= Item.CoralCrestKey; itemId++)
                    {
                        if (q.Player.FindItem(itemId))
                        {
                            itemCount = itemCount + 1;
                            itemTable[itemCount - 1] = itemId;
                        }
                    }

                    return QuestReply.ProgressEvent(631, itemTable[0], itemTable[1]);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 631, q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        // TODO: This is most likely not retail accurate, and need to check captures
                        // for forced zoning events.
                        q.Player.StartEvent(632);
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 632, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(0);
                    q.Player.GiveKeyItem(KeyItem.TempleKnightKey);
                });
        }
    }
}
