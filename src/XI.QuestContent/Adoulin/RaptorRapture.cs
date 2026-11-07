// Raptor Rapture (Adoulin 73), ported from the quest branches of scripts/zones/Western_Adoulin/npcs/Pagnelle.lua,
// npcs/Gontrain.lua and Zone.lua (quest rework B3b Adoulin group 5, call-only). Pagnelle !pos -8 0 -100 256 (no
// default), Gontrain !pos 13 0 -143 256 (default 5042). Its state is the char var Raptor_Rapture_Status.
// Pagnelle, available: status below 3: status 1 and 5032, whose finish sends the char to Rala Waterways
// at 0 0 0 (player:setPos(0, 0, 0, 0, 258)); else 5061, whose finish on option 1 adds the quest and sets status 4.
// Accepted: status 4 5033; 5 5035 (finish: status 6); 6 with the three Rockberries 5037 (finish: the berries taken,
// status 7), without them 5036; 7 5038; 8 5039 (finish: status 0, the raw player:completeQuest, 1000 bayld at the rate,
// BAYLD_OBTAINED, 30 Adoulin fame, needToZone). Completed: needing to zone 5040, else 5041. Gontrain: accepted at
// status 4: 5034 (finish: status 5). Western Adoulin's zone-in at status 2: 5056 (before the zone's SoA and Mog House
// events), whose finish (the zone's) sets status 3, and on option 1 adds the quest and sets status 4.
// Kept as the scripts have them: nothing sets status 2 (Rala Waterways' zone-in knows nothing of it), so 5056 and its
// finish never play and the quest is reached only through a GM's status; nothing gives the Rockberries or
// sets status 8; the raw completeQuest with its own rewards; 5056's finish sets status 3 on any option.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class RaptorRapture : QuestScript
    {
        public RaptorRapture() : base(QuestLog.Adoulin, "RAPTOR_RAPTURE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Pagnelle).Called().OnTrigger(q =>
                {
                    QuestStatus raptorRapture = q.Quest.Status;
                    int raptorRaptureStatus = q.Player.CharVar("Raptor_Rapture_Status");

                    if (raptorRapture == QuestStatus.Available)
                    {
                        if (raptorRaptureStatus < 3)
                        {
                            // Starts chain of events for the introduction CS for Quest: 'Raptor Rapture'.
                            // If player somehow doesn't finish the chain of events, they can just talk to Pagnelle
                            // again to retry.
                            q.Player.SetCharVar("Raptor_Rapture_Status", 1);
                            return QuestReply.Event(5032);
                        }
                        else
                        {
                            // Player has finished introductory CS event chain, but didn't accept the quest.
                            // Offers Quest: 'Raptor Rapture' if player has yet to accept it.
                            return QuestReply.Event(5061);
                        }
                    }
                    else if (raptorRapture == QuestStatus.Accepted)
                    {
                        if (raptorRaptureStatus == 4)
                        {
                            // Reminder during Quest: 'Raptor Rapture', speak to Ilney.
                            return QuestReply.Event(5033);
                        }
                        else if (raptorRaptureStatus == 5)
                        {
                            // Progresses Quest: 'Raptor Rapture', spoke to Ilney.
                            return QuestReply.Event(5035);
                        }
                        else if (raptorRaptureStatus == 6)
                        {
                            bool hasRockberries = q.Player.HasKeyItem(KeyItem.Rockberry1) &&
                                q.Player.HasKeyItem(KeyItem.Rockberry2) && q.Player.HasKeyItem(KeyItem.Rockberry3);
                            if (hasRockberries)
                            {
                                // Progresses Quest: 'Raptor Rapture', turning in rockberries.
                                return QuestReply.Event(5037);
                            }
                            else
                            {
                                // Reminder during Quest: 'Raptor Rapture', bring rockberries.
                                return QuestReply.Event(5036);
                            }
                        }
                        else if (raptorRaptureStatus == 7)
                        {
                            // Reminder during Quest: 'Raptor Rapture', go to Rala.
                            return QuestReply.Event(5038);
                        }
                        else if (raptorRaptureStatus == 8)
                        {
                            // Finishes Quest: 'Raptor Rapture'
                            return QuestReply.Event(5039);
                        }
                    }
                    else
                    {
                        if (q.Player.NeedToZone)
                        {
                            // Dialogue after finishing Quest: 'Raptor Rapture', before zoning
                            return QuestReply.Event(5040);
                        }
                        else
                        {
                            // Dialogue after finishing Quest: 'Raptor Rapture', after zoning
                            return QuestReply.Event(5041);
                        }
                    }

                    return null;
                })
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Gontrain).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.CharVar("Raptor_Rapture_Status") == 4)
                    {
                        // Progresses Quest: 'Raptor Rapture', speaking to Ilney.
                        return QuestReply.Event(5034);
                    }

                    return null;
                })
                // Western Adoulin's Zone.lua onZoneIn, its first test
                .OnZoneIn(Zone.WesternAdoulin, q =>
                {
                    if (q.Player.CharVar("Raptor_Rapture_Status") == 2)
                    {
                        // Resuming cutscene for Quest: 'Raptor Rapture', after Pagnelle warps you to Rala Waterways
                        // mid-CS, then back here.
                        return QuestReply.Event(5056);
                    }

                    return null;
                });

            b.AnyState()
                // Pagnelle's
                .OnEventFinish(Zone.WesternAdoulin, 5032, q =>
                {
                    // Warps player to Rala Waterways to continue intrductory CS for Quest: 'Raptor Rapture'
                    q.Player.SetPos(0, 0, 0, 0, 258);
                })
                .OnEventFinish(Zone.WesternAdoulin, 5061, q =>
                {
                    if (q.Option == 1)
                    {
                        // Starts Quest: 'Raptor Rapture'
                        q.Quest.Begin();
                        q.Player.SetCharVar("Raptor_Rapture_Status", 4);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 5035, q =>
                {
                    // Progresses Quest: 'Raptor Rapture', spoke to Ilney, now need rockberries.
                    q.Player.SetCharVar("Raptor_Rapture_Status", 6);
                })
                .OnEventFinish(Zone.WesternAdoulin, 5037, q =>
                {
                    // Progresses Quest: 'Raptor Rapture', brought rockberries, now need to go to Rala.
                    q.Player.DelKeyItem(KeyItem.Rockberry1);
                    q.Player.DelKeyItem(KeyItem.Rockberry2);
                    q.Player.DelKeyItem(KeyItem.Rockberry3);
                    q.Player.SetCharVar("Raptor_Rapture_Status", 7);
                })
                .OnEventFinish(Zone.WesternAdoulin, 5039, q =>
                {
                    // Finishing Quest: 'Raptor Rapture'
                    q.Player.SetCharVar("Raptor_Rapture_Status", 0);
                    q.Player.CompleteQuest(QuestLog.Adoulin, "RAPTOR_RAPTURE");
                    int bayld = RateAmount(1000, q.Settings.MainNumber("BAYLD_RATE"));
                    q.Player.AddCurrency("bayld", bayld);
                    q.Text.Special(Text.WesternAdoulin.BAYLD_OBTAINED, bayld);

                    // TODO: Verify fame value added
                    q.Player.AddFame(FameArea.Adoulin, 30);
                    q.Player.SetNeedToZone(true);
                })
                // Gontrain's
                .OnEventFinish(Zone.WesternAdoulin, 5034, q =>
                {
                    // Progresses Quest: 'Raptor Rapture', spoke to Ilney.
                    q.Player.SetCharVar("Raptor_Rapture_Status", 5);
                })
                // the zone's (npc nil)
                .OnEventFinish(Zone.WesternAdoulin, 5056, q =>
                {
                    // Successfully finished introduction CS event chain for Quest: 'Raptor Rapture'.
                    q.Player.SetCharVar("Raptor_Rapture_Status", 3);

                    if (q.Option == 1)
                    {
                        // Starts Quest: 'Raptor Rapture'
                        q.Quest.Begin();
                        q.Player.SetCharVar("Raptor_Rapture_Status", 4);
                    }
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
