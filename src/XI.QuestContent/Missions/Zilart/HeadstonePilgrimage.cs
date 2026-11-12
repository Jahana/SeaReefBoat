// Headstone Pilgrimage (Zilart 5, Log ID 3, Mission ID 10), a line-for-line port of
// scripts/missions/rotz/05_Headstone_Pilgrimage.lua (quest rework C3, C5). Cermet Headstones: Behemoth's Dominion
// (Lightning) !pos -74 -4 -87 127; Cape Teriggan (Wind) !pos -107 -8 450 113; Cloister of Frost (Ice) !pos 566 0 606
// 203; La Theine Plateau (Water) !pos -170 39 -504 102; the Sanctuary of Zi'Tah (Light) !pos 235 0 280 121; Western
// Altepa Desert (Earth) !pos -108 10 -216 125; Yuhtunga Jungle (Fire) !pos 491 20 301 123; Gilgamesh
// !pos 122.452 -9.009 -12.052 252. Section 1 (current): Norg's Gilgamesh plays 9. The Cloister of Frost, La Theine and
// Western Altepa headstones play 200 (the fragment) without their fragment, say ALREADY_HAVE_ALL_FRAGS with all seven
// and ALREADY_OBTAINED_FRAG (no action) with theirs. The Behemoth's Dominion headstone says ALREADY_OBTAINED_FRAG (no
// action) with its fragment; the Cape Teriggan, Zi'Tah and Yuhtunga ones say it only once their quest (Wandering Souls,
// Soul Searching, Wrath of the Opo-opos) is completed, else leave the char to the quest. Without the fragment, the four
// play 200 when the headstone's cooldown local var is past and their NMs are down, say SOMETHING_BETTER while one is
// up, and play 201 inside the cooldown. A 200 or 201 finish needs option 1 and the char within 1.92 yalms of the
// headstone (its x and z, the char's own y), else says MUST_MOVE_CLOSER (0, the fragment). 200's finish in an NM zone
// shows the zone's NPC text and spawns its NMs claimed by the char; in the three others it gives the fragment, as
// 201's finish does after adding the zone's quest (none at Behemoth's Dominion). The fragment says KEYITEM_OBTAINED,
// or, with all seven held, FOUND_ALL_FRAGS and completes the mission (the Bearer of the Eight Prayers title, Through
// the Quicksand Caves next). Section 2 (completed): Behemoth's Dominion, Cloister of Frost, La Theine and Western
// Altepa say ALREADY_HAVE_ALL_FRAGS, Cape Teriggan, Zi'Tah and Yuhtunga ZILART_MONUMENT, replacing the default (while
// that zone's quest is still accepted, the quest's own message answers there instead, as the traces show). Kept
// as the Lua has it: the NMs' deaths set the cooldown (their mob scripts, not this file); the Dark Fragment is not
// among the seven; the third arm of the three plain headstones repeats the first arm's test, so it always holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class HeadstonePilgrimage : MissionScript
    {
        public HeadstonePilgrimage() : base(XI.Quests.MissionLog.Zilart, "HEADSTONE_PILGRIMAGE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/05_Headstone_Pilgrimage";

        static readonly int[] RequiredFragments =
        {
            KeyItem.FireFragment,
            KeyItem.IceFragment,
            KeyItem.WindFragment,
            KeyItem.EarthFragment,
            KeyItem.LightningFragment,
            KeyItem.WaterFragment,
            KeyItem.LightFragment,
        };

        // Note: Dark Fragment is granted on complete for ZM4 and not checked here.
        static bool HasAllFragments(IQuestContext q)
        {
            foreach (int keyItemId in RequiredFragments)
            {
                if (!q.Player.HasKeyItem(keyItemId))
                {
                    return false;
                }
            }

            return true;
        }

        // A monument only answers a player who accepted its prompt and is close enough to it. After multiple captures,
        // the distance is set here at a uniform 1.92 yalms, which indicates the furthest activation range on retail.
        static bool CanUseHeadstone(IQuestContext q, ZoneText mustMoveCloser, int keyItemId)
        {
            if (q.Option != 1)
            {
                return false;
            }

            if (q.Player.DistanceTo(q.Npc.XPos, q.Player.YPos, q.Npc.ZPos) > 1.92)
            {
                // This message takes the key item as its second parameter.
                q.Text.Special(mustMoveCloser, 0, keyItemId);

                return false;
            }

            return true;
        }

        // Retail shows the all 8 fragments message in place of the key item message on the final fragment.
        static void GiveFragment(IQuestContext q, ZoneText keyItemObtained, ZoneText foundAllFrags, int keyItemId)
        {
            q.Player.AddKeyItem(keyItemId);

            if (!HasAllFragments(q))
            {
                q.Text.Special(keyItemObtained, keyItemId);

                return;
            }

            q.Text.Special(foundAllFrags, keyItemId);
            q.Mission.Complete();
        }

        // the onTrigger of the Cloister of Frost, La Theine and Western Altepa headstones
        static QuestReply PlainTrigger(IQuestContext q, int keyItemId, ZoneText alreadyHaveAll,
                                       ZoneText alreadyObtained)
        {
            if (!q.Player.HasKeyItem(keyItemId))
            {
                return QuestReply.ProgressEvent(200, keyItemId);
            }
            else if (HasAllFragments(q))
            {
                return QuestReply.MessageSpecial(alreadyHaveAll);
            }
            else if (q.Player.HasKeyItem(keyItemId))
            {
                q.Text.Special(alreadyObtained, keyItemId);

                return QuestReply.NoAction();
            }

            return null;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.BearerOfTheEightPrayers,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.ThroughTheQuicksandCaves,
            });

            b.State(current: true)
                .Npc(Zone.BehemothsDominion, Npc.BehemothsDominion.CermetHeadstone).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LightningFragment))
                    {
                        q.Text.Special(Text.BehemothsDominion.ALREADY_OBTAINED_FRAG, KeyItem.LightningFragment);

                        return QuestReply.NoAction();
                    }
                    else if (q.Player.SystemTime >= q.Npc.LocalVar("cooldown"))
                    {
                        if (!q.Player.MobIsSpawned("ANCIENT_WEAPON") && !q.Player.MobIsSpawned("LEGENDARY_WEAPON"))
                        {
                            return QuestReply.ProgressEvent(200, KeyItem.LightningFragment);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.BehemothsDominion.SOMETHING_BETTER);
                        }
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(201, KeyItem.LightningFragment);
                    }
                })
                .OnEventFinish(Zone.BehemothsDominion, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.BehemothsDominion.MUST_MOVE_CLOSER, KeyItem.LightningFragment))
                    {
                        return;
                    }

                    q.Npc.MessageText(Text.BehemothsDominion.AIR_AROUND_YOU_CHANGED);

                    q.Player.SpawnMob("ANCIENT_WEAPON", 0, true);
                    q.Player.SpawnMob("LEGENDARY_WEAPON", 0, true);
                })
                .OnEventFinish(Zone.BehemothsDominion, 201, q =>
                {
                    if (!CanUseHeadstone(q, Text.BehemothsDominion.MUST_MOVE_CLOSER, KeyItem.LightningFragment))
                    {
                        return;
                    }

                    GiveFragment(q, Text.BehemothsDominion.KEYITEM_OBTAINED, Text.BehemothsDominion.FOUND_ALL_FRAGS,
                        KeyItem.LightningFragment);
                })
                .Npc(Zone.CapeTeriggan, Npc.CapeTeriggan.CermetHeadstone).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.WindFragment))
                    {
                        // If the quest isn't completed, it's accepted by force, and we use quest trigger.
                        if (q.Player.HasCompletedQuest(QuestLog.Outlands, "WANDERING_SOULS"))
                        {
                            q.Text.Special(Text.CapeTeriggan.ALREADY_OBTAINED_FRAG, KeyItem.WindFragment);
                            return QuestReply.NoAction();
                        }
                    }
                    else
                    {
                        if (q.Player.SystemTime >= q.Npc.LocalVar("cooldown"))
                        {
                            if (!q.Player.MobIsSpawned("AXESARION_THE_WANDERER"))
                            {
                                return QuestReply.ProgressEvent(200, KeyItem.WindFragment);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.CapeTeriggan.SOMETHING_BETTER);
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(201, KeyItem.WindFragment); // Gives KI. Starts quest.
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.CapeTeriggan, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.CapeTeriggan.MUST_MOVE_CLOSER, KeyItem.WindFragment))
                    {
                        return;
                    }

                    q.Npc.MessageText(Text.CapeTeriggan.COLD_WIND_CHILLS_YOU);

                    q.Player.SpawnMob("AXESARION_THE_WANDERER", 0, true);
                })
                .OnEventFinish(Zone.CapeTeriggan, 201, q =>
                {
                    if (!CanUseHeadstone(q, Text.CapeTeriggan.MUST_MOVE_CLOSER, KeyItem.WindFragment))
                    {
                        return;
                    }

                    q.Player.AddQuest(QuestLog.Outlands, "WANDERING_SOULS");

                    GiveFragment(q, Text.CapeTeriggan.KEYITEM_OBTAINED, Text.CapeTeriggan.FOUND_ALL_FRAGS,
                        KeyItem.WindFragment);
                })
                .Npc(Zone.CloisterOfFrost, Npc.CloisterOfFrost.CermetHeadstone).OnTrigger(q =>
                    PlainTrigger(q, KeyItem.IceFragment, Text.CloisterOfFrost.ALREADY_HAVE_ALL_FRAGS,
                        Text.CloisterOfFrost.ALREADY_OBTAINED_FRAG))
                .OnEventFinish(Zone.CloisterOfFrost, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.CloisterOfFrost.MUST_MOVE_CLOSER, KeyItem.IceFragment))
                    {
                        return;
                    }

                    GiveFragment(q, Text.CloisterOfFrost.KEYITEM_OBTAINED, Text.CloisterOfFrost.FOUND_ALL_FRAGS,
                        KeyItem.IceFragment);
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.CermetHeadstone).OnTrigger(q =>
                    PlainTrigger(q, KeyItem.WaterFragment, Text.LaTheinePlateau.ALREADY_HAVE_ALL_FRAGS,
                        Text.LaTheinePlateau.ALREADY_OBTAINED_FRAG))
                .OnEventFinish(Zone.LaTheinePlateau, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.LaTheinePlateau.MUST_MOVE_CLOSER, KeyItem.WaterFragment))
                    {
                        return;
                    }

                    GiveFragment(q, Text.LaTheinePlateau.KEYITEM_OBTAINED, Text.LaTheinePlateau.FOUND_ALL_FRAGS,
                        KeyItem.WaterFragment);
                })
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(9))
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.CermetHeadstone).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LightFragment))
                    {
                        // If the quest isn't completed, it's accepted and we use quest trigger.
                        if (q.Player.HasCompletedQuest(QuestLog.Outlands, "SOUL_SEARCHING"))
                        {
                            q.Text.Special(Text.TheSanctuaryOfZitah.ALREADY_OBTAINED_FRAG, KeyItem.LightFragment);
                            return QuestReply.NoAction();
                        }
                    }
                    else
                    {
                        if (q.Player.SystemTime >= q.Npc.LocalVar("cooldown"))
                        {
                            if (!q.Player.MobIsSpawned("DOOMED_PILGRIMS"))
                            {
                                return QuestReply.ProgressEvent(200, KeyItem.LightFragment);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.SOMETHING_BETTER);
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(201, KeyItem.LightFragment); // Gives KI. Starts quest.
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.TheSanctuaryOfZitah, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.TheSanctuaryOfZitah.MUST_MOVE_CLOSER, KeyItem.LightFragment))
                    {
                        return;
                    }

                    q.Npc.MessageText(Text.TheSanctuaryOfZitah.AIR_HAS_SUDDENLY_CHANGED);

                    q.Player.SpawnMob("DOOMED_PILGRIMS", 0, true);
                })
                .OnEventFinish(Zone.TheSanctuaryOfZitah, 201, q =>
                {
                    if (!CanUseHeadstone(q, Text.TheSanctuaryOfZitah.MUST_MOVE_CLOSER, KeyItem.LightFragment))
                    {
                        return;
                    }

                    q.Player.AddQuest(QuestLog.Outlands, "SOUL_SEARCHING");

                    GiveFragment(q, Text.TheSanctuaryOfZitah.KEYITEM_OBTAINED, Text.TheSanctuaryOfZitah.FOUND_ALL_FRAGS,
                        KeyItem.LightFragment);
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.CermetHeadstone).OnTrigger(q =>
                    PlainTrigger(q, KeyItem.EarthFragment, Text.WesternAltepaDesert.ALREADY_HAVE_ALL_FRAGS,
                        Text.WesternAltepaDesert.ALREADY_OBTAINED_FRAG))
                .OnEventFinish(Zone.WesternAltepaDesert, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.WesternAltepaDesert.MUST_MOVE_CLOSER, KeyItem.EarthFragment))
                    {
                        return;
                    }

                    GiveFragment(q, Text.WesternAltepaDesert.KEYITEM_OBTAINED, Text.WesternAltepaDesert.FOUND_ALL_FRAGS,
                        KeyItem.EarthFragment);
                })
                .Npc(Zone.YuhtungaJungle, Npc.YuhtungaJungle.CermetHeadstone).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FireFragment))
                    {
                        // If the quest isn't completed, it's accepted and we use quest trigger.
                        if (q.Player.HasCompletedQuest(QuestLog.Outlands, "WRATH_OF_THE_OPO_OPOS"))
                        {
                            q.Text.Special(Text.YuhtungaJungle.ALREADY_OBTAINED_FRAG, KeyItem.FireFragment);
                            return QuestReply.NoAction();
                        }
                    }
                    else
                    {
                        if (q.Player.SystemTime >= q.Npc.LocalVar("cooldown"))
                        {
                            if (!q.Player.MobIsSpawned("TIPHA") && !q.Player.MobIsSpawned("CARTHI"))
                            {
                                return QuestReply.ProgressEvent(200, KeyItem.FireFragment);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.YuhtungaJungle.SOMETHING_BETTER);
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(201, KeyItem.FireFragment); // Gives KI. Starts quest.
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.YuhtungaJungle, 200, q =>
                {
                    if (!CanUseHeadstone(q, Text.YuhtungaJungle.MUST_MOVE_CLOSER, KeyItem.FireFragment))
                    {
                        return;
                    }

                    q.Npc.MessageText(Text.YuhtungaJungle.THE_OPO_OPOS_ATTACK);

                    q.Player.SpawnMob("TIPHA", 0, true);
                    q.Player.SpawnMob("CARTHI", 0, true);
                })
                .OnEventFinish(Zone.YuhtungaJungle, 201, q =>
                {
                    if (!CanUseHeadstone(q, Text.YuhtungaJungle.MUST_MOVE_CLOSER, KeyItem.FireFragment))
                    {
                        return;
                    }

                    q.Player.AddQuest(QuestLog.Outlands, "WRATH_OF_THE_OPO_OPOS");

                    GiveFragment(q, Text.YuhtungaJungle.KEYITEM_OBTAINED, Text.YuhtungaJungle.FOUND_ALL_FRAGS,
                        KeyItem.FireFragment);
                });

            // Section: Mission has been Completed
            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.HeadstonePilgrimage))
                .Npc(Zone.BehemothsDominion, Npc.BehemothsDominion.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.BehemothsDominion.ALREADY_HAVE_ALL_FRAGS)
                        .ReplaceDefault())
                .Npc(Zone.CapeTeriggan, Npc.CapeTeriggan.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.CapeTeriggan.ZILART_MONUMENT).ReplaceDefault())
                .Npc(Zone.CloisterOfFrost, Npc.CloisterOfFrost.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.CloisterOfFrost.ALREADY_HAVE_ALL_FRAGS).ReplaceDefault())
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.LaTheinePlateau.ALREADY_HAVE_ALL_FRAGS).ReplaceDefault())
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.ZILART_MONUMENT).ReplaceDefault())
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.WesternAltepaDesert.ALREADY_HAVE_ALL_FRAGS)
                        .ReplaceDefault())
                .Npc(Zone.YuhtungaJungle, Npc.YuhtungaJungle.CermetHeadstone)
                    .OnTrigger(QuestReply.MessageSpecial(Text.YuhtungaJungle.ZILART_MONUMENT).ReplaceDefault());
        }
    }
}
