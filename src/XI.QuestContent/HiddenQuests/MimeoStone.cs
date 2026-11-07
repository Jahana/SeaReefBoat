// Mimeo Stone (hidden quest mimeoStone), ported from scripts/quests/hiddenQuests/Mimeo_Stone.lua (quest rework B3b,
// Deferred 1). Attohwa Chasm: the six Luminants (!pos 241.833 19.009 -152.072; !pos 444.687 19.697 -172.114;
// !pos 514.630 19.328 -79.209; !pos 275.930 19.229 153.760; !pos 197.387 19.977 -56.561; !pos 442.997 19.500 -34.534),
// the Cradle of Rebirth (!pos 319.958 -25.652 -11.895).
// Section 1 (no Mimeo Stone): a Luminant gives the stone (npcUtil.giveKeyItem), MIMEO_STONE_PICKUP, the zone global
// handleLuminantUsed(npc) (the Luminant goes CUTSCENE_ONLY; a timer of its own wakes a used one), the locals NumFades
// and Progress 0, and a 50-90 s player timer (stoneTimer); nil. stoneTimer (a P10 callback, the quest's: it runs
// whatever the section): no stone or Progress 1: stop; the fifth fade: MIMEO_JEWEL_OFFSET + 4, the stone taken; else
// NumFades + 1, MIMEO_JEWEL_OFFSET + fades - 1, and itself armed again in 50-90 s.
// Section 2 (the stone held): a Luminant: NOTHING_OUT_OF_ORDINARY, nil. The Cradle, Progress 0:
// MIMEO_STONE_BRIGHTNESS_OFFSET + fades; the reward (Regain, Rebirth, Revive, Blaze, Fire Feather by fades 0-4) already
// held: CANNOT_OBTAIN_ITEM, noAction; no free slot: ITEM_CANNOT_BE_OBTAINED (npcUtil.giveItem's), noAction; else the
// next NPC's 'krtu' animation, Progress 1, 2; 2's finish: the stone taken, the reward. Leaving the zone:
// MIMEO_JEWEL_OFFSET + 4, the stone taken.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the Cradle's room test ("Make sure player
// has room for reward") also wants a free slot, refusing with the message npcUtil.giveItem gave the Lua's full bag; the
// Lua's tested hasItem alone, so with a full bag 2 played and its finish took the stone while giveItem refused the
// reward.
// Kept as the Lua has it: the Cradle's arm tests the stone again (the section's check holds it); the onZoneOut message
// and delete run with the stone held only (section 2); the reward is given by npcUtil.giveItem without a check of its
// result. stoneTimer's `not player` test has no C# form: the callback's context always has its char (a zone change or
// a logout destroys the char's entity and clears its timers with it, ~CCharEntity; ZoneHost ClearActionQueues, on both
// sides).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class MimeoStone : HiddenQuestScript
    {
        public MimeoStone() : base("mimeoStone") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Mimeo_Stone";

        private static readonly int[] Rewards =
        {
            Item.RegainFeather,
            Item.RebirthFeather,
            Item.ReviveFeather,
            Item.BlazeFeather,
            Item.FireFeather,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            // stoneTimer(player), armed by
            // player:timer(math.randomInt(50, 90) * 1000, function() stoneTimer(player) end)
            QuestCallback stoneTimer = null;
            stoneTimer = b.Callback("stoneTimer", q =>
            {
                // Once the animation at the Cradle has started, the reward has already been locked in
                // so don't continue the timer after this point
                if (!q.Player.HasKeyItem(KeyItem.MimeoStone) ||
                    q.Quest.LocalVar("Progress") == 1)
                {
                    return;
                }

                int numFades = q.Quest.LocalVar("NumFades") + 1;
                if (numFades == 5)
                {
                    q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_JEWEL_OFFSET) + 4, KeyItem.MimeoStone);
                    q.Player.DelKeyItem(KeyItem.MimeoStone);
                    return;
                }

                q.Quest.SetLocalVar("NumFades", numFades);

                q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_JEWEL_OFFSET) + numFades - 1, KeyItem.MimeoStone);

                // Captures show ranges from 49 to 88, but it may not necessarily be purely random
                // For Luminant Offset+3, all times (12 recorded) were below 60s except 1
                // For all other Luminants, there were no times below 60s (14 recorded)
                // There could be some subtle differences based on the Luminant distance to the Cradle,
                // but unless we get a hundred more times captured this is probably the best we can do.
                q.Player.Timer(q.Player.RandomInt(50, 90) * 1000, stoneTimer);
            });

            b.AnyState(when: q => !q.Player.HasKeyItem(KeyItem.MimeoStone))
                .Npc(Zone.AttohwaChasm, Npc.AttohwaChasm.Luminant).OnTrigger(q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MimeoStone);
                    q.Text.Special(Text.AttohwaChasm.MIMEO_STONE_PICKUP, KeyItem.MimeoStone);

                    q.Npc.AttohwaChasmHandleLuminantUsed();

                    q.Quest.SetLocalVar("NumFades", 0);
                    q.Quest.SetLocalVar("Progress", 0);

                    q.Player.Timer(q.Player.RandomInt(50, 90) * 1000, stoneTimer);

                    return null;
                });

            b.AnyState(when: q => q.Player.HasKeyItem(KeyItem.MimeoStone))
                .Npc(Zone.AttohwaChasm, Npc.AttohwaChasm.Luminant).OnTrigger(q =>
                {
                    q.Text.Special(Text.AttohwaChasm.NOTHING_OUT_OF_ORDINARY);
                    return null;
                })
                .Npc(Zone.AttohwaChasm, Npc.AttohwaChasm.CradleofRebirth).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MimeoStone) &&
                        q.Quest.LocalVar("Progress") == 0)
                    {
                        int numFades = q.Quest.LocalVar("NumFades");
                        q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_STONE_BRIGHTNESS_OFFSET) + numFades,
                                       KeyItem.MimeoStone);

                        // Make sure player has room for reward, otherwise timer keeps running
                        int reward = Rewards[numFades];
                        if (q.Player.HasItem(reward))
                        {
                            q.Text.Special(Text.AttohwaChasm.CANNOT_OBTAIN_ITEM, reward);
                            return QuestReply.NoAction();
                        }

                        // no free slot: npcUtil.giveItem's message (fixed, user ruling 2026-11-04)
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.AttohwaChasm.ITEM_CANNOT_BE_OBTAINED, reward);
                            return QuestReply.NoAction();
                        }

                        // GetNPCByID(npc:getID() + 1), when there is one
                        var animationNpc = q.Player.NpcById(q.Npc.Id + 1);
                        if (animationNpc != null)
                        {
                            animationNpc.EntityAnimationPacket("krtu");
                        }

                        q.Quest.SetLocalVar("Progress", 1);
                        return QuestReply.ProgressEvent(2);
                    }

                    return null;
                })
                .OnZoneOut(Zone.AttohwaChasm, q =>
                {
                    q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_JEWEL_OFFSET) + 4, KeyItem.MimeoStone);
                    q.Player.DelKeyItem(KeyItem.MimeoStone);
                })
                .OnEventFinish(Zone.AttohwaChasm, 2, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MimeoStone);
                    int numFades = q.Quest.LocalVar("NumFades");
                    q.Player.GiveItem(Rewards[numFades]);
                });
        }
    }
}
