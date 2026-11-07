// The Fumbling Friar (Log ID 7, Quest ID 22), a line-for-line port of scripts/quests/crystalWar/The_Fumbling_Friar.lua
// (quest rework B3b Crystal War group 11). Garlaige Citadel [S]: Fondactiont !pos -95.21 0 195.616 (no script; default
// 25). Grauberg [S]: qm2 !pos 79.876 -1.169 457.219 (the second qm2, qm2+104 in the flows; the first, at 416 -28 284,
// runs no script when triggered, on Lua as on C#; default NOTHING_OUT_OF_ORDINARY).
// Available, the Bronze Ribbon of Service and level 30+: Fondactiont 26, whose finish begins it. Accepted: Fondactiont:
// the Ornate Package: 28, whose finish completes it (the Scroll of Recall-Pashh) and takes the package; else 27 (an
// Event). qm2: no package: noAction; else nil. Completed: Fondactiont 29 (an Event, replaceDefault).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): qm2 with no package returns
// quest:keyItem(ORNATE_PACKAGE), the package given; the Lua built that reply, threw it away and returned
// quest:noAction(), so the package was never given and only a GM's package completed the quest. 26 begins on any
// option; 28's completion fails on a full inventory (the package kept).
// Rival containers: none. Fondactiont and the qm2s have no script and no other quest line: the reply kinds are pinned
// in tests/XI.Verify.Quests/CrystalWarGroup11.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheFumblingFriar : QuestScript
    {
        public TheFumblingFriar() : base(QuestLog.CrystalWar, "THE_FUMBLING_FRIAR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/The_Fumbling_Friar";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ScrollOfRecallPashh,
            });

            b.State(QuestStatus.Available, when: q =>
                    // TODO: Change to BRASS_RIBBON_OF_SERVICE when Campaign has been added.
                    q.Player.HasKeyItem(KeyItem.BronzeRibbonOfService) &&
                    q.Player.MainLevel >= 30)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Fondactiont).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(26);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 26, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Fondactiont).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.OrnatePackage))
                    {
                        return QuestReply.ProgressEvent(28);
                    }
                    else
                    {
                        return QuestReply.Event(27);
                    }
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 28, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OrnatePackage);
                    }
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.qm2).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OrnatePackage))
                    {
                        // the Lua dropped this quest:keyItem reply for noAction (fixed, user ruling 2026-11-04)
                        return QuestReply.KeyItem(KeyItem.OrnatePackage);
                    }
                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Fondactiont).OnTrigger(q =>
                {
                    return QuestReply.Event(29).ReplaceDefault();
                });
        }
    }
}
