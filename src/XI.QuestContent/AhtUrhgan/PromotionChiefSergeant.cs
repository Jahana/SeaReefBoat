// Promotion: Chief Sergeant (Log ID 6, Quest ID 96), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_Chief_Sergeant.lua (quest rework B3b Aht Urhgan, group 4). Aht Urhgan Whitegate:
// Abquhbah !pos 35 -6 -58 (his script: 255), Hagakoff !pos 82 0 -74 (his script: HAGAKOFF_SHOP_DIALOG and the besieged
// shop). Aydeewa Subterrane: the six Mushroom Patches (one name; no script; default NOTHING_OUT_OF_ORDINARY).
// Vars: Prog, MPatch<n> (patch n: 0 none, 1 harvested, 2 planted, 3 picked), MPatch_Time<n> (the day patch n is ready),
// Option (the sprouts carried, 1-5), Stage (the sprouts polished so far).
// Available with AssaultPromotion >= 25 (player:getVar) and Promotion: Sergeant Major completed: Abquhbah 5060, whose
// finish begins it. Prog 0: Abquhbah 5062 once per zone; Hagakoff 5067 (Prog 1). Prog 1: Abquhbah 5068 (Prog 2). Prog
// 2: Abquhbah 5069 once per zone; Hagakoff 5063 (Prog 3). Prog 3: a patch: no Scourshroom and untouched: 14 (harvested:
// the Scourshroom, no message); harvested: 15; the Scourshroom and untouched: 19 (option 0: planted, ready tomorrow,
// needToZone, the Scourshroom taken); planted: ready (its MPatch_Time<n> <= today) and no needToZone: 21 ([0]
// the hour bonus 1 or 2, [3] 0 or 5: random draws), else 20; picked: 18. 21's finish (option not 100): picked,
// Option = clamp(option + Option, 1, 5), with NO_MORE_SPROUTS and NO_MORE_SPROUTS + 1 (0, 5) past 5. Abquhbah with
// Option > 0: 5061 ([0] = Stage, [1] = Option), whose finish: option < 11: Stage += Option, Option 0, every patch
// reset; option >= 20: (option >= 30 with under two free slots: ITEM_CANNOT_BE_OBTAINED + 4, nothing more) completes
// it (an Imperial Mythril Piece, the CS Wildcat Badge, Chief Sergeant), at option >= 30 an Imperial Gold Piece, then
// AssaultPromotion 0, PROMOTION_CHIEF_SERGEANT and the Scourshroom taken; options 11 to 19: nothing. With Option 0:
// nil (his script's 255).
// Deliberate changes from the Lua (user ruling 2026-11-04, kept Lua bugs): a planted patch reads its own
// MPatch_Time<n>, which 19 sets to tomorrow; the Lua read MPatch_Time (no patch number), which nothing sets, so a patch
// was ready as soon as the char had zoned. The NO_MORE_SPROUTS messages of 21's finish and 5061's
// ITEM_CANNOT_BE_OBTAINED + 4 are sent; the Lua's were quest:messageSpecial actions, built in a finish and dropped.
// Kept as the Lua has it: the 21 bonus draw (randomInt(1, 100)) is made every time, the hour draw only from 04:00
// to 06:59; the last branch of triggerPatch (16) is unreachable (19 takes its case first); the completion takes the
// Scourshroom, not the SM badge.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionChiefSergeant : QuestScript
    {
        public PromotionChiefSergeant() : base(QuestLog.AhtUrghan, "PROMOTION_CHIEF_SERGEANT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Chief_Sergeant";

        // local patchStage
        const int PatchNone = 0, PatchHarvested = 1, PatchPlanted = 2, PatchPicked = 3;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialMythrilPiece,
                KeyItem = KeyItem.CsWildcatBadge,
                Title = Title.ChiefSergeant,
            });
            b.Steps(1, 2, 3);

            // Section 1: available, AssaultPromotion >= 25, Sergeant Major completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_SERGEANT_MAJOR"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.ProgressEvent(5060))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5060, q => q.Quest.Begin());

            // Section 2: started, a reminder; Hagakoff to progress
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.ProgressEvent(5062).OncePerZone())
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Hagakoff)
                    .OnTrigger(q => QuestReply.ProgressEvent(5067))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5067, q => q.Quest.SetStep(1));

            // Section 3: told by Hagakoff to go back to Abquhbah
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.ProgressEvent(5068))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5068, q => q.Quest.SetStep(2));

            // Section 4: told by Abquhbah to go back to Hagakoff
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.ProgressEvent(5069).OncePerZone())
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Hagakoff)
                    .OnTrigger(q => QuestReply.ProgressEvent(5063))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5063, q => q.Quest.SetStep(3));

            // Section 5: the mushrooms
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.MushroomPatch).OnTrigger(TriggerPatch)
                .OnEventFinish(Zone.AydeewaSubterrane, 14, q =>
                {
                    int mushroomPatchOffset = PatchOffset(q);

                    q.Quest.SetVar("MPatch" + mushroomPatchOffset, PatchHarvested);
                    q.Player.AddKeyItem(KeyItem.Scourshroom);
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 19, q =>
                {
                    if (q.Option == 0)
                    {
                        int mushroomPatchOffset = PatchOffset(q);

                        q.Quest.SetVar("MPatch" + mushroomPatchOffset, PatchPlanted);
                        q.Quest.SetVar("MPatch_Time" + mushroomPatchOffset, q.Player.VanadielUniqueDay + 1);
                        q.Player.SetNeedToZone(true);
                        q.Player.DelKeyItem(KeyItem.Scourshroom);
                    }
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 21, q =>
                {
                    if (q.Option != 100)
                    {
                        int mushrooms = q.Quest.Var("Option");
                        int mushroomPatchOffset = PatchOffset(q);
                        int amount = q.Option + mushrooms;

                        if (amount > 5)
                        {
                            // the Lua's two quest:messageSpecial actions were dropped (fixed, user ruling 2026-11-04)
                            q.Text.Special(Text.AydeewaSubterrane.NO_MORE_SPROUTS);
                            q.Text.Special(q.Text.Id(Text.AydeewaSubterrane.NO_MORE_SPROUTS) + 1, 0, 5);
                        }

                        q.Quest.SetVar("MPatch" + mushroomPatchOffset, PatchPicked);
                        q.Quest.SetVar("Option", System.Math.Clamp(amount, 1, 5)); // utils.clamp(amount, 1, 5)
                    }
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int mushrooms = q.Quest.Var("Option");
                    int polished = q.Quest.Var("Stage");

                    if (mushrooms > 0)
                    {
                        return QuestReply.ProgressEvent(5061).Option(0, polished).Option(1, mushrooms);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5061, q =>
                {
                    if (q.Option < 11)
                    {
                        int polished = q.Quest.Var("Stage");

                        q.Quest.SetVar("Stage", q.Quest.Var("Option") + polished);
                        q.Quest.SetVar("Option", 0);
                        UnsetPatches(q);
                    }
                    else if (q.Option >= 20)
                    {
                        if (q.Option >= 30 && q.Player.FreeSlotCount < 2)
                        {
                            // the Lua's quest:messageSpecial: built and dropped (fixed, user ruling 2026-11-04)
                            q.Text.Special(q.Text.Id(Text.AhtUrhganWhitegate.ITEM_CANNOT_BE_OBTAINED) + 4);
                            return;
                        }

                        if (q.Quest.Complete())
                        {
                            if (q.Option >= 30)
                            {
                                q.Player.GiveItem(Item.ImperialGoldPiece);
                            }

                            q.Player.SetCharVar("AssaultPromotion", 0);
                            q.Text.Special(Text.AhtUrhganWhitegate.PROMOTION_CHIEF_SERGEANT);
                            q.Player.DelKeyItem(KeyItem.Scourshroom);
                        }
                    }
                });
        }

        // local mushroomPatchOffset = npc:getID() - aydeewaID.npc.MUSHROOM_PATCH (GetFirstID('Mushroom_Patch'))
        static int PatchOffset(IQuestContext q) => q.Npc.Id - q.Player.NpcIdOf("MUSHROOM_PATCH");

        // triggerPatch
        static QuestReply TriggerPatch(IQuestContext q)
        {
            int mushroomPatchOffset = PatchOffset(q);
            string patch = "MPatch" + mushroomPatchOffset;
            int mushroomVar = q.Quest.Var(patch);
            var result = QuestReply.MessageSpecial(Text.AydeewaSubterrane.NOTHING_OUT_OF_ORDINARY);
            int time = q.Player.VanadielHour;

            // Don't have the KI, have not picked or planted at the patch
            if (!q.Player.HasKeyItem(KeyItem.Scourshroom) &&
                mushroomVar == PatchNone)
            {
                return QuestReply.ProgressEvent(14);
            }
            // a patch already picked from
            else if (mushroomVar == PatchHarvested)
            {
                return QuestReply.ProgressEvent(15);
            }
            // planting: the KI, the patch not yet touched
            else if (q.Player.HasKeyItem(KeyItem.Scourshroom) &&
                     mushroomVar == PatchNone)
            {
                return QuestReply.ProgressEvent(19);
            }
            // planted a mushroom
            else if (mushroomVar == PatchPlanted)
            {
                // Ready to harvest: the patch's own MPatch_Time<n> (the Lua read 'MPatch_Time', no patch number:
                // fixed, user ruling 2026-11-04)
                if (q.Quest.Var("MPatch_Time" + mushroomPatchOffset) <= q.Player.VanadielUniqueDay &&
                    !q.Player.NeedToZone)
                {
                    int timeBonus = 1;
                    int bonus = q.Player.RandomInt(1, 100) >= 75 ? 5 : 0;
                    if (time >= 4 && time <= 6 && q.Player.RandomInt(1, 100) >= 50)
                    {
                        timeBonus = 2;
                    }

                    return QuestReply.ProgressEvent(21).Option(0, timeBonus).Option(3, bonus);
                }
                // Not ready to harvest
                else
                {
                    return QuestReply.ProgressEvent(20);
                }
            }
            // Picked already
            else if (mushroomVar == PatchPicked)
            {
                return QuestReply.ProgressEvent(18);
            }
            // the KI, not harvested already (unreachable: the planting branch takes this case first)
            else if (q.Player.HasKeyItem(KeyItem.Scourshroom) &&
                     mushroomVar == PatchNone)
            {
                return QuestReply.ProgressEvent(16);
            }

            return result;
        }

        // unsetPatches
        static void UnsetPatches(IQuestContext q)
        {
            for (int i = 0; i <= 5; i++)
            {
                string patch = "MPatch" + i;
                string patchTime = "MPatch_Time" + i;
                q.Quest.SetVar(patch, 0);
                q.Quest.SetVar(patchTime, 0);
            }
        }
    }
}
