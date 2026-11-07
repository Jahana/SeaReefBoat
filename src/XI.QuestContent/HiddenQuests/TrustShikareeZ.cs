// Trust: Shikaree Z (hidden quest TrustShikareeZ), ported from scripts/quests/hiddenQuests/Trust_ShikareeZ.lua (quest
// rework B3b hidden quests). Perih Vashai !gotoid 17764470 / !pos 117.5 -3.7 90.453 241 (his failing
// talks play The Fanged One's 351, the first, then 352; the first accepts windurst.the_fanged_one at the finish).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Shikaree Z not learned, CoP Three Paths completed, CoP current
// not from Flames in the Darkness to Fire in the Eyes of Men and not A Fate Decided, and the Windurst Trust Permit.
// Perih Vashai: 869 (a progress event); its finish (any option) while a permit is held: Shikaree Z learned (silently)
// and YOU_LEARNED_TRUST.
// Kept as the Lua has it: the finish reads no option (a refusal teaches her too); the check asks for a permit twice
// (any permit, then Windurst's); no quest:complete.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustShikareeZ : HiddenQuestScript
    {
        public TrustShikareeZ() : base("TrustShikareeZ") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_ShikareeZ";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.ShikareeZ) &&
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.ThreePaths) &&
                    // TODO BG WIKI States acquisition can be blocked while Shikaree Z is "out of town".
                    !(q.Player.CurrentMission(MissionLog.Cop) >= Mission.Cop.FlamesInTheDarkness &&
                    q.Player.CurrentMission(MissionLog.Cop) <= Mission.Cop.FireInTheEyesOfMen) &&
                    q.Player.CurrentMission(MissionLog.Cop) != Mission.Cop.AFateDecided &&
                    q.Player.HasKeyItem(KeyItem.WindurstTrustPermit))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).OnTrigger(q =>
                    QuestReply.ProgressEvent(869))
                .OnEventFinish(Zone.WindurstWoods, 869, q =>
                {
                    if (TrustPermit.Has(q.Player))
                    {
                        q.Player.AddSpell(Spell.ShikareeZ, silentLog: true);
                        q.Text.Special(Text.WindurstWoods.YOU_LEARNED_TRUST, 0, Spell.ShikareeZ);
                    }
                });
        }
    }
}
