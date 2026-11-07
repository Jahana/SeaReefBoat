// Petals for Parelbriaux (Log ID 4, Quest ID 74), a line-for-line port of scripts/quests/otherAreas/Petals_for_Parelbriaux.lua
// (quest rework B3b). Tavnazian Safehold, no scripts: Chemioue !pos 81.728 -33.970 66.351 (two, 16883724 and +36;
// default 280), Parelbriaux !pos 114.121 41.000 42.564 (two, +36; default 294), Ondieulix !pos 3.563 -21.912 65.983 (two,
// 16883727 and +36; default 285); Lufaise Meadows: qm_baumesel !pos -211.481 -16.016 287.182 (default NOTHING_OUT_OF_ORDINARY)
// and the NM Baumesel.
// Available once CoP's Darkness Named is completed: Chemioue at Prog 0: 512 (Prog 1); Parelbriaux at Prog 1: 513 (Prog 2);
// Ondieulix at Prog 2: 514, whose finish sets Prog 3 and begins the quest. Accepted: Baumesel's death: the char's local
// NMKilled 1 (any Prog); the ??? at Prog 3 with NMKilled 1: the Particularly Poignant Petal (npcUtil.giveKeyItem) and the
// cutscene 115 (progressCutscene), whose finish sets Prog 4; at Prog 3 without, in fog (xi.weather.FOG, 3) and
// Baumesel not up: Baumesel popped (claimed, the ??? not hidden), SPINE_CHILLING_PRESENCE. Ondieulix at Prog 4: 516, whose
// finish, when quest:complete gives the Powerful Rope and Putrid Purveyor of Pungent Petals, takes the petal.
// Kept as the Lua has it: NMKilled is a plain local var (cleared on zoning; The Call of the Sea's Bloody Coffin sets
// the same name); the ??? calls giveKeyItem again at Prog 3 with NMKilled 1 before 115 is finished (a no-op: the petal
// is held); popFromQM's result is not read (the message plays even when the pop fails, which the isSpawned test makes
// rare).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class PetalsForParelbriaux : QuestScript
    {
        public PetalsForParelbriaux() : base(QuestLog.OtherAreas, "PETALS_FOR_PARELBRIAUX") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Petals_for_Parelbriaux";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.PowerfulRope,
                Title = Title.PutridPurveyorOfPungentPetals,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.DarknessNamed))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Chemioue).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(512);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Ondieulix).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(514);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Parelbriaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(513);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 512, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 513, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 514, q =>
                {
                    q.Quest.SetStep(3);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.Baumesel).OnMobDeath(q =>
                {
                    q.Player.SetLocalVar("NMKilled", 1);
                })
                .Npc(Zone.LufaiseMeadows, Npc.LufaiseMeadows.qmbaumesel).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 3 &&
                        q.Player.LocalVar("NMKilled") == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.ParticularlyPoignantPetal);
                        return QuestReply.ProgressCutscene(115);
                    }
                    else if (progress == 3 &&
                             q.Player.Weather == Weather.Fog &&
                             !q.Player.MobIsSpawned(Mob.LufaiseMeadows.BAUMESEL))
                    {
                        q.Player.PopFromQm(Mob.LufaiseMeadows.BAUMESEL, hide: 0, claim: true);
                        return QuestReply.MessageSpecial(Text.LufaiseMeadows.SPINE_CHILLING_PRESENCE);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LufaiseMeadows, 115, q =>
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Ondieulix).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(516);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 516, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ParticularlyPoignantPetal);
                    }
                });
        }
    }
}
