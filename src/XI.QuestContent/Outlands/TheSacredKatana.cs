// The Sacred Katana (SAM AF1; Log ID 5, Quest ID 140), a line-for-line port of
// scripts/quests/outlands/SAM_AF1_The_Sacred_Katana.lua (quest rework B3b Outlands, group 1). Norg: Jaucribaix !pos 91 -7 -8
// (no script; default 71), Ranemaud !pos 15 0 23 (default 68). The Sanctuary of Zi'Tah: qm3 !pos -416 0 46 (no script;
// default NOTHING_OUT_OF_ORDINARY), the NM Isonade.
// Available, Forge Your Destiny completed, a Samurai from AF1_QUEST_LEVEL: Jaucribaix 139, begun on option 1. Accepted:
// Jaucribaix 140 with the Mumeito anywhere (findItem), else 143; a Sack of Fish Bait traded at Zi'Tah's qm3 pops Isonade
// (popFromQM, claimed, the qm not hidden) and says SENSE_OF_FOREBODING; its death sets the quest's local var Prog 1, and qm3
// then gives the Handful of Crystal Scales (a keyItem reply) while they are not held. The Mumeito traded to Jaucribaix with
// the scales: 141, whose finish runs quest:complete (Magoroku, Norg fame 20) and on true takes the Mumeito and the scales and
// sets Yomi Okuri's mustZone and its Timer now + 60. Ranemaud sells a lost Mumeito for 30000 gil (144, the trade 145) while
// none is held.
// Kept as the Lua has it: section 2 repeats section 1's 139 finish (begin while accepted: the quest's status stays; reached
// only with 139 open when the quest is begun by other means); Isonade's death sets the local var whoever claimed it; the
// local var is lost on a zone change (Isonade killed, then zoned: no scales until it is killed again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheSacredKatana : QuestScript
    {
        public TheSacredKatana() : base(QuestLog.Outlands, "THE_SACRED_KATANA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/SAM_AF1_The_Sacred_Katana";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Norg,
                Item = Item.Magoroku,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Outlands, "FORGE_YOUR_DESTINY") &&
                    q.Player.MainJob == Job.Sam &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(QuestReply.ProgressEvent(139))
                .OnEventFinish(Zone.Norg, 139, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix)
                    .OnTrade(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.HandfulOfCrystalScales) &&
                            q.Trade.Matches((Item.Mumeito, 1)))
                        {
                            return QuestReply.ProgressEvent(141);
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.Event(q.Player.FindItem(Item.Mumeito) ? 140 : 143))
                .Npc(Zone.Norg, Npc.Norg.Ranemaud)
                    .OnTrade(q =>
                    {
                        if (!q.Player.FindItem(Item.Mumeito) &&
                            q.Trade.Matches((Item.Gil, 30000)))
                        {
                            return QuestReply.ProgressEvent(145);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (!q.Player.FindItem(Item.Mumeito))
                        {
                            return QuestReply.Event(144);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 139, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Norg, 141, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.DelKeyItem(KeyItem.HandfulOfCrystalScales);

                        // Player must zone before being able to flag the next quest
                        q.Player.SetQuestMustZone(QuestLog.Outlands, "YOMI_OKURI");
                        q.Player.SetQuestVar(QuestLog.Outlands, "YOMI_OKURI", "Timer", q.Player.SystemTime + 60); // 1 minute wait time
                    }
                })
                .OnEventFinish(Zone.Norg, 145, q =>
                {
                    if (q.Player.GiveItem(Item.Mumeito))
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.Isonade).OnMobDeath(q =>
                {
                    q.Quest.SetLocalVar("Prog", 1);
                })
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.qm3)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.SackOfFishBait, 1)) &&
                            // { hide = 0 }: popFromQM's own claim default (true)
                            q.Player.PopFromQm(Mob.TheSanctuaryOfZitah.ISONADE, hide: 0))
                        {
                            q.Trade.Complete();
                            return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.SENSE_OF_FOREBODING);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.LocalVar("Prog") == 1 &&
                            !q.Player.HasKeyItem(KeyItem.HandfulOfCrystalScales))
                        {
                            return QuestReply.KeyItem(KeyItem.HandfulOfCrystalScales);
                        }
                        return null;
                    });
        }
    }
}
