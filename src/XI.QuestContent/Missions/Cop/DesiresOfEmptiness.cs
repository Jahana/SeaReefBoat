// Desires of Emptiness (Promathia 5-2, Log ID 6, Mission ID 518), a line-for-line port of
// scripts/missions/cop/5_2_Desires_of_Emptiness.lua (quest rework C3). Stone Door !pos -380 46.074 330.5 9; Flux 1
// (_0mc) !pos 180 -2.5 -60 22; Flux 2 (_0md) !pos 420 -2.5 140 22; Flux 3 (_0m0) !pos -340 -2.5 140 22; Cid !pos -12
// -12 1 237. The mission current. The 'Option' variable is a bitfield: bits 0 to 2 are the Propagator, Solicitor and
// Ponderer defeated (their deaths set them), bits 3 to 5 the cutscene seen. A flux (_0mc Propagator 51, _0md Solicitor
// 52, _0m0 Ponderer 53) whose NM is not defeated and not spawned spawns it claimed and says ON_NM_SPAWN; defeated and
// not seen it plays its cutscene, whose finish sets the seen bit and, with all three seen, Status 1. Behaviour ported
// as is: a flux whose NM is dead with only the seen bit set (no death bit) pops the NM again. Psoxja's _i99 plays the
// event of the three seen bits (psoXjaEventIdByOffset: 50, 106, 107, 109, 108, 110, 111, 112), the Psoxja finishes set
// the char down at the Vahzl position of their option (1 to 4, none otherwise). The Spire of Vahzl: the zone-in plays
// 21 at Status 0 (its finish sets the char down in Promyvion - Vahzl) and 20 at Status 1 (Status 2); the 32001 finish
// with the Desires of Emptiness battlefield won at Status 2 sets Status 3 and sets the char down in Beaucedine
// Glacier, whose zone-in plays 206 at Status 3 (update: updateEvent(0, MYSTERIOUS_AMULET); finish: Status 4); at
// Status 4 Leigon-Moigon, Potete and Torino-Samarino play 212, 213 and 211 as important events and Metalworks' Cid
// plays 850 (its finish completes the mission). Once complete, Psoxja's _i99 plays 112 and its finish sets the Vahzl
// position. Three Paths is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class DesiresOfEmptiness : MissionScript
    {
        public DesiresOfEmptiness() : base(XI.Quests.MissionLog.CoP, "DESIRES_OF_EMPTINESS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/5_2_Desires_of_Emptiness";

        // psoXjaEventIdByOffset, by the upper three Option bits
        static readonly int[] PsoXjaEventIdByOffset = { 50, 106, 107, 109, 108, 110, 111, 112 };

        // setPromyvionVahzlPos: vahzlPositions[option] for an option of 1 to 4
        static void SetPromyvionVahzlPos(IQuestContext q)
        {
            switch (q.Option)
            {
                case 1:
                    q.Player.SetPos(-14.744, 0.036, -119.736, 1, 22);
                    break;
                case 2:
                    q.Player.SetPos(183.546, 0, -59.906, 0, 22);
                    break;
                case 3:
                    q.Player.SetPos(415.757, 0, 139.977, 128, 22);
                    break;
                case 4:
                    q.Player.SetPos(-336.001, 0, 139.815, 0, 22);
                    break;
            }
        }

        // memoryFluxOnTrigger: bit is the flux's Option bit, mob its NM, csid its cutscene
        static QuestReply FluxTrigger(IQuestContext q, int bit, string mob, int csid)
        {
            if (!q.Mission.IsVarBitsSet("Option", bit) && !q.Player.MobIsSpawned(mob))
            {
                q.Player.SpawnMob(mob, 0, claim: true);
                return QuestReply.MessageSpecial(Text.PromyvionVahzl.ON_NM_SPAWN);
            }

            if (q.Mission.IsVarBitsSet("Option", bit) && !q.Mission.IsVarBitsSet("Option", bit + 3))
            {
                return QuestReply.ProgressCutscene(csid);
            }

            return null;
        }

        // memoryFluxOnEventFinish
        static void FluxFinish(IQuestContext q, int bit)
        {
            q.Mission.SetVarBit("Option", bit + 3);

            if (q.Mission.IsVarBitsSet("Option", 3, 4, 5))
            {
                q.Mission.SetVar("Status", 1);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.ThreePaths,
            });

            b.State(current: true)
                .Npc(Zone.PromyvionVahzl, Npc.PromyvionVahzl._0mc).OnTrigger(q =>
                    FluxTrigger(q, 0, Mob.PromyvionVahzl.PROPAGATOR, 51))
                .Npc(Zone.PromyvionVahzl, Npc.PromyvionVahzl._0md).OnTrigger(q =>
                    FluxTrigger(q, 1, Mob.PromyvionVahzl.SOLICITOR, 52))
                .Npc(Zone.PromyvionVahzl, Npc.PromyvionVahzl._0m0).OnTrigger(q =>
                    FluxTrigger(q, 2, Mob.PromyvionVahzl.PONDERER, 53))
                .Npc(Zone.PromyvionVahzl, Npc.PromyvionVahzl.Propagator).OnMobDeath(q =>
                    q.Mission.SetVarBit("Option", 0))
                .Npc(Zone.PromyvionVahzl, Npc.PromyvionVahzl.Solicitor).OnMobDeath(q =>
                    q.Mission.SetVarBit("Option", 1))
                .Npc(Zone.PromyvionVahzl, Npc.PromyvionVahzl.Ponderer).OnMobDeath(q => q.Mission.SetVarBit("Option", 2))
                .OnEventFinish(Zone.PromyvionVahzl, 51, q => FluxFinish(q, 0))
                .OnEventFinish(Zone.PromyvionVahzl, 52, q => FluxFinish(q, 1))
                .OnEventFinish(Zone.PromyvionVahzl, 53, q => FluxFinish(q, 2))
                .Npc(Zone.Psoxja, Npc.Psoxja.i99).OnTrigger(q =>
                    QuestReply.ProgressEvent(PsoXjaEventIdByOffset[q.Mission.Var("Option") >> 3]))
                .OnEventFinish(Zone.Psoxja, 50, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 106, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 107, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 108, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 109, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 110, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 111, SetPromyvionVahzlPos)
                .OnEventFinish(Zone.Psoxja, 112, SetPromyvionVahzlPos)
                .OnZoneIn(Zone.SpireOfVahzl, q =>
                {
                    int status = q.Mission.Var("Status");

                    if (status == 0)
                    {
                        return QuestReply.Event(21);
                    }

                    return status == 1 ? QuestReply.Event(20) : null;
                })
                .OnEventFinish(Zone.SpireOfVahzl, 20, q => q.Mission.SetVar("Status", 2))
                .OnEventFinish(Zone.SpireOfVahzl, 21, q =>
                    q.Player.SetPos(-140.011, 0, -53.990, 192, Zone.PromyvionVahzl))
                .OnEventFinish(Zone.SpireOfVahzl, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.DesiresOfEmptiness &&
                        q.Mission.Var("Status") == 2)
                    {
                        q.Mission.SetVar("Status", 3);

                        // NOTE (Lua): only players on this mission are transported here on completion of the
                        // battlefield; all others stay at the location of the 32001 finish
                        q.Player.SetPos(-340.00, -100.25, 140.00, 64, Zone.BeaucedineGlacier);
                    }
                })
                .Npc(Zone.BeaucedineGlacier, Npc.BeaucedineGlacier.LeigonMoigon).OnTrigger(q =>
                    q.Mission.Var("Status") == 4 ? QuestReply.Event(212).ImportantEvent() : null)
                .Npc(Zone.BeaucedineGlacier, Npc.BeaucedineGlacier.Potete).OnTrigger(q =>
                    q.Mission.Var("Status") == 4 ? QuestReply.Event(213).ImportantEvent() : null)
                .Npc(Zone.BeaucedineGlacier, Npc.BeaucedineGlacier.TorinoSamarino).OnTrigger(q =>
                    q.Mission.Var("Status") == 4 ? QuestReply.Event(211).ImportantEvent() : null)
                .OnZoneIn(Zone.BeaucedineGlacier, q => q.Mission.Var("Status") == 3 ? QuestReply.Event(206) : null)
                .OnEventUpdate(Zone.BeaucedineGlacier, 206, q => q.Player.UpdateEvent(0, KeyItem.MysteriousAmulet))
                .OnEventFinish(Zone.BeaucedineGlacier, 206, q => q.Mission.SetVar("Status", 4))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                    q.Mission.Var("Status") == 4 ? QuestReply.ProgressEvent(850) : null)
                .OnEventFinish(Zone.Metalworks, 850, q => q.Mission.Complete());

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.DesiresOfEmptiness))
                .Npc(Zone.Psoxja, Npc.Psoxja.i99).OnTrigger(QuestReply.Event(112))
                .OnEventFinish(Zone.Psoxja, 112, SetPromyvionVahzlPos);
        }
    }
}
