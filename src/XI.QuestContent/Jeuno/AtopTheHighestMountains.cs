// Atop the Highest Mountains (Log ID 3, Quest ID 129), a line-for-line port of
// scripts/quests/jeuno/LB02_Atop_the_Highest_Mountains.lua (quest rework B3b). Maat : !pos 8 3 118 243; the three ??? in
// Xarcabard (qm_boreal_tiger, qm_boreal_coeurl, qm_boreal_hound). At level cap 55 (MAX_LEVEL 60 or more) Maat offers 82
// from level 51 (option 1: begun), else MAAT_LB2_PLACEHOLDER. Accepted: Maat 83, 84 with the three frigicites
// (completed, Summit Breaker, the frigicites taken, level cap 60, YOUR_LEVEL_LIMIT_IS_NOW_60); each ??? gives its
// frigicite when it is not held (with OLDSCHOOL_G2, only while its Boreal NM is dead). The Lua file has no known bug to
// keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class AtopTheHighestMountains : QuestScript
    {
        public AtopTheHighestMountains() : base(QuestLog.Jeuno, "ATOP_THE_HIGHEST_MOUNTAINS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB02_Atop_the_Highest_Mountains";

        // not xi.settings.main.OLDSCHOOL_G2 or GetMobByID(xarcabardID.mob.<NM>):isDead(): Lua's truthiness
        // (Settings.MainTruthy, quest rework P9), so only false and nil are off; a 0 is on, as in Lua
        static bool NmAllows(IQuestContext q, string mobName) =>
            !q.Settings.MainTruthy("OLDSCHOOL_G2") || q.Player.MobIsDead(mobName);

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.SummitBreaker });

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.LevelCap == 55 &&
                    q.Settings.MainNumber("MAX_LEVEL") >= 60)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 51)
                    {
                        return QuestReply.ProgressEvent(82);
                    }
                    else
                    {
                        return QuestReply.MessageText(Text.RuludeGardens.MAAT_LB2_PLACEHOLDER);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 82, q =>
                {
                    if (q.Option == 1) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RoundFrigicite) &&
                        q.Player.HasKeyItem(KeyItem.SquareFrigicite) &&
                        q.Player.HasKeyItem(KeyItem.TriangularFrigicite))
                    {
                        return QuestReply.ProgressEvent(84);
                    }
                    else
                    {
                        return QuestReply.Event(83);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 84, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.RoundFrigicite);
                        q.Player.DelKeyItem(KeyItem.SquareFrigicite);
                        q.Player.DelKeyItem(KeyItem.TriangularFrigicite);
                        q.Player.SetLevelCap(60);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_60);
                    }
                })
                .Npc(Zone.Xarcabard, Npc.Xarcabard.qmborealtiger).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.RoundFrigicite) &&
                        NmAllows(q, Mob.Xarcabard.BOREAL_TIGER))
                    {
                        return QuestReply.KeyItem(KeyItem.RoundFrigicite);
                    }
                    return null;
                })
                .Npc(Zone.Xarcabard, Npc.Xarcabard.qmborealcoeurl).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.SquareFrigicite) &&
                        NmAllows(q, Mob.Xarcabard.BOREAL_COEURL))
                    {
                        return QuestReply.KeyItem(KeyItem.SquareFrigicite);
                    }
                    return null;
                })
                .Npc(Zone.Xarcabard, Npc.Xarcabard.qmborealhound).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TriangularFrigicite) &&
                        NmAllows(q, Mob.Xarcabard.BOREAL_HOUND))
                    {
                        return QuestReply.KeyItem(KeyItem.TriangularFrigicite);
                    }
                    return null;
                });
        }
    }
}
