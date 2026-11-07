// Altepa - Dominion Op #11 (Log ID 8, Quest ID 97), a line-for-line port of
// scripts/quests/abyssea/Altepa_Dominion_Op_11.lua (quest rework B3b Abyssea). Abyssea - Altepa: the op's target
// Nannakola (19 placed spawns, the first 17670208); the file names Dominion Sergeant (Volker's Camp). Accepted: each
// Nannakola death runs xi.abyssea.dominionOnMobDeath(mob, player, 570), which counts it in Quest[8][97]Prog up to 5
// (FOV_DEFEATED_TARGET). The Dominion Sergeants' global (globals/abyssea/dominion.lua, which stays Lua) adds the op
// (player:addQuest), cancels it (delQuest) and completes it (completeDominionOp) from its own NPC handlers. Kept as the
// Lua has it: the count runs whenever the op is accepted, whether or not it is the active one (activeDominionOp), and
// for every member of the killer's alliance in the zone who has it accepted, the killer or not; a completed op signed
// on again stays completed (addQuest sets the current bit, the completed bit wins), so its deaths count nothing until
// it is cancelled; an empty reward table; no other section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class DominionOp11Altepa : QuestScript
    {
        public DominionOp11Altepa() : base(QuestLog.Abyssea, "DOMINION_OP_11_ALTEPA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Altepa_Dominion_Op_11";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AbysseaAltepa, Npc.AbysseaAltepa.Nannakola).OnMobDeath(q =>
                {
                    q.Player.DominionOnMobDeath(570);
                });
        }
    }
}
