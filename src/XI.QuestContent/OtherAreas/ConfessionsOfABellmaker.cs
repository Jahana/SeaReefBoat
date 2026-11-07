// Confessions of a Bellmaker (Log ID 4, Quest ID 79), a line-for-line port of
// scripts/quests/otherAreas/Confessions_of_a_Bellmaker.lua (quest rework B3b, Other Areas group 8). Riverne - Site
// #A01: Stone Monument !pos -733 -46 -300 (no script; the default MONUMENT) and the NM Arcane Phantasm; Bastok Markets:
// Reinberta !pos -190 -7 -59 (the Goldsmiths' guild master: her script runs xi.crafting.guildMasterOnTrigger, event
// 300); Northern San d'Oria: Mevreauche !pos -193 11 148 (the Smiths' guild master: the same script, event 626).
// Available (no other condition): the Monument 101 (progressCutscene), whose finish begins the quest and gives the
// Ornamented Scroll. Accepted: Reinberta at Prog 0: 402, whose finish sets Prog 1; else nil (her guild script).
// Mevreauche at Prog 1: 74, whose finish sets Prog 2; else nil (his guild script). The Monument: Prog 2: 102
// (progressCutscene), whose finish sets Prog 3; 3: npcUtil.popFromQM(player, the Monument, ARCANE_PHANTASM, { hide = 0
// }), noAction whether it popped or not; 4: 103 (progressCutscene), whose finish runs quest:complete (Minstrel's
// Dagger) and on true takes the scroll; else nil. Arcane Phantasm's death at Prog 3: Prog 4. Completed: no state (the
// Monument's default; Bastok Markets' Lamepaue reads hasCompletedQuest).
// Kept as the Lua has it: the Lua pops from GetNPCByID(STONE_MONUMENT), the zone's only Stone Monument, which is the
// handler's NPC, so `if monumentObj then` always holds and is not ported as a branch; the pop's answer is not read
// (Arcane Phantasm up: noAction all the same); 101's finish begins the quest whatever the option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ConfessionsOfABellmaker : QuestScript
    {
        public ConfessionsOfABellmaker() : base(QuestLog.OtherAreas, "CONFESSIONS_OF_A_BELLMAKER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Confessions_of_a_Bellmaker";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MinstrelsDagger,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available)
                .Npc(Zone.RiverneSiteA01, Npc.RiverneSiteA01.StoneMonument).OnTrigger(q =>
                {
                    return QuestReply.ProgressCutscene(101);
                })
                .OnEventFinish(Zone.RiverneSiteA01, 101, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.OrnamentedScroll);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.RiverneSiteA01, Npc.RiverneSiteA01.StoneMonument).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressCutscene(102);
                    }
                    else if (q.Quest.Step == 3)
                    {
                        // monumentObj = GetNPCByID(zones[xi.zone.RIVERNE_SITE_A01].npc.STONE_MONUMENT): the handler's
                        // npc (the zone has one Stone Monument), so `if monumentObj then` always holds
                        // TODO: Determine if message is displayed on NM pop, return message if this is successful, else
                        // noAction()
                        q.Player.PopFromQm(Mob.RiverneSiteA01.ARCANE_PHANTASM, hide: 0);

                        return QuestReply.NoAction();
                    }
                    else if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressCutscene(103);
                    }
                    return null;
                })
                .Npc(Zone.RiverneSiteA01, Npc.RiverneSiteA01.ArcanePhantasm).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.RiverneSiteA01, 102, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.RiverneSiteA01, 103, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OrnamentedScroll);
                    }
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Reinberta).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(402);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarkets, 402, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Mevreauche).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(74);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 74, q =>
                {
                    q.Quest.SetStep(2);
                });
        }
    }
}
