// Whence Blows the Wind (Log ID 3, Quest ID 130), a line-for-line port of scripts/quests/jeuno/LB03_Whence_Blows_the_wind.lua
// (quest rework B3b). Maat : !pos 8 3 118 243; qm1 (Monastic Cavern) : !pos 168 -1 -22 150; qm2 (Castle Oztroja) : !pos
// -100 -63 58 151; qm1 (Qulun Dome) : !pos 261 39 79 148. At level cap 60 (MAX_LEVEL 65 or more) Maat offers 85 from
// level 56 (option 1: begun), else MAAT_LB3_PLACEHOLDER. Accepted: Maat 86, 87 with the three crests (completed, Sky
// Breaker, the crests taken, level cap 65, YOUR_LEVEL_LIMIT_IS_NOW_65); each ??? gives its crest when it is not held.
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class WhenceBlowsTheWind : QuestScript
    {
        public WhenceBlowsTheWind() : base(QuestLog.Jeuno, "WHENCE_BLOWS_THE_WIND") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB03_Whence_Blows_the_wind";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.SkyBreaker });

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.LevelCap == 60 &&
                    q.Settings.MainNumber("MAX_LEVEL") >= 65)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 56)
                    {
                        return QuestReply.ProgressEvent(85);
                    }
                    else
                    {
                        return QuestReply.MessageText(Text.RuludeGardens.MAAT_LB3_PLACEHOLDER);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 85, q =>
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
                    if (q.Player.HasKeyItem(KeyItem.OrcishCrest) &&
                        q.Player.HasKeyItem(KeyItem.QuadavCrest) &&
                        q.Player.HasKeyItem(KeyItem.YagudoCrest))
                    {
                        return QuestReply.ProgressEvent(87);
                    }
                    else
                    {
                        return QuestReply.Event(86);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 87, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OrcishCrest);
                        q.Player.DelKeyItem(KeyItem.QuadavCrest);
                        q.Player.DelKeyItem(KeyItem.YagudoCrest);
                        q.Player.SetLevelCap(65);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_65);
                    }
                })
                .Npc(Zone.CastleOztroja, Npc.CastleOztroja.qm2).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.YagudoCrest))
                    {
                        return QuestReply.KeyItem(KeyItem.YagudoCrest);
                    }
                    return null;
                })
                .Npc(Zone.MonasticCavern, Npc.MonasticCavern.qm1).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OrcishCrest))
                    {
                        return QuestReply.KeyItem(KeyItem.OrcishCrest);
                    }
                    return null;
                })
                .Npc(Zone.QulunDome, Npc.QulunDome.qm1).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.QuadavCrest))
                    {
                        return QuestReply.KeyItem(KeyItem.QuadavCrest);
                    }
                    return null;
                });
        }
    }
}
