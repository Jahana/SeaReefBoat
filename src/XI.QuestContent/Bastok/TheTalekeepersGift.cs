// The Talekeeper's Gift (Log ID 1, Quest ID 56, WAR AF3), a line-for-line port of
// scripts/quests/bastok/WAR_AF3_The_Talekeepers_Gift.lua (quest rework B3b). Deidogg : !pos -13 7 29 234; Detzo : !pos
// 5.365 6.999 9.891 234; qm_talekeeper : !pos 211 4 -79 127. Offered once The Talekeeper's Truth is completed, its Timer
// (set by that quest) is reached and its mustZone is clear: Deidogg's 170 (Prog 1), Detzo's 171 (Prog 2) and the Ginger
// Cookie to Deidogg (172) begin it at Prog 0. The ??? in Behemoth's Dominion (100) spawns Picklix Longindex (claimed),
// Moxnix Nightgoggle and Doglix Muttsnout (on enmity), TALEKEEPERS_GIFT_OFFSET + 0, 1, 2; their deaths set Prog bits 2, 1
// and 0, and the zone-in to Qufim Island from Behemoth's Dominion at Prog 7 plays 100, which completes it. The zone-in's
// prevZone is player:getPreviousZone() (the same value).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheTalekeepersGift : QuestScript
    {
        public TheTalekeepersGift() : base(QuestLog.Bastok, "THE_TALEKEEPERS_GIFT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/WAR_AF3_The_Talekeepers_Gift";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.FightersLorica,
                Title = Title.ParagonOfWarriorExcellence,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_TALEKEEPERS_TRUTH") &&
                    q.Player.MainJob == Job.War &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL") &&
                    q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay &&
                    !q.Quest.MustZone)
                .Npc(Zone.BastokMines, Npc.BastokMines.Deidogg).OnTrade(q =>
                {
                    if (q.Quest.Var("Prog") == 2 && q.Trade.Matches((Item.GingerCookie, 1)))
                    {
                        return QuestReply.ProgressEvent(172);
                    }
                    return null;
                })
                .OnTrigger(q => q.Quest.Var("Prog") == 0 ? QuestReply.ProgressEvent(170) : null)
                .Npc(Zone.BastokMines, Npc.BastokMines.Detzo).OnTrigger(q =>
                    q.Quest.Var("Prog") == 1 ? QuestReply.ProgressEvent(171) : null)
                .OnEventFinish(Zone.BastokMines, 170, q => q.Quest.SetVar("Prog", 1))
                .OnEventFinish(Zone.BastokMines, 171, q => q.Quest.SetVar("Prog", 2))
                .OnEventFinish(Zone.BastokMines, 172, q =>
                {
                    q.Trade.Complete();

                    q.Quest.Begin();
                    q.Quest.SetVar("Prog", 0);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BehemothsDominion, Npc.BehemothsDominion.qmtalekeeper).OnTrigger(q =>
                {
                    if (q.Quest.Var("Prog") != 7 &&
                        !q.Player.MobIsSpawned(Mob.BehemothsDominion.TALEKEEPERS_GIFT_OFFSET, 0) &&
                        !q.Player.MobIsSpawned(Mob.BehemothsDominion.TALEKEEPERS_GIFT_OFFSET, 1) &&
                        !q.Player.MobIsSpawned(Mob.BehemothsDominion.TALEKEEPERS_GIFT_OFFSET, 2))
                    {
                        q.Text.Special(Text.BehemothsDominion.THERE_ARE_SYMBOLS);
                        return QuestReply.ProgressEvent(100);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BehemothsDominion, 100, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SpawnMob(Mob.BehemothsDominion.TALEKEEPERS_GIFT_OFFSET, 0, claim: true);  // Picklix_Longindex
                        q.Player.SpawnMob(Mob.BehemothsDominion.TALEKEEPERS_GIFT_OFFSET, 1, claim: false); // Moxnix_Nightgoggle
                        q.Player.SpawnMob(Mob.BehemothsDominion.TALEKEEPERS_GIFT_OFFSET, 2, claim: false); // Doglix_Muttsnout

                        q.Text.Special(Text.BehemothsDominion.YOU_HEAR_A_NOISE);
                    }
                })
                .Npc(Zone.BehemothsDominion, Npc.BehemothsDominion.DoglixMuttsnout).OnMobDeath(q => q.Quest.SetVarBit("Prog", 0))
                .Npc(Zone.BehemothsDominion, Npc.BehemothsDominion.MoxnixNightgoggle).OnMobDeath(q => q.Quest.SetVarBit("Prog", 1))
                .Npc(Zone.BehemothsDominion, Npc.BehemothsDominion.PicklixLongindex).OnMobDeath(q => q.Quest.SetVarBit("Prog", 2))
                .OnZoneIn(Zone.QufimIsland, q =>
                {
                    if (q.Player.PreviousZone == Zone.BehemothsDominion && q.Quest.Var("Prog") == 7)
                    {
                        return QuestReply.Event(100);
                    }
                    return null;
                })
                .OnEventFinish(Zone.QufimIsland, 100, q => q.Quest.Complete());
        }
    }
}
