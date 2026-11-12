// When Angels Fall (Promathia 8-3, Log ID 6, Mission ID 828), a line-for-line port of
// scripts/missions/cop/8_3_When_Angels_Fall.lua (quest rework C3, C4). The mission current. The Garden of Ru'Hmet: the
// zone-in plays 201 at Status 0 (its finish gives Prishe's Mysterious Amulet, Status 1). The five Ebon Panels (_iz2
// for races 1-2, Ebon_Panel_Elvaan 3-4, _Taru 5-6, _Mithra 7, _Galka 8) play 202 at Status 1 (Status 2); at Status 2
// the char's own race panel plays its event (120, 121, 123, 124, 122) within 1.8 yalms, else says
// YOU_MUST_MOVE_CLOSER; the panel's finish with option 1 gives its light, the Warrior of the Crystal title and Status
// 3. _0z0 plays 203 at Status 3 (Status 4) and the event 205 at Status 5; _0zt plays 204 at Status 5 (Status 6). _0zu
// and _0zv play the optional cutscenes 110 and 111 (cs_option 0, skippable) while the Brand of Dawn and the Brand of
// Twilight are not held; their finish with option 1 gives the brand. The 32001 finish with When Angels Fall won
// (battlefield.when_angels_fall sets battlefieldWin) at Status 4 sets Status 5 (the battlefield moves the char).
// Al'Taieu's zone-in plays 165 at Status 6; its finish takes the amulet (RETURN_AMULET_TO_PRISHE) and completes the
// mission. Dawn is next; no title.
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class WhenAngelsFall : MissionScript
    {
        public WhenAngelsFall() : base(XI.Quests.MissionLog.CoP, "WHEN_ANGELS_FALL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/8_3_When_Angels_Fall";

        // ebonPanel: the basic race, the event and the light of each panel
        static readonly Dictionary<string, (int Race, int Csid, int Light)> EbonPanel =
            new Dictionary<string, (int Race, int Csid, int Light)>
            {
                [Npc.TheGardenOfRuhmet.iz2] = (1, 120, KeyItem.LightOfVahzl),
                [Npc.TheGardenOfRuhmet.EbonPanelElvaan] = (2, 121, KeyItem.LightOfMea),
                [Npc.TheGardenOfRuhmet.EbonPanelTaru] = (3, 123, KeyItem.LightOfHolla),
                [Npc.TheGardenOfRuhmet.EbonPanelMithra] = (4, 124, KeyItem.LightOfDem),
                [Npc.TheGardenOfRuhmet.EbonPanelGalka] = (5, 122, KeyItem.LightOfAltaieu),
            };

        // ebonPanelOnTrigger
        static QuestReply EbonPanelOnTrigger(IQuestContext q)
        {
            int missionStatus = q.Mission.Var("Status");

            if (missionStatus == 1)
            {
                return QuestReply.ProgressEvent(202);
            }
            else if (missionStatus == 2)
            {
                // player:getRace() <= xi.race.MITHRA and math.ceil(player:getRace() / 2) or 5
                int basicRace = q.Player.Race <= 7 ? (q.Player.Race + 1) / 2 : 5;
                var panelData = EbonPanel[q.Npc.Name];

                if (basicRace == panelData.Race)
                {
                    if (q.Npc.PlayerDistance <= 1.8)
                    {
                        return QuestReply.ProgressEvent(panelData.Csid);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.TheGardenOfRuhmet.YOU_MUST_MOVE_CLOSER);
                    }
                }
            }

            return null;
        }

        // ebonPanelOnEventFinish
        static void EbonPanelOnEventFinish(IQuestContext q)
        {
            if (q.Option == 1)
            {
                q.Player.GiveKeyItem(EbonPanel[q.Npc.Name].Light);
                q.Player.AddTitle(Title.WarriorOfTheCrystal);
                q.Mission.SetVar("Status", 3);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.Dawn,
            });

            b.State(current: true)
                .OnZoneIn(Zone.TheGardenOfRuhmet, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(201) : null)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet.iz2).OnTrigger(EbonPanelOnTrigger)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet.EbonPanelElvaan).OnTrigger(EbonPanelOnTrigger)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet.EbonPanelTaru).OnTrigger(EbonPanelOnTrigger)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet.EbonPanelMithra).OnTrigger(EbonPanelOnTrigger)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet.EbonPanelGalka).OnTrigger(EbonPanelOnTrigger)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet._0z0).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(203);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.Event(205);
                    }

                    return null;
                })
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet._0zt).OnTrigger(q =>
                    q.Mission.Var("Status") == 5 ? QuestReply.ProgressEvent(204) : null)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet._0zu).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.BrandOfDawn)
                        ? QuestReply.ProgressOptionalCutscene(110).CsOption(0).CanSkip()
                        : null)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet._0zv).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.BrandOfTwilight)
                        ? QuestReply.ProgressOptionalCutscene(111).CsOption(0).CanSkip()
                        : null)
                .OnEventFinish(Zone.TheGardenOfRuhmet, 110, q =>
                {
                    if (q.Option == 1) q.Player.GiveKeyItem(KeyItem.BrandOfDawn);
                })
                .OnEventFinish(Zone.TheGardenOfRuhmet, 111, q =>
                {
                    if (q.Option == 1) q.Player.GiveKeyItem(KeyItem.BrandOfTwilight);
                })
                .OnEventFinish(Zone.TheGardenOfRuhmet, 120, EbonPanelOnEventFinish)
                .OnEventFinish(Zone.TheGardenOfRuhmet, 121, EbonPanelOnEventFinish)
                .OnEventFinish(Zone.TheGardenOfRuhmet, 122, EbonPanelOnEventFinish)
                .OnEventFinish(Zone.TheGardenOfRuhmet, 123, EbonPanelOnEventFinish)
                .OnEventFinish(Zone.TheGardenOfRuhmet, 124, EbonPanelOnEventFinish)
                .OnEventFinish(Zone.TheGardenOfRuhmet, 201, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MysteriousAmuletPrishe);
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.TheGardenOfRuhmet, 202, q => q.Mission.SetVar("Status", 2))
                .OnEventFinish(Zone.TheGardenOfRuhmet, 203, q => q.Mission.SetVar("Status", 4))
                .OnEventFinish(Zone.TheGardenOfRuhmet, 204, q => q.Mission.SetVar("Status", 6))
                .OnEventFinish(Zone.TheGardenOfRuhmet, 32001, q =>
                {
                    // NOTE (Lua): Moving the player for this event is handled in the battlefield script.
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.WhenAngelsFall &&
                        q.Mission.Var("Status") == 4)
                    {
                        q.Mission.SetVar("Status", 5);
                    }
                })
                .OnZoneIn(Zone.Altaieu, q => q.Mission.Var("Status") == 6 ? QuestReply.Event(165) : null)
                .OnEventFinish(Zone.Altaieu, 165, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MysteriousAmuletPrishe);
                    q.Text.Special(Text.Altaieu.RETURN_AMULET_TO_PRISHE, KeyItem.MysteriousAmulet);
                    q.Mission.Complete();
                });
        }
    }
}
