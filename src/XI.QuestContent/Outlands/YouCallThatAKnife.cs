// You Call That a Knife (Log ID 5, Quest ID 6), a line-for-line port of scripts/quests/outlands/You_Call_That_a_Knife.lua
// (quest rework B3b Outlands, group 2). Kazham: Mhebi Juhbily !pos 40 -11 -161 (no script; default 125), Pula Rhatti !pos -18
// -4 -38.5 (no script; default 92), Vah Keshura !pos 28 -8.5 -106 (his script: BathedInScent only; default 124), the shed
// _6y9 !pos 39 -12 -167 (no script; default 126). Temple of Uggalepih: Chef Nonberry !pos -135 0 -92 (no script; default
// NOTHING_I_CANT_CUT), the four cooks (COOK_OFFSET .. + 3).
// Available at Windurst fame level 6: a Sandfish alone traded to Mhebi at Prog 0 plays 127 (0, Sandfish), whose option 511
// sets Prog 1 and takes the fish; at Prog 1 Mhebi plays 318 and the shed _6y9 128, whose option 1 begins it. Pula plays
// 269 (0, Sandfish) while available and accepted. Accepted without Nonberry's Knife: Mhebi 129; Vah 130 at Prog 1 (Prog 2)
// else 131; Chef Nonberry: a Tonberry Board alone plays 27 (confirmed, the knife), any other trade 28, which takes the whole
// trade and, while GetSystemTime() is at or past his local var cooldown, pops the four cooks (claimed, the NPC not hidden)
// and on that sets cooldown now + 600. With the knife: Mhebi 133 completes it (Windurst fame 60, 7200 gil, Ya Done Good),
// then the knife taken and the mustZone; Vah 132. Completed: Mhebi 134 while the mustZone is set, else 135.
// Kept as the Lua has it: Chef Nonberry reads no Prog (his trades work at Prog 1 and 2); 28 takes the trade whatever it
// holds, and takes it even when no cook pops; the cooldown is set only when popFromQM pops (a cook up: unchanged);
// 133's quest:complete has no item (always true).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class YouCallThatAKnife : QuestScript
    {
        public YouCallThatAKnife() : base(QuestLog.Outlands, "YOU_CALL_THAT_A_KNIFE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/You_Call_That_a_Knife";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 60,
                FameArea = FameArea.Windurst,
                Gil = 7200,
                Title = Title.YaDoneGood,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 6)
                .Npc(Zone.Kazham, Npc.Kazham.MhebiJuhbily)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 0 &&
                            q.Trade.HasExactly(Item.Sandfish))
                        {
                            return QuestReply.ProgressEvent(127, 0, Item.Sandfish);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.Event(318);
                        }
                        return null;
                    })
                .Npc(Zone.Kazham, Npc.Kazham.PulaRhatti).OnTrigger(QuestReply.Event(269, 0, Item.Sandfish))
                .Npc(Zone.Kazham, Npc.Kazham._6y9).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(128);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Kazham, 127, q =>
                {
                    if (q.Option == 511)
                    {
                        q.Quest.SetStep(1);
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.Kazham, 128, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.NonberrysKnife))
                .Npc(Zone.Kazham, Npc.Kazham.MhebiJuhbily).OnTrigger(QuestReply.Event(129))
                .Npc(Zone.Kazham, Npc.Kazham.PulaRhatti).OnTrigger(QuestReply.Event(269, 0, Item.Sandfish))
                .Npc(Zone.Kazham, Npc.Kazham.VahKeshura).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(130);
                    }
                    else
                    {
                        return QuestReply.Event(131);
                    }
                })
                .OnEventFinish(Zone.Kazham, 130, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.ChefNonberry).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.TonberryBoard))
                    {
                        return QuestReply.ProgressEvent(27);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(28);
                    }
                })
                .OnEventFinish(Zone.TempleOfUggalepih, 27, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.NonberrysKnife);
                })
                .OnEventFinish(Zone.TempleOfUggalepih, 28, q =>
                {
                    q.Trade.Complete();
                    // cookMobs = { COOK_OFFSET, + 1, + 2, + 3 }; npcUtil.popFromQM(player, npc, cookMobs, { claim = true,
                    // hide = 0 }): the series form (look false: popFromQM reads params.look as a truth value, absent alike)
                    if (q.Player.SystemTime >= q.Npc.LocalVar("cooldown") &&
                        q.Player.PopFromQmSeries(Mob.TempleOfUggalepih.COOK_OFFSET, 4, 0, true, false))
                    {
                        q.Npc.SetLocalVar("cooldown", q.Player.SystemTime + 600); // 10 minutes between repops
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.NonberrysKnife))
                .Npc(Zone.Kazham, Npc.Kazham.MhebiJuhbily).OnTrigger(QuestReply.ProgressEvent(133))
                .Npc(Zone.Kazham, Npc.Kazham.VahKeshura).OnTrigger(QuestReply.Event(132))
                .OnEventFinish(Zone.Kazham, 133, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.NonberrysKnife);
                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.MustZone)
                .Npc(Zone.Kazham, Npc.Kazham.MhebiJuhbily).OnTrigger(QuestReply.Event(134));

            b.State(QuestStatus.Completed, when: q => !q.Quest.MustZone)
                .Npc(Zone.Kazham, Npc.Kazham.MhebiJuhbily).OnTrigger(QuestReply.Event(135));
        }
    }
}
