// Playing the Part (ToAU 25, Log ID 4, Mission ID 24), a line-for-line port of
// scripts/missions/toau/25_Playing_the_Part.lua (quest rework C3 T2). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current). Naja, when the mustZone is clear and VanadielUniqueDay has reached the Timer var
// (Foiled Ambition's finish sets the Timer), plays the progress event 3110; else the plain event 3098 (the mercenary
// rank, 1, and the dialog at the eighth operand: the Option var plus 1, so 1 then 2 then 1; the Option var set to 1
// when the dialog was 1, else 0). 3110's finish completes the mission (Seal of the Serpent current). Kept as the Lua
// has it: Foiled Ambition's finish sets this mission's mustZone as a local var, which getMustZone (a char var) never
// reads, so only the Timer delays the mission. The ROV era module (Modules/Era/Missions/MissionAdjustments.cs) replaces
// Naja's trigger.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PlayingThePart : MissionScript
    {
        public PlayingThePart() : base(XI.Quests.MissionLog.ToAU, "PLAYING_THE_PART") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/25_Playing_the_Part";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.SealOfTheSerpent,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (!q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                    {
                        return QuestReply.ProgressEvent(3110);
                    }

                    int dialog = q.Mission.Var("Option") + 1; // Captured values 1 and 2
                    if (dialog == 1)
                    {
                        q.Mission.SetVar("Option", 1);
                    }
                    else
                    {
                        q.Mission.SetVar("Option", 0);
                    }

                    return QuestReply.Event(3098, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3110, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
