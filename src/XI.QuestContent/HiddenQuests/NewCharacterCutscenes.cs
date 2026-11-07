// New Character Cutscenes (hidden quest newCharacterCS), ported from
// scripts/quests/hiddenQuests/New_Character_Cutscenes.lua
// (quest rework B3b hidden quests). Char creation writes HQuest[newCharacterCS]notSeen 1 (XI.Login/ViewSession.cs:230,
// with NEW_CHARACTER_CUTSCENE); the nation change NPCs (Rakano-Marukano, Mythily, Beriphaule) write it again.
// One section (notSeen 1 and NEW_CHARACTER_CUTSCENE 1): each starting city's zone-in sets the char at the scene's exit
// (Windurst Waters and Port Windurst: first at the home point's spot when no nation was seen), sets the home point when
// no nation was seen yet (nations 0), and plays the opening cutscene { csid, -1, flags } (RESET_CAMERA, NO_PCS,
// OPENING_MODE; Windurst Waters and Woods NO_NPCS too): Bastok Markets 0, Bastok Mines 1, Port Bastok 1, Northern San
// d'Oria 535, Southern San d'Oria 503, Port San d'Oria 500, Windurst Waters 531, Windurst Woods 367, Port Windurst 305.
// Bastok Markets' 0 finish starts 7 (the same flags, isHidden); every scene's last finish gives the Adventurer's Coupon
// (npcUtil.giveItem: no second chance), shows MAP_MARKER_TUTORIAL (the char's own messageText; the order of the two
// differs by city), notSeen 0, the nation's bit in nations. Windurst Waters' 531 update: updateEvent({ [7] = 0 }).
// The zone-in returns the Lua table { csid, -1, flags } (luautils::OnZoneIn: id, text table, flags): the reply's
// .TextTable(-1).Flags(flags).
// Kept as the Lua has it: the coupon is given whatever the inventory (a full one shows its message and the coupon is
// lost); the cities' scene exits and home point spots as the Lua sets them; Bastok Markets' 7 is started by the finish.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class NewCharacterCutscenes : HiddenQuestScript
    {
        public NewCharacterCutscenes() : base("newCharacterCS") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/New_Character_Cutscenes";

        // xi.cutsceneFlag
        private const uint ResetCamera = 0x01;
        private const uint NoPcs = 0x02;
        private const uint NoNpcs = 0x10;
        private const uint OpeningMode = 0x80;

        private const uint CutsceneFlags = ResetCamera | NoPcs | OpeningMode;
        private const uint CutsceneFlagsNoNpcs = ResetCamera | NoPcs | NoNpcs | OpeningMode;

        // the zone-in's { csid, -1, flags }
        private static QuestReply OpeningCutscene(int csid, uint flags) =>
            QuestReply.Event(csid).TextTable(-1).Flags(flags);

        // the set-up shared by the zone-ins: the scene's exit, the home point when no nation was seen
        private static void SetUp(IQuestContext q, double x, double y, double z, int rot)
        {
            q.Player.SetPos(x, y, z, rot);
            if (q.Quest.Var("nations") == 0)
            {
                q.Player.SetHomePoint();
            }
        }

        // utils.mask.setBit(nationsSeen, nation, true)
        private static void SetNationSeen(IQuestContext q, int nation)
        {
            q.Quest.SetVar("notSeen", 0);
            int nationsSeen = q.Quest.Var("nations");
            q.Quest.SetVar("nations", nationsSeen | (1 << nation));
        }

        // the coupon, then the marker text (couponFirst) or the other order
        private static void LastFinish(IQuestContext q, int nation, bool couponFirst)
        {
            var marker = new ZoneText(q.Zone, "MAP_MARKER_TUTORIAL");

            if (couponFirst)
            {
                // If you don't get the coupon, tough luck. Retail doesn't give you a chance to get it again.
                q.Player.GiveItem(Item.AdventurerCoupon);
                q.Text.MessageTextFromPlayer(marker);
            }
            else
            {
                q.Text.MessageTextFromPlayer(marker);
                // If you don't get the coupon, tough luck. Retail doesn't give you a chance to get it again.
                q.Player.GiveItem(Item.AdventurerCoupon);
            }

            SetNationSeen(q, nation);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Note:
            // Some areas swap order of when you get the Adventurer's coupon and the marker help text.
            // On retail, even if your inventory is full and change nations, the starting cutscene will attempt to
            // give you the Adventurer's coupon and report your inventory is full.
            b.AnyState(when: q =>
                    q.Quest.Var("notSeen") == 1 &&
                    q.Settings.MainEquals("NEW_CHARACTER_CUTSCENE", 1))
                .OnZoneIn(Zone.BastokMarkets, q =>
                {
                    SetUp(q, -280, -12, -90, 0);
                    return OpeningCutscene(0, CutsceneFlags); // CS 0 is not a typo.
                })
                .OnEventFinish(Zone.BastokMarkets, 0, q =>
                {
                    // Retail normally zones you for this -- but testing proved there was no difference other than
                    // NOT zoning by just calling the next event. event 0x00 fades your screen to black and seems to
                    // wait for another event, otherwise it will never fade back in. I think retail uses zoning to set
                    // the "isHidden" state we use for events.

                    q.Player.StartEventFlags(7, CutsceneFlags, true);
                })
                .OnEventFinish(Zone.BastokMarkets, 7, q => LastFinish(q, Nation.Bastok, couponFirst: true))

                .OnZoneIn(Zone.BastokMines, q =>
                {
                    SetUp(q, -45, -0.0, 25, 192);
                    return OpeningCutscene(1, CutsceneFlags);
                })
                .OnEventFinish(Zone.BastokMines, 1, q => LastFinish(q, Nation.Bastok, couponFirst: true))

                .OnZoneIn(Zone.PortBastok, q =>
                {
                    SetUp(q, 134, 8.5, -11, 96);
                    return OpeningCutscene(1, CutsceneFlags);
                })
                .OnEventFinish(Zone.PortBastok, 1, q => LastFinish(q, Nation.Bastok, couponFirst: false))

                .OnZoneIn(Zone.NorthernSanDoria, q =>
                {
                    SetUp(q, 0, 0, -12, 192);
                    return OpeningCutscene(535, CutsceneFlags);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 535, q => LastFinish(q, Nation.Sandoria, couponFirst: false))

                .OnZoneIn(Zone.SouthernSanDoria, q =>
                {
                    SetUp(q, -100, 1, -40, 224); // cs exit
                    return OpeningCutscene(503, CutsceneFlags);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 503, q => LastFinish(q, Nation.Sandoria, couponFirst: false))

                .OnZoneIn(Zone.PortSanDoria, q =>
                {
                    SetUp(q, -100, -8, -125, 224);
                    return OpeningCutscene(500, CutsceneFlags);
                })
                .OnEventFinish(Zone.PortSanDoria, 500, q => LastFinish(q, Nation.Sandoria, couponFirst: true))

                .OnZoneIn(Zone.WindurstWaters, q =>
                {
                    // The HP position is slightly different than the CS exit.
                    if (q.Quest.Var("nations") == 0)
                    {
                        q.Player.SetPos(-40.0, -5, 100, 64);
                        q.Player.SetHomePoint();
                    }

                    q.Player.SetPos(-40.611, -5, 102.5, 57); // Move back to CS exit position

                    return OpeningCutscene(531, CutsceneFlagsNoNpcs);
                })
                .OnEventUpdate(Zone.WindurstWaters, 531, q =>
                {
                    // param 7 is checked in the event according to decomp, but it was never set on Hume F starting a
                    // new character. param 7 was 0x20012 on a galka changing nations.
                    // player:updateEvent({ [7] = 0 }): the table form sends the same 0x05C as eight zeros
                    q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.WindurstWaters, 531, q => LastFinish(q, Nation.Windurst, couponFirst: false))

                .OnZoneIn(Zone.WindurstWoods, q =>
                {
                    SetUp(q, 30, 2, -40, 128);
                    return OpeningCutscene(367, CutsceneFlagsNoNpcs);
                })
                .OnEventFinish(Zone.WindurstWoods, 367, q => LastFinish(q, Nation.Windurst, couponFirst: false))

                .OnZoneIn(Zone.PortWindurst, q =>
                {
                    if (q.Quest.Var("nations") == 0)
                    {
                        q.Player.SetPos(-120, -5.5, 175, 48);
                        q.Player.SetHomePoint();
                    }

                    q.Player.SetPos(-140, -7, 172, 32);

                    return OpeningCutscene(305, CutsceneFlags);
                })
                .OnEventFinish(Zone.PortWindurst, 305, q => LastFinish(q, Nation.Windurst, couponFirst: false));
        }
    }
}
