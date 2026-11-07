// Lost Chick (hidden quest LostChick), ported from scripts/quests/hiddenQuests/Lost_Chick.lua (quest rework B3b hidden
// quests). Hantileon : !pos -2.675 -0.100 -105.287 230; Zopago : !pos 51.706 0.874 -109.065 234; Pulonono : !pos
// 130.124 -6.350 -119.341 241; the owners: xi.chocoboRaising.walks.chickOwners. A chick's short walk finds a lost
// chick, trainers met on later walks give clues to its owner, and the stable clerk reviews them (all in the chocobo
// raising library, which stays Lua). The right owner teaches the diligent story; any guess ends the search, and the
// trainer reports how it went. State: the char var [ChocoboRaising]LostChick (walks.lostChickVar), packed by walks.lua
// (owner 0-3, clues 4-5, location 6-7, 2 bits per clue trainer 1-4 from 8, result 16-17, solved 18). Sections 1-3
// (ENABLE_CHOCOBO_RAISING, a chick lost at that location: owner ~= 0): each owner of the location (the table below)
// answers its events' updates: the ask option: the local Asked 1, updateEvent(1, 1, 1); the guess option (after the
// ask, for an owner with one): walks.askOwner (right: owner and location match), updateEvent(answer x3), the effects
// (right: the Story of a Diligent Chocobo key item; the var reset: result RETURNED / WRONG_OWNER, solved kept or set),
// right: KEYITEM_OBTAINED; another option at Violitte (a map vendor): xi.maps.onEventUpdate(player, 595, ...). An owner
// without an ask option plays its question event on trigger (priorityEvent(event, 1)). Section 4
// (ENABLE_CHOCOBO_RAISING, a result reported): the location's trainer (Hantileon 852, Zopago 542, Pulonono 766) plays {
// [0] = 1 - returned, [1] = returned, [7] = location }; the finish clears the result. Kept as the Lua has it: the
// owners' table is a copy of walks.chickOwners (walks.lua:114-150: the TODO owners Abd-al-Raziq, Azima, Kyaa Taali,
// Kopuro-Popuro, Peshi Yohnts and Ponono have no entry, so they never answer); the var's unpack and repack
// (walks.lostChick / packLostChick / askOwner / clearLostChickResult) are written out here, so a repack keeps bits 0-18
// only; the update handlers answer any event of those ids in the zone and test the npc's name; a guess at an owner with
// an ask option is ignored until the ask (Asked is a local var: a zone change clears it).
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class LostChick : HiddenQuestScript
    {
        public LostChick() : base("LostChick") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Lost_Chick";

        // walks.lostChickVar (walks.lua:19)
        private const string LostChickVar = "[ChocoboRaising]LostChick";

        // walks.lostChickResult (walks.lua:94-99)
        private const int ResultNone = 0;
        private const int ResultReturned = 1;
        private const int ResultWrongOwner = 2;

        // a walks.chickOwners entry; MapVendorEvent: the owner's map shop event (Violitte's 595, xi.maps' own id for
        // her)
        private sealed class Owner
        {
            public Owner(int id, string npc, int[] events, int? ask, int guess, int? mapVendorEvent = null)
            {
                Id = id;
                NpcName = npc;
                Events = events;
                Ask = ask;
                Guess = guess;
                MapVendorEvent = mapVendorEvent;
            }

            public int Id { get; }
            public string NpcName { get; }
            public int[] Events { get; }
            public int? Ask { get; }
            public int Guess { get; }
            public int? MapVendorEvent { get; }
        }

        // walks.chickOwners (walks.lua:114-150), in the clerk's order. ask: the option that offers the question inline;
        // guess: the option naming the owner.
        private static readonly Dictionary<int, Owner[]> ChickOwners = new Dictionary<int, Owner[]>
        {
            [1] = new[]
            {
                new Owner(1, Npc.SouthernSanDoria.Coderiant,   new[] { 583 }, 1, 2),
                new Owner(2, Npc.SouthernSanDoria.Cahaurme,    new[] { 847 }, null, 1),
                new Owner(3, Npc.SouthernSanDoria.Victoire,    new[] { 848 }, null, 1),
                new Owner(4, Npc.SouthernSanDoria.Corua,       new[] { 849 }, null, 1),
                new Owner(5, Npc.SouthernSanDoria.Luthiaque,   new[] { 658 }, 1, 2),
                new Owner(6, Npc.SouthernSanDoria.Lanqueron,   new[] { 850 }, null, 1),
                new Owner(7, Npc.SouthernSanDoria.Violitte,    new[] { 851 }, null, 2, 595),
                new Owner(8, Npc.SouthernSanDoria.Valderotaux, new[] { 58 },  1, 2),
            },
            [2] = new[]
            {
                new Owner(1, Npc.BastokMines.Drangord, new[] { 21 },     1, 2),
                new Owner(2, Npc.BastokMines.Deegis,   new[] { 537 },    null, 1),
                new Owner(3, Npc.BastokMines.GrayWolf, new[] { 19 },     1, 2),
                new Owner(4, Npc.BastokMines.Gerbaum,  new[] { 22, 23 }, 1, 2),
                new Owner(5, Npc.BastokMines.Griselda, new[] { 538 },    null, 1),
                new Owner(6, Npc.BastokMines.Galdeo,   new[] { 539 },    null, 1),
                // TODO: Abd-al-Raziq (7) and Azima (8) ask inside their guild shop events.
            },
            [3] = new[]
            {
                // TODO: Kyaa Taali (1) asks inside her guild shop event.
                new Owner(2, Npc.WindurstWoods.KapehMyohrye,       new[] { 340 },           1, 2),
                // TODO: Kopuro-Popuro (3) asks with option 2 and guesses with option 100; confirm the event.
                new Owner(4, Npc.WindurstWoods.Kororo,             new[] { 277 },           2, 3),
                new Owner(5, Npc.WindurstWoods.GiohAjihri,         new[] { 424 },           1, 2),
                new Owner(6, Npc.WindurstWoods.GottahMaporushanoh, new[] { 420, 483, 486 }, 1, 2),
                // TODO: Peshi Yohnts (7) and Ponono (8) ask inside their guild shop events.
            },
        };

        private static readonly Dictionary<int, ushort> OwnerZoneIds = new Dictionary<int, ushort>
        {
            [1] = Zone.SouthernSanDoria,
            [2] = Zone.BastokMines,
            [3] = Zone.WindurstWoods,
        };

        // walks.lostChick(value) (walks.lua:331-347): the fields the quest reads
        private static int ChickOwner(int value) => value & 0xF;
        private static int ChickLocation(int value) => (value >> 6) & 3;
        private static int ChickResult(int value) => (value >> 16) & 3;
        private static bool ChickSolved(int value) => ((value >> 18) & 1) == 1;

        // walks.packLostChick(walks.lostChick(value)): the unpacked fields repacked (bits 0-18; the clue trainers 1-4
        // are bits 8-15)
        private const int PackedMask = 0x7FFFF;

        // walks.clearLostChickResult(value) (walks.lua:398-403): result NONE, the rest repacked
        private static int ClearLostChickResult(int value) => (value & PackedMask) & ~(3 << 16) | (ResultNone << 16);

        // chickAt(player, location) (Lost_Chick.lua:82-90)
        private static bool ChickAt(IQuestCheck q, int location)
        {
            if (!q.Settings.MainTruthy("ENABLE_CHOCOBO_RAISING"))
            {
                return false;
            }

            int chick = q.Player.CharVar(LostChickVar);

            return ChickOwner(chick) != 0 && ChickLocation(chick) == location;
        }

        // ownerUpdate(location, ownerId, owner) (Lost_Chick.lua:20-56)
        private static void OwnerUpdate(IQuestContext q, int location, Owner owner)
        {
            // npc:getName()
            if (q.Npc == null || q.Npc.Name != owner.NpcName)
            {
                return;
            }

            int choice = q.Option & 0xFFFF;

            // Inline events offer the question only when p1 is 1.
            if (owner.Ask.HasValue && choice == owner.Ask.Value)
            {
                q.Quest.SetLocalVar("Asked", 1);
                q.Player.UpdateEvent(1, 1, 1);
            }
            else if (choice == owner.Guess)
            {
                // An inline event's own options share the guess value, so only the one after the ask counts.
                if (owner.Ask.HasValue)
                {
                    if (q.Quest.LocalVar("Asked") == 0)
                    {
                        return;
                    }

                    q.Quest.SetLocalVar("Asked", 0);
                }

                // walks.askOwner(player:getCharVar(walks.lostChickVar), location, ownerId) (walks.lua:371-394)
                int chick = q.Player.CharVar(LostChickVar);
                bool right = ChickOwner(chick) != 0 &&
                    ChickLocation(chick) == location &&
                    ChickOwner(chick) == owner.Id;
                int after = ((right ? ResultReturned : ResultWrongOwner) << 16) |
                    ((right || ChickSolved(chick)) ? 1 << 18 : 0);
                int answer = right ? 1 : 0;

                // Each owner event reads the answer from a different param, so it goes in p0 to p2.
                q.Player.UpdateEvent(answer, answer, answer);

                // xi.chocoboRaising.applyEffects(player, effects): ADD_KEY_ITEM (right only), then SET_CHAR_VAR
                if (right)
                {
                    q.Player.AddKeyItem(KeyItem.StoryOfADiligentChocobo);
                }

                q.Player.SetCharVar(LostChickVar, after);

                if (right)
                {
                    q.Text.Special(new ZoneText(q.Zone, "KEYITEM_OBTAINED"), KeyItem.StoryOfADiligentChocobo);
                }
            }
            else if (owner.MapVendorEvent.HasValue)
            {
                // xi.maps.onEventUpdate(player, owner.mapVendorEvent, option, npc) (the Lua global, with the handler's
                // npc)
                q.Player.MapsOnEventUpdate(owner.MapVendorEvent.Value, q.Option);
            }
        }

        // ownerZone(location) (Lost_Chick.lua:59-73): owners without an ask option play their question event on trigger
        private static void OwnerZone(QuestStateBuilder state, int location)
        {
            ushort zone = OwnerZoneIds[location];

            foreach (Owner owner in ChickOwners[location])
            {
                if (!owner.Ask.HasValue)
                {
                    state.Npc(zone, owner.NpcName).OnTrigger(QuestReply.PriorityEvent(owner.Events[0], 1));
                }

                foreach (int csid in owner.Events)
                {
                    Owner o = owner;
                    state.OnEventUpdate(zone, csid, q => OwnerUpdate(q, location, o));
                }
            }
        }

        // reportEvent(player, csid, location) (Lost_Chick.lua:76-80): p0 picks the line: 0 the owner came for the
        // chick, 1 the trainer took it back
        private static QuestReply ReportEvent(IQuestContext q, int csid, int location)
        {
            int returned = ChickResult(q.Player.CharVar(LostChickVar)) == ResultReturned ? 1 : 0;

            return QuestReply.ProgressEvent(csid).Option(0, 1 - returned).Option(1, returned).Option(7, location);
        }

        // the report's finish: xi.chocoboRaising.applyEffects(player, walks.clearLostChickResult(...)): one
        // SET_CHAR_VAR
        private static void ClearResult(IQuestContext q)
        {
            q.Player.SetCharVar(LostChickVar, ClearLostChickResult(q.Player.CharVar(LostChickVar)));
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Step 1: Ask the San d'Oria owners about the lost chick. The right one teaches the diligent story.
            OwnerZone(b.AnyState(when: q => ChickAt(q, 1)), 1);

            // Step 1: Ask the Bastok owners about the lost chick.
            OwnerZone(b.AnyState(when: q => ChickAt(q, 2)), 2);

            // Step 1: Ask the Windurst owners about the lost chick.
            OwnerZone(b.AnyState(when: q => ChickAt(q, 3)), 3);

            // Step 2: The trainer reports how the search went and clears the result.
            b.AnyState(when: q =>
                    q.Settings.MainTruthy("ENABLE_CHOCOBO_RAISING") &&
                    ChickResult(q.Player.CharVar(LostChickVar)) != ResultNone)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hantileon).OnTrigger(q => ReportEvent(q, 852, 1))
                .OnEventFinish(Zone.SouthernSanDoria, 852, ClearResult)
                .Npc(Zone.BastokMines, Npc.BastokMines.Zopago).OnTrigger(q => ReportEvent(q, 542, 2))
                .OnEventFinish(Zone.BastokMines, 542, ClearResult)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Pulonono).OnTrigger(q => ReportEvent(q, 766, 3))
                .OnEventFinish(Zone.WindurstWoods, 766, ClearResult);
        }
    }
}
