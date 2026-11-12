// Chains and Bonds (Promathia 7-1, Log ID 6, Mission ID 648), a line-for-line port of
// scripts/missions/cop/7_1_Chains_and_Bonds.lua (quest rework C3, C4). The mission current. Lufaise Meadows: the
// zone-in plays 111 at Status 0 with RingStashed 0; its update sends (0, Ducal Guard's Ring); its finish with no free
// slot says NO_ROOM_COME_BACK_LATER and sets RingStashed 1, else gives the ring and sets Status 1 (a failed give leaves
// both). qm_chains_and_bonds plays 114 while RingStashed is 1; its finish gives the ring and sets RingStashed 0 and
// Status 1. Tavnazian Safehold: the zone-in plays 114 at Status 1 (finish: Status 2); _0qa plays 115 while Option bit 0
// is unset and _0q1 116 while bit 1 is; Despachiaire and Justinius play the events 318 and 135. Sealion's Den: the
// zone-in plays 14 while bit 2 is unset. The 115, 116 and 14 finishes set their bit and complete the mission when
// Option is 7 (the three bits, at any Status). Flames in the Darkness is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class ChainsAndBonds : MissionScript
    {
        public ChainsAndBonds() : base(XI.Quests.MissionLog.CoP, "CHAINS_AND_BONDS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/7_1_Chains_and_Bonds";

        // the 115, 116 and 14 finishes: the bit, then the mission completes with all three
        static void SetBitAndCheck(IQuestContext q, int bit)
        {
            q.Mission.SetVarBit("Option", bit);

            if (q.Mission.Var("Option") == 7)
            {
                q.Mission.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.FlamesInTheDarkness,
            });

            b.State(current: true)
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.qmchainsandbonds).OnTrigger(q =>
                    q.Mission.Var("RingStashed") == 1 ? QuestReply.ProgressEvent(114) : null)
                .OnZoneIn(Zone.LufaiseMeadows, q =>
                    q.Mission.Var("Status") == 0 && q.Mission.Var("RingStashed") == 0 ? QuestReply.Event(111) : null)
                .OnEventUpdate(Zone.LufaiseMeadows, 111, q => q.Player.UpdateEvent(0, Item.DucalGuardsRing))
                .OnEventFinish(Zone.LufaiseMeadows, 111, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.LufaiseMeadows.NO_ROOM_COME_BACK_LATER, Item.DucalGuardsRing);
                        q.Mission.SetVar("RingStashed", 1);
                    }
                    else if (q.Player.GiveItem(Item.DucalGuardsRing))
                    {
                        q.Mission.SetVar("Status", 1);
                    }
                })
                .OnEventFinish(Zone.LufaiseMeadows, 114, q =>
                {
                    if (q.Player.GiveItem(Item.DucalGuardsRing))
                    {
                        q.Mission.SetVar("RingStashed", 0);
                        q.Mission.SetVar("Status", 1);
                    }
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0qa).OnTrigger(q =>
                    !q.Mission.IsVarBitsSet("Option", 0) ? QuestReply.ProgressEvent(115) : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0q1).OnTrigger(q =>
                    !q.Mission.IsVarBitsSet("Option", 1) ? QuestReply.ProgressEvent(116) : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(QuestReply.Event(318))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(QuestReply.Event(135))
                .OnZoneIn(Zone.TavnazianSafehold, q => q.Mission.Var("Status") == 1 ? QuestReply.Event(114) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 114, q => q.Mission.SetVar("Status", 2))
                .OnEventFinish(Zone.TavnazianSafehold, 115, q => SetBitAndCheck(q, 0))
                .OnEventFinish(Zone.TavnazianSafehold, 116, q => SetBitAndCheck(q, 1))
                .OnZoneIn(Zone.SealionsDen, q => !q.Mission.IsVarBitsSet("Option", 2) ? QuestReply.Event(14) : null)
                .OnEventFinish(Zone.SealionsDen, 14, q => SetBitAndCheck(q, 2));
        }
    }
}
