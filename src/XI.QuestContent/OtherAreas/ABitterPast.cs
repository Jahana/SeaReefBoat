// A Bitter Past (Log ID 4, Quest ID 66), a line-for-line port of scripts/quests/otherAreas/A_Bitter_Past.lua (quest rework
// B3b). Tavnazian Safehold: Frescheque !pos 18 -36 12 (two of the name, 16883771 and 16883772; default 158), Raminey
// !pos 82 -35 50 (default 159), Equette !pos 3 -22 -17 (default 162), no scripts; Lufaise Meadows: qm_bitter_past !pos 58 -7
// 27 (default NOTHING_OUT_OF_ORDINARY) and the NMs Blackbone Frazdiz and Rainbringer Yjatvot (ID.mob.BLACKBONE_FRAZDIZ and
// the id after it).
// Available (no other condition): Frescheque 151, whose finish begins the quest. Accepted: Raminey at Prog 0: 152 (Prog 1);
// Equette at Prog 1: 153 (Prog 2); the ??? at Prog 2, nmKilled not 1, when the pop of both NMs succeeds (claimed, the ???
// not hidden): SENSE_OF_FOREBODING (messageText); with nmKilled 1 and no Tiny Wristlet: the wristlet (npcUtil.giveKeyItem),
// noAction. Either NM's death at Prog 2 while the other is dead (isDead: never spawned or despawned counts): nmKilled 1.
// Frescheque with the wristlet: 154, whose finish, when quest:complete gives the Yin-Yang Lorgnette, takes the wristlet.
// Completed: Equette 155 (event, importantOnce).
// Kept as the Lua has it: the ??? at Prog 2 with an NM still up answers nil (the pop fails); nmKilled is never cleared
// before the completion; Frescheque's 154 needs only the wristlet (any Prog).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ABitterPast : QuestScript
    {
        public ABitterPast() : base(QuestLog.OtherAreas, "A_BITTER_PAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/A_Bitter_Past";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.YinyangLorgnette,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Frescheque).OnTrigger(QuestReply.ProgressEvent(151))
                .OnEventFinish(Zone.TavnazianSafehold, 151, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Frescheque).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TinyWristlet))
                    {
                        return QuestReply.ProgressEvent(154);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Raminey).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(152);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Equette).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(153);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 152, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 153, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 154, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.TinyWristlet);
                    }
                })
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.qmbitterpast).OnTrigger(q =>
                {
                    // npcUtil.popFromQM(player, npc, { orcNM, orcNM + 1 }, { claim = true, hide = 0 }): no look (nil, as false)
                    if (q.Quest.Step == 2 &&
                        q.Quest.Var("nmKilled") != 1 &&
                        q.Player.PopFromQmSeries(Mob.LufaiseMeadows.BLACKBONE_FRAZDIZ, 2, hide: 0, claim: true, look: false))
                    {
                        return QuestReply.MessageText(Text.LufaiseMeadows.SENSE_OF_FOREBODING);
                    }
                    else if (q.Quest.Var("nmKilled") == 1 &&
                             !q.Player.HasKeyItem(KeyItem.TinyWristlet))
                    {
                        q.Player.GiveKeyItem(KeyItem.TinyWristlet);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.BlackboneFrazdiz).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 2 &&
                        q.Player.MobIsDead(Mob.LufaiseMeadows.BLACKBONE_FRAZDIZ, 1))
                    {
                        q.Quest.SetVar("nmKilled", 1);
                    }
                })
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.RainbringerYjatvot).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 2 &&
                        q.Player.MobIsDead(Mob.LufaiseMeadows.BLACKBONE_FRAZDIZ))
                    {
                        q.Quest.SetVar("nmKilled", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Equette).OnTrigger(QuestReply.Event(155).ImportantOnce());
        }
    }
}
