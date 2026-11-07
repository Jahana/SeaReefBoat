// Altepa - Dominion Op #14 (Log ID 8, Quest ID 100), a line-for-line port of
// scripts/quests/abyssea/Altepa_Dominion_Op_14.lua (quest rework B3b Abyssea). Abyssea - Altepa: the op's target
// Barrens_Treant (16 placed spawns, the first 17670427); the file names Dominion Sergeant (Nanaa Mihgo's Camp).
// Accepted: each Barrens_Treant death runs xi.abyssea.dominionOnMobDeath(mob, player, 573), which counts it in
// Quest[8][100]Prog up to 5 (FOV_DEFEATED_TARGET). The Dominion Sergeants' global (globals/abyssea/dominion.lua, which
// stays Lua) adds the op (player:addQuest), cancels it (delQuest) and completes it (completeDominionOp) from its own
// NPC handlers. Kept as the Lua has it: the count runs whenever the op is accepted, whether or not it is the active one
// (activeDominionOp), and for every member of the killer's alliance in the zone who has it accepted, the killer or not;
// a completed op signed on again stays completed (addQuest sets the current bit, the completed bit wins), so its deaths
// count nothing until it is cancelled; an empty reward table; no other section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class DominionOp14Altepa : QuestScript
    {
        public DominionOp14Altepa() : base(QuestLog.Abyssea, "DOMINION_OP_14_ALTEPA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Altepa_Dominion_Op_14";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AbysseaAltepa, Npc.AbysseaAltepa.BarrensTreant).OnMobDeath(q =>
                {
                    q.Player.DominionOnMobDeath(573);
                });
        }
    }
}
