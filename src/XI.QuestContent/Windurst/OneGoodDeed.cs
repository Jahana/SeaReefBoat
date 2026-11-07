// One Good Deed? (Log ID 2, Quest ID 92), a line-for-line port of scripts/quests/windurst/One_Good_Deed.lua (quest
// rework B3b). Chipmy-Popmy !pos -181.466 -2.834 73.57; qm_deed !pos -321.364 -2.591 -738.066. At Windurst fame level
// 5, Chipmy-Popmy's 594, option 1, begins it. Accepted: Chipmy-Popmy's 596 (Prog 0), 595 (Prog 1, the Deed to
// Purgonorgo Isle: Prog 2), 599 (Prog 2), 597 (Prog 3: completed: fame 50, 2000 exp, 3200 gil, the Map of the Attohwa
// Chasm, the title Deed Verifier). Bibiki Bay's qm_deed at Prog 0 with no Peerifool killed (the player local var
// NMKilled) and none of the six up: the six Peerifools spawn, claimed by the char (IT_WAS_A_TRAP, then
// YOU_ARE_NOT_ALONE); each Peerifool's death at Prog 0 adds one to NMKilled (a local var: zoning resets it); at 6 the
// qm_deed's cutscene 34 gives the deed (Prog 1). Boneyard Gully's afterZoneIn at Prog 2: 8 (4, the deed, 0, 274, 8);
// its update option 101 updates the event (3, the map, 0, 274, 8); its finish: Prog 3, the map. The Boneyard Gully
// handler is an afterZoneIn (interaction_lookup's third zone-wide key, run 4 s after the zone-in by
// zoneutils::AfterZoneIn): OnAfterZoneIn (quest rework P5), whose reply probe.after_zone_in proves. Kept as the Lua
// file has it: 597's finish ignores a failed completion (it cannot fail: no item); the reward's map is given again
// though 8's finish gave it; 8's update ignores every option but 101.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class OneGoodDeed : QuestScript
    {
        public OneGoodDeed() : base(QuestLog.Windurst, "ONE_GOOD_DEED") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/One_Good_Deed";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Windurst,
                Exp = 2000,
                Gil = 3200,
                KeyItem = KeyItem.MapOfTheAttohwaChasm,
                Title = Title.DeedVerifier,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 5)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.ChipmyPopmy).OnTrigger(QuestReply.ProgressEvent(594))
                .OnEventFinish(Zone.PortWindurst, 594, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.ChipmyPopmy).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(596);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(595, 0, KeyItem.DeedToPurgonorgoIsle);
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.ProgressEvent(597);
                    }
                    else
                    {
                        return QuestReply.Event(599);
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 595, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.PortWindurst, 597, q => q.Quest.Complete())
                .Npc(Zone.BibikiBay, Npc.BibikiBay.qmdeed).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;
                    int nmKilled = q.Player.LocalVar("NMKilled");

                    if (
                        progress == 0 &&
                        nmKilled == 0 &&
                        !q.Player.MobIsSpawned(Mob.BibikiBay.PEERIFOOL) &&
                        !q.Player.MobIsSpawned(Mob.BibikiBay.PEERIFOOL, 1) &&
                        !q.Player.MobIsSpawned(Mob.BibikiBay.PEERIFOOL, 2) &&
                        !q.Player.MobIsSpawned(Mob.BibikiBay.PEERIFOOL, 3) &&
                        !q.Player.MobIsSpawned(Mob.BibikiBay.PEERIFOOL, 4) &&
                        !q.Player.MobIsSpawned(Mob.BibikiBay.PEERIFOOL, 5))
                    {
                        for (int i = 0; i <= 5; i++)
                        {
                            // SpawnMob(bayID.mob.PEERIFOOL + i):updateClaim(player)
                            q.Player.SpawnMob(Mob.BibikiBay.PEERIFOOL, i, claim: true);
                        }

                        q.Text.Special(Text.BibikiBay.IT_WAS_A_TRAP);
                        return QuestReply.MessageSpecial(Text.BibikiBay.YOU_ARE_NOT_ALONE);
                    }
                    else if (
                        progress == 0 &&
                        nmKilled == 6)
                    {
                        return QuestReply.ProgressCutscene(34);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BibikiBay, 34, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.GiveKeyItem(KeyItem.DeedToPurgonorgoIsle);
                })
                .Npc(Zone.BibikiBay, Npc.BibikiBay.Peerifool).OnMobDeath(q =>
                {
                    int nmKilled = q.Player.LocalVar("NMKilled");

                    if (q.Quest.Step == 0)
                    {
                        q.Player.SetLocalVar("NMKilled", nmKilled + 1); // Resets upon zoning causing the player to need to refight the NMs
                    }
                })
                // [xi.zone.BONEYARD_GULLY].afterZoneIn = function(player) ... end (interaction_lookup's 'afterZoneIn')
                .OnAfterZoneIn(Zone.BoneyardGully, q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(8, 4, KeyItem.DeedToPurgonorgoIsle, 0, 274, 8);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.BoneyardGully, 8, q =>
                {
                    if (q.Option == 101)
                    {
                        q.Player.UpdateEvent(3, KeyItem.MapOfTheAttohwaChasm, 0, 274, 8);
                    }
                })
                .OnEventFinish(Zone.BoneyardGully, 8, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.GiveKeyItem(KeyItem.MapOfTheAttohwaChasm);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.ChipmyPopmy).OnTrigger(QuestReply.Event(598));
        }
    }
}
