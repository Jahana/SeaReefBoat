// The First Meeting (Log ID 1, Quest ID 52, MNK AF2), a line-for-line port of
// scripts/quests/bastok/MNK_AF2_The_First_Meeting.lua (quest rework B3b). Oggbi : !pos -159 -7 5 236; Hide Flap (2) : !pos
// -124 3 -43 149. Oggbi offers it (233) once Ghosts of the Past is completed and its mustZone cleared. The zone-in to
// Fei'Yin from Qubia Arena without the Letter from Dalzakk plays 16, which gives it; the hide flap, searched from within a
// yalm (108), spawns Bilopdop (claimed) and Deloknok (on enmity) for a letter holder while neither is up; their deaths set
// nmKilled bits 0 and 1 (a quest local var) unless the San d'Orian Martial Arts Scroll is held, and with both the scroll is
// found. Oggbi's 234 with the letter and the scroll completes it. The zone-in's prevZone is player:getPreviousZone() (the
// same value).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheFirstMeeting : QuestScript
    {
        public TheFirstMeeting() : base(QuestLog.Bastok, "THE_FIRST_MEETING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/MNK_AF2_The_First_Meeting";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.TempleGaiters });

            b.State(QuestStatus.Available, when: q =>
                    !q.Quest.MustZone &&
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "GHOSTS_OF_THE_PAST") &&
                    q.Player.MainJob == Job.Mnk &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.PortBastok, Npc.PortBastok.Oggbi).OnTrigger(QuestReply.ProgressEvent(233))
                .OnEventFinish(Zone.PortBastok, 233, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Davoi, Npc.Davoi.HideFlap2).OnTrigger(q =>
                {
                    if (q.Npc.PlayerDistance <= 1)
                    {
                        return QuestReply.ProgressEvent(108);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.Davoi.CLOSER_TO_SEARCH);
                    }
                })
                .OnEventFinish(Zone.Davoi, 108, q =>
                {
                    if (q.Option != 0)
                    {
                        return;
                    }

                    if (q.Player.HasKeyItem(KeyItem.SanDorianMartialArtsScroll))
                    {
                        q.Text.Special(Text.Davoi.YOU_FIND_NOTHING);
                    }
                    else if (q.Quest.LocalVar("nmKilled") == 3)
                    {
                        q.Player.GiveKeyItem(KeyItem.SanDorianMartialArtsScroll);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.LetterFromDalzakk) &&
                             !q.Player.MobIsSpawned(Mob.Davoi.BILOPDOP) &&
                             !q.Player.MobIsSpawned(Mob.Davoi.DELOKNOK))
                    {
                        q.Text.Special(Text.Davoi.YOU_FIND_NOTHING);
                        q.Player.SpawnMob(Mob.Davoi.BILOPDOP, 0, claim: true);
                        q.Player.SpawnMob(Mob.Davoi.DELOKNOK, 0, claim: false);
                    }
                    else
                    {
                        q.Text.Special(Text.Davoi.YOU_FIND_NOTHING);
                    }
                })
                .Npc(Zone.Davoi, Npc.Davoi.Bilopdop).OnMobDeath(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.SanDorianMartialArtsScroll))
                    {
                        int nmStatus = q.Quest.LocalVar("nmKilled");

                        q.Quest.SetLocalVar("nmKilled", nmStatus | 1 << 0);
                    }
                })
                .Npc(Zone.Davoi, Npc.Davoi.Deloknok).OnMobDeath(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.SanDorianMartialArtsScroll))
                    {
                        int nmStatus = q.Quest.LocalVar("nmKilled");

                        q.Quest.SetLocalVar("nmKilled", nmStatus | 1 << 1);
                    }
                })
                .OnZoneIn(Zone.Feiyin, q =>
                {
                    if (q.Player.PreviousZone == Zone.QubiaArena && !q.Player.HasKeyItem(KeyItem.LetterFromDalzakk))
                    {
                        return QuestReply.Event(16);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Feiyin, 16, q => q.Player.GiveKeyItem(KeyItem.LetterFromDalzakk))
                .Npc(Zone.PortBastok, Npc.PortBastok.Oggbi).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LetterFromDalzakk) &&
                        q.Player.HasKeyItem(KeyItem.SanDorianMartialArtsScroll))
                    {
                        return QuestReply.ProgressEvent(234);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 234, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.LetterFromDalzakk);
                        q.Player.DelKeyItem(KeyItem.SanDorianMartialArtsScroll);
                    }
                });
        }
    }
}
