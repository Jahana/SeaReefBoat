// The Secrets of Worship (Promathia 4-3, Log ID 6, Mission ID 428), a line-for-line port of
// scripts/missions/cop/4_3_The_Secrets_of_Worship.lua (quest rework C3). Justinius !pos 76 -34 68 26; Walnut Door
// !pos 111 -41 41 26; Iron Gate !pos 52.58 -24.1 740.02 25; Wooden Gate (_0s8) !pos 45.5 -1.5 10 28. The mission
// current: Tavnazian Safehold's _0qa plays 111 at Status 0 (its finish sets Status 1), Justinius plays 131 as an
// important event at Status 0 and Parelbriaux 137 at Status 1; Misareaux Coast's _0p8 plays cutscene 9 at Status 1 (its
// finish with option 1 sets Status 2 and sets the char down in the Sacrarium) and 502 above Status 1 (its finish with
// option 0 sets the char down there too). In the Sacrarium the Wooden Gate (_0s8) answers CANNOT_OPEN_SIDE west of x 45
// and east of it plays cutscene 6 at Status 2 (finish: Status 3) and cutscene 5 at Status 3 with the Reliquarium Key
// (finish: the mission completes). At Status 3 without the key the six qm_prof qms either hand the key over (Mariselle
// was killed: the mission's hasKilledMariselle local var, set by her death at Status 3), or say DRAWER_SHUT while she
// is spawned, or set the spawn points of her and her two pupils to the qm and pop her (a qm with her
// hasProfessorMariselle local var set; EVIL_PRESENCE). A mission that is not current plays cutscene 110 at the gate
// east of x 45 with the key (the handler returns no reply, so the gate's default CANNOT_BE_OPENED follows the cutscene,
// as the Lua's bare startCutscene leaves it), and answers CANNOT_OPEN_SIDE west of it. The Lua's
// `if not mariselle then return end` is not ported: Mariselle is a mob of the zone's data, never nil (a removed dead
// guard). Slanderous Utterings is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheSecretsOfWorship : MissionScript
    {
        public TheSecretsOfWorship() : base(XI.Quests.MissionLog.CoP, "THE_SECRETS_OF_WORSHIP") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/4_3_The_Secrets_of_Worship";

        // the six qm_prof qms' handler
        static QuestReply ProfQm(IQuestContext q)
        {
            if (q.Mission.Var("Status") == 3 && !q.Player.HasKeyItem(KeyItem.ReliquiariumKey))
            {
                // Has already fought Mariselle.
                if (q.Mission.LocalVar("hasKilledMariselle") == 1)
                {
                    q.Player.GiveKeyItem(KeyItem.ReliquiariumKey);
                    return QuestReply.NoAction();
                }

                if (q.Npc.LocalVar("hasProfessorMariselle") == 1)
                {
                    // Hasn't fought Mariselle, but it's popped.
                    if (q.Player.MobIsSpawned("OLD_PROFESSOR_MARISELLE"))
                    {
                        return QuestReply.MessageSpecial(Text.Sacrarium.DRAWER_SHUT);
                    }

                    // Pop Mariselle.
                    q.Player.SetMobSpawn("OLD_PROFESSOR_MARISELLE", 0, q.Npc.XPos, q.Npc.YPos, q.Npc.ZPos, 0);
                    for (int i = 1; i <= 2; i++)
                    {
                        q.Player.SetMobSpawn("OLD_PROFESSOR_MARISELLE", i, q.Npc.XPos, q.Npc.YPos, q.Npc.ZPos, 0);
                    }

                    q.Player.PopFromQm("OLD_PROFESSOR_MARISELLE", true, 0, 2);
                    return QuestReply.MessageSpecial(Text.Sacrarium.EVIL_PRESENCE);
                }
            }

            return null;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.SlanderousUtterings,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0qa).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(111) : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.Event(131).ImportantEvent() : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Parelbriaux).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(137) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 111, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p8).OnTrigger(q =>
                {
                    int status = q.Mission.Var("Status");

                    if (status == 1)
                    {
                        return QuestReply.ProgressCutscene(9);
                    }

                    return status > 1 ? QuestReply.Event(502) : null;
                })
                .OnEventFinish(Zone.MisareauxCoast, 9, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Mission.SetVar("Status", 2);
                        q.Player.SetPos(-220.075, -15.999, 79.634, 62, 28);
                    }
                })
                .OnEventFinish(Zone.MisareauxCoast, 502, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetPos(-220.075, -15.999, 79.634, 62, 28);
                    }
                })
                .Npc(Zone.Sacrarium, Npc.Sacrarium._0s8).OnTrigger(q =>
                {
                    int status = q.Mission.Var("Status");

                    if (q.Player.XPos > 45)
                    {
                        if (status == 2)
                        {
                            return QuestReply.ProgressCutscene(6, 0, KeyItem.ReliquiariumKey);
                        }

                        if (status == 3 && q.Player.HasKeyItem(KeyItem.ReliquiariumKey))
                        {
                            return QuestReply.ProgressCutscene(5);
                        }

                        return null;
                    }

                    return QuestReply.MessageSpecial(Text.Sacrarium.CANNOT_OPEN_SIDE);
                })
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmprof0).OnTrigger(ProfQm)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmprof1).OnTrigger(ProfQm)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmprof2).OnTrigger(ProfQm)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmprof3).OnTrigger(ProfQm)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmprof4).OnTrigger(ProfQm)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.qmprof5).OnTrigger(ProfQm)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.OldProfessorMariselle).OnMobDeath(q =>
                {
                    if (q.Mission.Var("Status") == 3) q.Mission.SetLocalVar("hasKilledMariselle", 1);
                })
                .OnEventFinish(Zone.Sacrarium, 5, q => q.Mission.Complete())
                .OnEventFinish(Zone.Sacrarium, 6, q => q.Mission.SetVar("Status", 3));

            b.State(current: false)
                .Npc(Zone.Sacrarium, Npc.Sacrarium._0s8).OnTrigger(q =>
                {
                    if (q.Player.XPos > 45)
                    {
                        if (q.Player.HasKeyItem(KeyItem.ReliquiariumKey))
                        {
                            q.Player.StartCutscene(110);
                        }

                        return null;
                    }

                    return QuestReply.MessageSpecial(Text.Sacrarium.CANNOT_OPEN_SIDE);
                });
        }
    }
}
