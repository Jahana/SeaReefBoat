using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using XI.QuestContent;

// A golden flow (tests/golden/flows/<key>[#branch].flow): one statement per line, '#' starts a comment. The key (Key:
// the name up to its first '#') is its container's: a quest's <area>.<const> (bastok.the_doorman), or (quest rework
// C2) a mission's mission.<log>.<const> (mission.cop.the_rites_of_life#cop.flow) or a battlefield's
// battlefield.<const> (battlefield.cactuar_suave.flow); probe.<name> is a probe's (no container). Names resolve
// through the generated constants of XI.QuestContent by reflection (Zone, Item, KeyItem, FameArea, Slot, QuestKey,
// Mob, Npc), so a typo fails the parse with "<file>:<line>: unknown <kind> <name>".
//
// Header and setup lines (before login; not steps):
//   lists <Zone>/<second>/<third> ...     the lookup lists whose order each step records
//   char zone=<Zone> pos=<x>,<y>,<z> [job=<JOB>] [lvl=<n>] [nation=<n>] [race=<n>] [gil=<n>]
//   fame <FameArea> <n> | item <Item> [x<n>] [wsp=<n>] | keyitem <KeyItem> | charvar <name> <n>
//                                         wsp=: one weapon with n weapon skill points (its exdata's unlockPoints, what
//                                         getWeaponskillPoints reads: a trial weapon's 300); only on a weapon (an
//                                         item_weapon row: the run's parse checks it, WeaponResolver)
//   quest <QuestKey> <available|accepted|completed> | hide <require name>
//   module modules/<path>.lua             (quest rework P4, P11) the module that file is, loaded in this world as the
//                                         server loads its modules at its start: once the setup is written (the flow's
//                                         settings in effect), the server's Lua module loader runs the file on both
//                                         sides (ZoneHost.ModuleInitLines, ZoneHost.LoadLuaModules) and the C# module
//                                         that ports it, if any, is enabled by the same line; the lookup reloads
//                                         (InteractionGlobal.reload(true): the C# quests register again), then
//                                         xi.server.onServerStart() runs (the Lua module's override, super() and
//                                         modifyInteractionEntry included: Modules.cs). A header line, one per module
//                                         (in load order); a flow without one runs as before
//   mission <MissionLog> <Mission> <current|completed>    Mission is the constant of Mission.<Area> of that log (mission
//                                         Bastok TheZeruhnReport current). completed is completeMission's write: for
//                                         Cop it sets no flag (the C++ keeps none: hasCompletedMission compares the
//                                         current mission), so mission Cop <m> completed only clears the current
//                                         one (0); a completed CoP mission is a later one current
//   rank <n> [points=<n>]                 the rank (and rank points) of the nation of the char line, which must give nation=
//   title <Title> | setting <NAME> <n|decimal|true|false> xi.settings.main[NAME] (the name must exist: checked at the run);
//                                         a decimal (quest rework P5: GIL_RATE 1.5; digits, one point, a sign, no exponent) is
//                                         written into Lua invariantly. The C# side reads it as Lua has it through
//                                         Settings.MainNumber; Settings.Main rounds it (half away from zero: 1.5 reads 2)
//   setting map.<NAME> <n|decimal|true|false>
//                                         (quest rework P6) xi.settings.map[NAME], as setting writes main's; the run also
//                                         refuses a value of another kind than the setting's (a number into a boolean: the
//                                         Lua's FISHING_ENABLE == true is false for the number 1). Only the Lua table
//                                         changes: the engine's own copy (StatSettings, read once at the server's start) does
//                                         not, so fishing itself stays off. The C# side reads it through Settings.Map (as
//                                         Main) and Settings.MapFlag (the boolean true only, the Lua's == true)
//   storage <inventory|satchel> <n>       (quest rework P6) the container's size: char_storage's column (inventory,
//                                         satchel), what the loader reads; a fresh char has 30 and 0. Inventory 30 to 80,
//                                         satchel 0 to 80 (the engine holds a container to 0..80, and a char's inventory
//                                         starts at 30)
//   levelcap <n>                          the char's level cap (char_jobs.genkai, 50 for a fresh char), 1 to 99
//   merits <n>                            the char's merit points (char_exp.merits, what getMeritCount reads), 0 to 255;
//                                         the engine caps them at the bank (MAX_MERIT_POINTS, 30, plus Max Merit upgrades)
//   skill <Skill> <n>                     the skill's stored value (10x its level: 2400 is skill 240, as getCharSkillLevel
//                                         returns it); <Skill> is a Skill constant (xi.skill)
//   spell <id>                            (quest rework P8) a spell the char knows, by its xi.magic.spell id (no constant
//                                         class): a char_spells row; the run checks the id is a spell_list row
// Quest rework P8, item ids and support jobs: an <Item> of item / trade (and of a setup item line) is a generated Item
// name or a number, the item's id (item 2012, trade Kipopo 2012 x1 850 x1: item_basic names with no xi.item entry), 1 to
// 65534 and an item_basic row (ItemResolver, checked at the parse when there is a database). The char line takes
// sjob=<JOB> slvl=<n> (both or neither): char_stats' support job and its char_jobs level, another job than job=, slvl
// 1 to 99; the loader caps the level the char gets at half the main level (getSubLvl reads the capped one)
//   uncovered: <n>/<entity>/<trigger> -- <reason>
//                                         a waiver of the coverage gate (Program.CoverageFailures): the C# handler with that
//                                         label is reached by no trace of the quest, for the reason given (any flow of the
//                                         quest may hold it; the flow hash leaves it out)
// Steps (each ends with a recorded step): login [open], trigger <Npc> [at=<yalms>|here], trade <Npc> [gil=<n>] [<Item> x<n> ...]
// [gil=<n>] (gil first: trade slot 0, where trade:getGil() reads it, as the client offers it; gil last: after the
// items), finish [option=<n>], finish-if-event [option=<n>], update <csid> option=<n>, update <csid> option="<text>",
// zoneline <Zone>, route <Zone> ..., warp <x>,<y>,<z> <Zone> [option=<n>|open], rezone [option=<n>|open], walk <x>,<y>,<z>, equip <Item>
// <Slot>, seekeyitem <KeyItem>, delkeyitem <KeyItem>, localvar <name> <n>, npclocalvar <Npc> <name> <n>, weather <n>,
// animatenpc <Npc> <n>, fill-inventory, free-inventory, kill <Mob>[+<n>|-<n>], claim <Mob>[+<n>|-<n>], kill #<id>, claim #<id>, tick
// <ms>, wait vana=<hours>, wait jst=<days>, logout, and (quest rework P8) the GM steps effect <id>, npcstatus <Npc> <normal|disappear>,
// missionstatus <MissionLog> <value> [pos=<n>], currency <name> <n>, bfwin (each below). After the login, fame / item / keyitem /
// charvar / quest / mission / rank / title / setting / skill / levelcap / merits / storage / spell are steps too: a GM's Lua call on
// the char (player:setFame, addItem, addKeyItem, setCharVar, addQuest / completeQuest / delQuest, addMission /
// completeMission, setRank / setRankPoints, addTitle, setSkillLevel, setLevelCap, setMerits; setting: the assignment to
// xi.settings.main or .map; storage: player:changeContainerSize(container, n - getContainerSize(container)), the run
// failing when the size is not n after it: the engine keeps a size that would not hold the items in it). The option of warp / rezone is what the client answers to
// every event open after the zone-in (up to 4 in a row, each ended with it), 0 when not given; a login answers with 0.
// open (quest rework P5) answers none: login open, rezone open and warp ... open leave the zone-in's event open (a
// cutscene onZoneIn returned), so the steps after it drive it (update <csid> option=<n> reaches its onEventUpdate, then
// finish); a flow must drive an open zone-in event to its finish (one left unfinished passes silently, and a later trigger
// or trade step inside the open event fails the run, P8 B10); a zone-in with no event leaves nothing open. A login open's packets are its char's login bundle, as a login's
// (TraceStep.LoginOf). The at= of trigger is
// how far east of the NPC the char stands to talk (1.5 yalms when not given: a script's checkDistance(npc) <= 1 needs
// less). trigger <Npc> here (quest rework P6) talks from where the char stands, with no walk (after a walk <x>,<y>,<z>, a
// handler's player:getXPos() / getZPos() reads that spot: His Name is Valgeir's Felisa, The Sand Charm's Blandine); the
// run fails when the NPC is out of the engine's talk range (ZoneHost.InTalkRange, OnTalk's own test: 6 yalms), since the
// engine would answer with a release only, on both sides alike, and the step would prove nothing. A kill / claim mob is the IDs.lua mob key, plus n to its id for a mob of an offset key (TALEKEEPERS_GIFT_OFFSET+1),
// or minus n for a mob whose id lies below every key of its zone (GALGALIM-3); the run refuses an id that names no mob
// of the zone. update <csid> option="<text>" is the text the client typed into the event (GP_CLI_COMMAND_PASSWARDS,
// 0x060), which reaches onEventUpdate as a Lua string: 0 to 16 characters, printable, no space and no '"'; the packet
// carries no event id, so the run refuses it unless <csid> is the current event. A finish carries a number only (the
// engine has no string finish). delkeyitem <KeyItem> is a GM's player:delKeyItem (a step only: the arm of a key item
// gone). localvar <name> <n> is a GM's player:setLocalVar (a step only: a local var lives on the logged-in char, not in
// the database; the arm of a local var no script sets, Past Perfect's EviDialog). Its <n> is 0 or more: setLocalVar
// takes a uint32, so the parse refuses a negative value. npclocalvar <Npc> <name> <n> is a GM's
// GetNPCByID(id):setLocalVar(name, n) (quest rework P5; a step only: an NPC's local var lives in the zone, not in the
// database): the NPC named as trigger names it (<Npc>, <Npc>+<n>, <KEY>, <KEY>+<n>, in the char's zone; checked at the
// run), its <n> 0 or more as localvar's; the arm of an NPC local var only a zone or mob script sets (A Timely Visit's qm1
// Wait, which the NMs' despawn sets). weather <n> is a GM's player:setWeather(n) (!setweather; a
// step only): the zone's weather becomes the xi.weather id n, 0 (NONE) to 19 (DARKNESS), until the zone's own weather
// timer next rolls (3 to 30 minutes of the harness clock after the last roll; tick moves that clock, and so do warp
// (400 ms), walk (2 x 400 ms) and kill / claim (2 s, plus up to 30 s for the claim), so a flow that moves it that far
// may see the roll, which is seeded); the arm of a weather the zone's table never rolls (Wish Upon a Star's NONE in
// Bastok Markets). The weather is the zone's, not the char's: bob: weather <n> sets bob's zone's, which every char in
// that zone then has (main's too when it stands there), as localvar under a prefix is that char's own.
// animatenpc <Npc> <n> is a GM's !animatenpc (scripts/commands/animatenpc.lua:
// GetNPCByID(id):setAnimation(n); a step only): the NPC's animation becomes n (0 to 255, an xi.animation id:
// setAnimation takes a uint8), the arm of an animation no script gives that NPC (Community Service's lamp neither
// OPEN_DOOR nor CLOSE_DOOR). The NPC of trigger / trade / animatenpc is <Npc> (the first of that name), <Npc>+<n>
// (the NPC n ids after the first of that name in id order: NPCs that share a name, Brazier+2), <KEY> or <KEY>+<n> (an
// IDs.lua npc key of the zone, all capitals and not an NPC's name, plus n: SARCOPHAGUS_OFFSET+3; checked at the run);
// the run refuses an id that names no NPC of the zone, or one of another name than the first's.
//
// The P8 GM steps (each a step only, a GM's Lua call on the char or its zone):
//   effect <id>                           player:addStatusEffect(<id>, { power = 1, origin = player }) (godmode's form:
//                                         no duration, so it stays until removed); <id> an xi.effect id, 1 to 813
//                                         (MAX_EFFECTID - 1). The run fails when the engine refuses it (returns false)
//   kill #<id> / claim #<id>              the mob by its id (a zone whose IDs.lua names no mob: Abyssea - Uleguerand); the
//                                         run refuses an id that names no mob of the char's zone. '#' followed by a digit
//                                         right after kill / claim is the id, not a comment
//   npcstatus <Npc> <normal|disappear>    GetNPCByID(id):setStatus(xi.status.NORMAL | DISAPPEAR): an NPC that loads hidden
//                                         and only a global script shows (the Warhorse Hoofprints); the NPC as trigger
//                                         names it; the run checks the status after
//   missionstatus <MissionLog> <value> [pos=<n>]
//                                         player:setMissionStatus(log, value[, pos]): the whole status (0 or more), or with
//                                         pos= (0 to 7) the 4-bit field at that position (value 0 to 15), what
//                                         getMissionStatus(log, pos) reads (Over the Hills and Far Away: COP LOUVERANCE 2)
//   spell <id>                            (after the login) player:addSpell(<id>); the run fails unless hasSpell is true after
//   currency <name> <n>                   player:addCurrency(name, n): <name> lower case (bayld, cruor), n 1 or more; the
//                                         run fails unless getCurrency rose by n (an unknown name, or the cap)
//   bfwin                                 player:getBattlefield():setStatus(xi.battlefield.status.WON), then two 1 s ticks
//                                         (as kill's): the battlefield's win runs and its 32001 opens in the step. The run
//                                         fails when the char is in no battlefield
//
// The client's rest (quest rework B3b Adoulin group 4, Dances with Luopans' healing hook):
//   heal | heal stop                      GP_CLI_COMMAND_CAMP (0x0E8), mode On / Off, as the client's /heal sends it: the
//                                         engine's own path (ZoneHost.OnCamp: the HEALING effect added, its script's
//                                         onEffectGain; stop: the effect deleted, its onEffectLose). Refused while an event
//                                         is open (the engine drops a 0x0E8 sent in one, BlockedState.InEvent)
//
// Carry-harness-2 (the user's rulings of 2026-11-04):
//   <step> expect-error "<text>"          a suffix that ends a step (not a header or setup line): the step raises a
//                                         script error, expected there. The record (Lua side) needs a script error
//                                         holding <text> among the step's log lines; the C# side a C# handler's throw
//                                         (ZoneHost.HandlerThrew) or that same script error (Program.ExpectedErrors).
//                                         <text>: printable ASCII, spaces allowed, no '"'; a '#' in it is no comment
//   item <Item> x<n> (setup)              split into rows of the item's stack size; a second row of a Rare item fails
//                                         the run; the lines of tests/golden/item-stack-exempt.txt keep their old row
//                                         (ItemStacks.cs)
//   warning: quests: <key>: one finish of <csid> ran <n> handlers (<labels>) -- <reason>
//                                         a C# quest's double finish (ZoneHost.DoubleFinishLine) where the Lua runs it
//                                         twice too
//
// More chars (quest rework P3). The char line's char is main. Each further char has its own header line, after main's:
//   char <name> zone=<Zone> pos=<x>,<y>,<z> [job=<JOB>] [lvl=<n>] [nation=<n>] [race=<n>] [gil=<n>]
// (<name>: lower case, letters, digits and _, at most 16; not main, not a statement's verb; at most 6 chars in all). race=
// (xi.race, 1 to 8; 1 Hume when not given) goes on main's line too. A statement prefixed <name>: is that char's: before
// its login a setup line (its rows written before the run, wherever the line stands), after it a step run as it
// (bob: login, bob: trigger Vilatroire, bob: walk 0,0,0, bob: item CopperIngot). Without a prefix it is main's (main: is
// allowed too). Main logs in first; a char's steps follow its login and none follows its logout but a login (a login
// of a char logged in logs it in again where it stands, as main's always could: a zone-in after a GM teleport); tick,
// wait, setting and the header lines take no prefix (the world's). Party steps (the engine's own path):
//   invite <name>                         GP_CLI_COMMAND_GROUP_SOLICIT_REQ (0x06E), a party invite to that char (logged in):
//                                         its act index when it stands in the inviter's zone, else 0 (found by id). Each
//                                         invite is answered (accept / decline) before the next one
//   accept | decline                      GP_CLI_COMMAND_GROUP_SOLICIT_RES (0x074) by the char invited, to its invite;
//                                         accept then ticks once (400 ms), when the party's reload (PartyReload through
//                                         the world) sends the lists. Before that tick the harness sets the members'
//                                         accounts_parties.timestamp to the order they joined (Program.PinPartyOrder):
//                                         the engine lists a party (GROUP_TBL, GROUP_LIST) by that column, the database's
//                                         wall clock in seconds, and MariaDB orders a tie as it likes
// A char's id as a value: option=$<name> (finish, finish-if-event, update, warp, rezone) is that char's id; update <csid>
// option="$<name>" types that char's name. In a flow of more than one char the harness's world routes the party's IPC
// (Program.RoutedIpc) back to the zone host after each client packet, before each tick and when
// each step is recorded.
internal sealed class Statement
{
    public int Line;
    public string Verb;
    // the statement's words joined by single spaces: the step's label
    public string Label;
    public string Name;               // npc, mob key, charvar name, require name, quest key, setting name
    public string Var;                // npclocalvar <Npc> <name> <n>: the NPC's local var's name (Name is the NPC)
    public string Status;             // quest status, mission status, setting kind (number or bool)
    public int Value;                 // item, key item, fame area, slot, csid, ms, hours, days, charvar value, mission log, rank points (-1: none), title, skill
    public int Amount;                // fame / charvar value, item count, mission id, rank, setting number or 0 / 1 (Status bool), skill value, level cap, merits
    public double? Real;              // setting <NAME> <decimal>: the fractional value (Amount 0); null for a whole number or a boolean
    public string Table = "main";     // setting: the xi.settings table, main (setting <NAME>) or map (setting map.<NAME>, quest rework P6)
    public bool Here;                 // trigger <Npc> here: talk from where the char stands, no walk (quest rework P6)
    public uint? Option;
    public bool Open;                 // login / rezone / warp ... open: the zone-in's events left open (no 0x05B answers them)
    public string OptionText;         // update <csid> option="<text>": the typed text (null: a number option)
    public ushort ZoneId;
    public ushort[] Zones;
    public float X, Y, Z;
    public List<(int Item, uint Qty)> Items;
    public float? At;                 // trigger at=<yalms>: the char's distance east of the NPC (null: the default 1.5, or here)
    public int? WsPoints;             // item wsp=<n>: the weapon skill points of the item (null: none, its exdata untouched)
    public bool Held;                 // item ... held (carry-harness-3): the step's addItem must add nothing
    public int Offset;                // kill / claim <Mob>+<n> / <Mob>-<n>: n added to (taken from) the mob key's id; trigger / trade <Npc>+<n>, <KEY>+<n>
    public bool NpcKey;               // trigger / trade <KEY>[+<n>]: Name is an IDs.lua npc key of the zone, not an NPC's name
    public int Actor;                 // the char the statement is for: its index in Flow.Chars (0: main; <name>: prefix)
    public int? OptionChar;           // option=$<name>: the option is that char's id (its index in Flow.Chars), at the run
    public int? OptionTextChar;       // option="$<name>": the typed text is that char's name (its index in Flow.Chars)
    public int Target = -1;           // invite <name>: the invited char's index in Flow.Chars
    public uint? MobNumber;           // kill / claim #<id>: the mob's id (quest rework P8); null: Name is its IDs.lua key
    public bool NoKiller;             // kill <Mob> nokiller: the mob dies with no killer (quest rework P9)
    public int Pos = -1;              // missionstatus ... pos=<n>: the 4-bit field (quest rework P8); -1: the whole status
    public string ExpectError;        // <step> expect-error "<text>": its script error (carry-harness-2); null: none
}

// uncovered: <label> -- <reason>, at a line of a flow file
internal sealed record Waiver(string File, int Line, string Label, string Reason);
// warning: <log line> -- <reason> (quest rework P9): an engine warning the flow's run logs, not a script error
internal sealed record FlowWarning(string File, int Line, string Text, string Reason);

internal sealed class CharSpec
{
    public string Name = Flow.MainChar;
    public ushort Zone;
    public int Race = 1;              // race=<n> (xi.race: 1 Hume male ... 8 Galka); the fixture's char_look.race
    public float X, Y, Z;
    public int Job = 1;
    public int? Level;
    public int Nation = 1;
    public bool HasNation;
    public uint Gil = 1000;
    public int SJob;                  // sjob=<JOB> (quest rework P8): char_stats' support job; 0 none
    public int SLvl;                  // slvl=<n>: its char_jobs level (the loader caps the level the char gets)
}

internal sealed class Flow
{
    public string Name;               // the file name without .flow: <key>[#branch]
    // the container key (a quest's, a mission's mission.<log>.<const>, a battlefield's battlefield.<const>) or a
    // probe's
    public string Key => Name.Split('#')[0];
    public string File;
    public string Sha256;
    public bool MentionsTwin;         // the file's text (comments too) names a twin: a probe flow that expects Twins.cs to register one
    // "twin" at the start of a word, any case (twin, twins, Twins.cs), or "Twin" anywhere, case-sensitive (a twin class:
    // IllTakeTheBigBoxTwin): not lower-case inside a word, so a zone name that holds it (PortWindurst) does not count
    // (B3b Windurst group 11)
    private static readonly System.Text.RegularExpressions.Regex TwinWord =
        new(@"(?i:\btwin)|Twin", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    // the flow's chars in declaration order: the first char line's (main), then each named one's
    public readonly List<CharSpec> Chars = new();
    // the first char (main): every statement without a <name>: prefix is its
    public CharSpec Char => Chars.Count > 0 ? Chars[0] : null;
    public readonly List<(ushort Zone, string Second, string Third)> Lists = new();
    public readonly List<Statement> Setup = new();
    public readonly List<Statement> Steps = new();
    public readonly List<string> Hidden = new();
    // module modules/<path>.lua lines, in order (quest rework P4)
    public readonly List<string> Modules = new();
    public readonly List<Waiver> Waivers = new();
    public readonly List<FlowWarning> Warnings = new();

    // the engine's log lines that are warnings, not errors (C++ ShowWarning; the C# engine logs both as "lua: ..."): only
    // these may be named by a flow's warning: line. AddItem of an item id with no item_basic row (Trial by Ice's 709 at
    // option 0 / above 6: player:addItem(0)): the item is not given and the script goes on. completeMission of a
    // mission not current (nothing changes) and addMission over a current one (it is replaced): Western Adoulin's
    // Zone.lua csid 2 finish after the SoA mission's own (B3b Adoulin group 5, Raptor Rapture's #heartwings)
    internal static readonly System.Text.RegularExpressions.Regex[] EngineWarnings =
    {
        new(@"^lua: AddItem: Item <-?[0-9]+> is not found in a database$"),
        new(@"^lua: completeMission: [0-9]+ on log [0-9]+ is not the current mission$"),
        new(@"^lua: addMission: overwriting current mission [0-9]+ of log [0-9]+ with [0-9]+$"),
    };
    // a C# quest's double finish (carry-harness-2: ZoneHost.DoubleFinishLine, one onEventFinish dispatch running two of
    // its finish handlers), which a flow may declare by its warning: line where the Lua runs the finish twice as well;
    // the line's quest key, or null when the line is not one
    private static readonly System.Text.RegularExpressions.Regex DoubleFinish =
        new(@"^quests: (?<key>\S+): one finish of [0-9]+ ran ([2-9]|[1-9][0-9]+) handlers \(.+\)$");
    internal static string DoubleFinishKey(string line) =>
        DoubleFinish.Match(line) is { Success: true } m ? m.Groups["key"].Value : null;
    // <step> expect-error "<text>" (carry-harness-2): the word anywhere after a space, then the suffix's form at the
    // end: the text printable ASCII (spaces allowed), no '"', not empty
    internal const string ExpectErrorVerb = "expect-error";
    private static readonly System.Text.RegularExpressions.Regex ExpectErrorWord = new(@"(^|\s)expect-error(\s|$)");
    private static readonly System.Text.RegularExpressions.Regex ExpectErrorSuffix =
        new(@"(^|\s+)expect-error\s+""(?<text>[ -!#-~]*[!#-~][ -!#-~]*)""\s*$");
    // warning: <text> -- <reason>
    private static readonly System.Text.RegularExpressions.Regex WarningLine =
        new(@"^\s*warning:\s*(?<text>.*?)\s--\s+(?<reason>.*?)\s*$");

    // a handler label: <n>/<entity>/<trigger>[@<Zone>] (ZoneHost.LabelsOf); an entity name may hold spaces (Gloom Phantom),
    // not at its ends. States count from 1; the one state-0 label is a deferred callback's (quest rework P10:
    // ZoneHost.CallbackLabel), a separate alternative, so state 0 is valid only with the entity - and callback:<name>
    // (0/Npc/onTrigger stays refused)
    private static readonly System.Text.RegularExpressions.Regex HandlerLabel =
        new(@"^[1-9][0-9]*/([^/\s]|[^/\s][^/]*[^/\s])/(onTrigger|onTrade|onMobDeath|onZoneIn|afterZoneIn|onZoneOut|(onEventFinish|onEventUpdate|onTriggerAreaEnter|onTriggerAreaLeave):[0-9]+)(@(?<zone>\w+))?$" +
            @"|^0/-/callback:[A-Za-z][A-Za-z0-9_]*$");
    // uncovered: <label> -- <reason>: the label up to the first " --" that a space or the line's end follows
    private static readonly System.Text.RegularExpressions.Regex WaiverLine =
        new(@"^\s*uncovered:\s*(?<label>.*?)(\s--(\s+(?<reason>.*?))?)?\s*$");
    // what WaiverLine took for a label when its -- lacks a space on one side: the label, then the reason
    private static readonly System.Text.RegularExpressions.Regex MisspacedWaiver =
        new(@"^(?<label>.*?)\s*--\s*(?<reason>\S.*)$");

    // whether the entity name is an NPC of the zone: Npc.<Zone> first, then the resolver (the zone data's npcs.yaml)
    public delegate bool NpcResolver(ushort zone, string name);

    // whether the item is a weapon (item wsp=<n> takes only one); null: not checked (no database)
    public delegate bool WeaponResolver(int item);

    // whether an item id is an item_basic row (an item by id, quest rework P8); null: not checked (no database)
    public delegate bool ItemResolver(int item);

    // the last item id an item / trade takes by number: 65535 is the gil (GilItem)
    public const int MaxItemId = 65534;
    // effect <id>: 1 to MAX_EFFECTID - 1 (XI.Zone.EffectId.Max)
    public const int MaxEffect = XI.Zone.EffectId.Max - 1;
    // npcstatus: the xi.status names a GM sets an NPC to
    public static readonly string[] NpcStatuses = { "normal", "disappear" };

    // trigger without at=: the char stands 1.5 yalms east of the NPC
    public const float TriggerDistance = 1.5f;

    // storage <inventory|satchel> <n>: the container's xi.inv id and the sizes the engine takes for it (quest rework P6).
    // The engine holds any container to 0..80; a char's inventory starts at 30 (charCreate's START_INVENTORY floor), so
    // less is no size a char has
    public static readonly (string Name, int Container, int Min, int Max)[] StorageContainers =
    {
        ("inventory", 0, 30, 80),
        ("satchel", 5, 0, 80),
    };

    // the gil as an inventory item (slot 0)
    public const int GilItem = 65535;

    // the first char's name: what a <name>: prefix names it by, never a char line's name
    public const string MainChar = "main";
    // the chars a flow may have: a full party
    public const int MaxChars = 6;

    private static readonly string[] SetupVerbs = { "fame", "item", "keyitem", "charvar", "quest", "mission", "rank", "title", "setting", "skill", "levelcap", "merits", "storage", "spell" };
    private static readonly string[] HeaderVerbs = { "char", "hide", "lists", "uncovered:", "module", "warning:" };
    private static readonly string[] StepVerbs =
    {
        "login", "trigger", "trade", "finish", "finish-if-event", "update", "zoneline", "route", "warp", "rezone", "walk", "equip",
        "seekeyitem", "delkeyitem", "localvar", "npclocalvar", "weather", "animatenpc", "fill-inventory", "free-inventory", "kill", "claim", "tick", "wait", "logout",
        "invite", "accept", "decline", "effect", "npcstatus", "missionstatus", "currency", "bfwin", "heal",
    };
    // the statements of the world, not of a char: no <name>: prefix
    private static readonly string[] WorldVerbs = { "tick", "wait", "setting" };
    // a char's name: lower case, as a flow writes it (main and the statements' verbs excluded)
    private static readonly System.Text.RegularExpressions.Regex CharNameRe = new(@"^[a-z][a-z0-9_]{0,15}$");

    public static Flow Load(string path, NpcResolver npcs, WeaponResolver weapons = null, ItemResolver items = null)
    {
        var bytes = System.IO.File.ReadAllBytes(path);
        string text = Encoding.UTF8.GetString(bytes);
        var f = Parse(text, Path.GetFileName(path), Path.GetFileNameWithoutExtension(path), npcs, weapons, items);
        f.File = path;
        return f;
    }

    // the sha256 hex of the flow's statements: each line without its comment and trailing whitespace, the blank lines
    // and the waivers dropped, joined by LF (a comment, a blank line, a waiver or a checkout's CRLF changes nothing)
    public static string Hash(string text)
    {
        var kept = text.Replace("\r\n", "\n").Split('\n').Select(l => WithoutComment(l).TrimEnd())
            .Where(l => l.Length > 0 && !l.TrimStart().StartsWith("uncovered:", StringComparison.Ordinal) && !l.TrimStart().StartsWith("warning:", StringComparison.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", kept)))).ToLowerInvariant();
    }

    // '#' starts a comment, except inside a require name or a word (Zon-Fobun has none; HomePoint#1 does), and except the
    // '#' of kill #<id> / claim #<id> (quest rework P8: a '#' and a digit right after the verb, a <name>: prefix allowed)
    internal static string WithoutComment(string line)
    {
        var mobId = MobIdWord.Match(line);
        int keep = mobId.Success ? mobId.Groups["hash"].Index : -1;
        // the quoted text of expect-error "<text>" (carry-harness-2) is no comment: an error may name an argument #2
        var quoted = ExpectErrorQuote.Match(line);
        int textFrom = quoted.Success ? quoted.Groups["quote"].Index : line.Length;
        int textTo = quoted.Success ? line.IndexOf('"', textFrom + 1) : -1;
        if (textTo < 0) textTo = line.Length;
        for (int i = 0; i < line.Length; i++)
        {
            if (i > textFrom && i < textTo) continue;
            if (line[i] == '#' && i != keep && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line.Substring(0, i);
        }
        return line;
    }
    private static readonly System.Text.RegularExpressions.Regex ExpectErrorQuote =
        new(@"(^|\s)expect-error\s+(?<quote>"")");
    private static readonly System.Text.RegularExpressions.Regex MobIdWord = new(@"^\s*([a-z][a-z0-9_]*:\s+)?(kill|claim)\s+(?<hash>#)[0-9]");

    public static Flow Parse(string text, string file, string name, NpcResolver npcs, WeaponResolver weapons = null, ItemResolver items = null)
    {
        // an <Item>: a generated Item name, or an item id (quest rework P8)
        int ItemOf(string word, Func<string, Exception> err) => ResolveItem(word, items, err);
        var f = new Flow { Name = name, Sha256 = Hash(text), MentionsTwin = TwinWord.IsMatch(text) };
        // main's login opens the steps; each char's own state: logged in now, ever logged in, its zone
        bool loggedIn = false;
        var on = new List<bool>();
        var everOn = new List<bool>();
        var heres = new List<ushort>();
        // the char whose invite is not answered yet (-1: none)
        int pending = -1;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string raw = WithoutComment(lines[n]);
            string where = $"{file}:{n + 1}";
            Exception Err(string msg) => new FormatException($"{where}: {msg}");
            // <step> expect-error "<text>" (carry-harness-2): the step's script error, expected; the suffix comes off
            // the words, and stays in the step's label
            string expectError = null;
            // a waiver's or a warning's reason is free text: the word may stand in it
            string head = raw.TrimStart();
            bool freeText = head.StartsWith("uncovered:", StringComparison.Ordinal) ||
                            head.StartsWith("warning:", StringComparison.Ordinal);
            if (!freeText && ExpectErrorWord.IsMatch(raw))
            {
                var ee = ExpectErrorSuffix.Match(raw);
                if (!ee.Success)
                    throw Err("expect-error ends a step: <step> expect-error \"<text>\" (printable, no '\"')");
                expectError = ee.Groups["text"].Value;
                raw = raw.Substring(0, ee.Index);
            }
            var all = raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (all.Length == 0 && expectError != null) throw Err("expect-error ends a step: it has none before it");
            if (all.Length == 0) continue;
            // <name>: <statement>: the statement of that char (main's without one)
            int actor = 0;
            var w = all;
            if (all[0].Length > 1 && all[0].EndsWith(':') && all[0] != "uncovered:" && all[0] != "warning:")
            {
                string who = all[0].Substring(0, all[0].Length - 1);
                actor = f.Chars.FindIndex(cs => cs.Name == who);
                if (actor < 0) throw Err($"unknown char {who}");
                if (all.Length == 1) throw Err($"{all[0]} takes a statement");
                w = all.Skip(1).ToArray();
            }
            bool prefixed = w != all;
            var st = new Statement
            {
                Line = n + 1, Verb = w[0], Actor = actor, ExpectError = expectError,
                Label = string.Join(" ", all) + (expectError == null ? "" : $" {ExpectErrorVerb} \"{expectError}\""),
            };
            string verb = w[0];
            bool isStep = StepVerbs.Contains(verb), isSetup = SetupVerbs.Contains(verb), isHeader = HeaderVerbs.Contains(verb);
            if (!isStep && !isSetup && !isHeader) throw Err($"unknown statement {verb}");
            if (prefixed && (isHeader || WorldVerbs.Contains(verb))) throw Err($"{verb} is not a char's statement: no {all[0]} prefix");
            if (isHeader && expectError != null) throw Err($"expect-error ends a step, not a header line: {verb}");
            // main logs in first; a named char's steps follow its own login, and none follows its logout but a login
            if (isStep && !loggedIn && (verb != "login" || actor > 0)) throw Err("step before login");
            if (isHeader && loggedIn) throw Err($"{verb} after login");
            if (verb != "char" && verb != "lists" && verb != "hide" && verb != "uncovered:" && verb != "module" && verb != "warning:" && f.Char == null) throw Err("the char line comes first");
            if (isStep && !WorldVerbs.Contains(verb) && verb != "login" && !on[actor])
                throw Err(everOn[actor] ? $"{f.Chars[actor].Name} is logged out" : $"{f.Chars[actor].Name} has not logged in");
            // a setup statement before its char's login is a fixture row (main's before the steps, a named char's wherever it
            // stands: its rows are written before the run); after it, a step
            bool setupRow = isSetup && (actor == 0 ? !loggedIn : !everOn[actor]);
            if (isSetup && !setupRow && verb != "setting" && !on[actor]) throw Err($"{f.Chars[actor].Name} is logged out");
            ushort here = f.Chars.Count > 0 ? heres[actor] : (ushort)0;
            void Args(int min, int max)
            {
                if (w.Length - 1 < min || w.Length - 1 > max) throw Err($"{verb} takes {(min == max ? $"{min}" : $"{min} to {max}")} arguments");
            }
            // option=<n> or option=$<name> (that char's id, at the run)
            void Option(string word)
            {
                if (word.StartsWith("option=$", StringComparison.Ordinal))
                {
                    st.OptionChar = CharRef(f, word.Substring("option=$".Length), Err);
                    st.Option = 0;
                }
                else st.Option = (uint)KeyInt(word, "option", Err);
            }
            switch (verb)
            {
                case "lists":
                    Args(1, 999);
                    foreach (var l in w.Skip(1))
                    {
                        var p = l.Split('/');
                        if (p.Length != 3) throw Err($"a list is <Zone>/<second>/<third>: {l}");
                        f.Lists.Add((ResolveZone(p[0], Err), p[1], p[2]));
                    }
                    continue;
                case "uncovered:":
                {
                    // the label (its entity name may hold spaces), then "--" and the reason
                    var m = WaiverLine.Match(raw);
                    string label = m.Groups["label"].Value, reason = m.Groups["reason"].Value;
                    if (label.Length == 0) throw Err("uncovered: takes <n>/<entity>/<trigger> -- <reason>");
                    var lm = HandlerLabel.Match(label);
                    // "2/Michea/onTrade --reason" (or "onTrade-- reason"): a label, then -- with no space on one side
                    if (!lm.Success && MisspacedWaiver.Match(label) is { Success: true } mw &&
                        HandlerLabel.IsMatch(mw.Groups["label"].Value))
                        throw Err("uncovered: a space each side of -- before the reason: " +
                                  $"{mw.Groups["label"].Value} -- {mw.Groups["reason"].Value}");
                    if (!lm.Success) throw Err($"a handler label is <n>/<entity>/<trigger>: {label}");
                    if (lm.Groups["zone"].Success) ResolveZone(lm.Groups["zone"].Value, Err);
                    if (reason.Length == 0) throw Err("uncovered needs a reason");
                    f.Waivers.Add(new Waiver(file, n + 1, label, reason));
                    continue;
                }
                case "warning:":
                {
                    // an engine warning the run logs (quest rework P9): the whole log line, then "--" and the reason
                    var m = WarningLine.Match(raw);
                    string warned = m.Success ? m.Groups["text"].Value : "", reason = m.Success ? m.Groups["reason"].Value : "";
                    if (warned.Length == 0 || reason.Length == 0) throw Err("warning: takes <log line> -- <reason>");
                    if (warned.StartsWith("quests: ", StringComparison.Ordinal))
                    {
                        // a quests: line may be named only when it is a double finish (the kept-bug list's one line,
                        // Sharpening the Sword's check 4, went with its fix: user ruling 2026-11-05, kept bugs)
                        if (DoubleFinishKey(warned) == null)
                            throw Err("not a double finish quests: line (ZoneHost.DoubleFinishLine; " +
                                      $"an error stays an error): {warned}");
                    }
                    else if (!EngineWarnings.Any(re => re.IsMatch(warned)))
                        throw Err($"not an engine warning (an error stays an error): {warned}");
                    if (f.Warnings.Any(x => x.Text == warned)) throw Err($"a second warning: line for {warned}");
                    f.Warnings.Add(new FlowWarning(file, n + 1, warned, reason));
                    continue;
                }
                case "module":
                {
                    // modules/<path>.lua from the csharp folder, / separators, no . or .. segments (a C# module's LuaFile)
                    Args(1, 1);
                    string path = w[1];
                    if (!path.StartsWith("modules/", StringComparison.Ordinal) || !path.EndsWith(".lua", StringComparison.Ordinal) || path.Contains('\\') ||
                        path.Split('/').Any(p => p is "" or "." or ".."))
                        throw Err($"a module is modules/<path>.lua (a Lua module file, / separators): {path}");
                    if (path == "modules/module_utils.lua") throw Err("modules/module_utils.lua is the helpers, not a module");
                    if (f.Modules.Contains(path)) throw Err($"a second module line for {path}");
                    f.Modules.Add(path);
                    continue;
                }
                case "hide":
                    Args(1, 1);
                    if (!w[1].StartsWith("scripts/", StringComparison.Ordinal) || w[1].EndsWith(".lua", StringComparison.Ordinal))
                        throw Err($"a require name is scripts/<path> without .lua: {w[1]}");
                    f.Hidden.Add(w[1]);
                    continue;
                case "char":
                {
                    // char key=value ... (main), then char <name> key=value ... (each other char)
                    var cs = new CharSpec();
                    int from = 1;
                    if (w.Length > 1 && !w[1].Contains('='))
                    {
                        if (f.Char == null) throw Err($"the first char line names no char: it is {MainChar}");
                        if (!CharNameRe.IsMatch(w[1]) || w[1] == MainChar || StepVerbs.Contains(w[1]) || SetupVerbs.Contains(w[1]) || HeaderVerbs.Contains(w[1]))
                            throw Err($"a char's name is a lower-case word (letters, digits, _; at most 16), not {MainChar} or a statement: {w[1]}");
                        if (f.Chars.Any(o => o.Name == w[1])) throw Err($"a second char line for {w[1]}");
                        if (f.Chars.Count == MaxChars) throw Err($"a flow has at most {MaxChars} chars (a party)");
                        cs.Name = w[1];
                        from = 2;
                    }
                    else if (f.Char != null) throw Err("a second char line: each char after the first has a name (char <name> zone=...)");
                    bool hasZone = false, hasPos = false;
                    foreach (var kv in w.Skip(from))
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 0) throw Err($"char takes key=value: {kv}");
                        string k = kv.Substring(0, eq), v = kv.Substring(eq + 1);
                        switch (k)
                        {
                            case "zone": cs.Zone = ResolveZone(v, Err); hasZone = true; break;
                            case "pos": (cs.X, cs.Y, cs.Z) = Pos(v, Err); hasPos = true; break;
                            case "job":
                                cs.Job = Array.IndexOf(Program.Jobs, v);
                                if (cs.Job <= 0) throw Err($"unknown job {v}");
                                break;
                            case "lvl": cs.Level = Int(v, Err); break;
                            case "nation": cs.Nation = Int(v, Err); cs.HasNation = true; break;
                            case "race":
                                cs.Race = Int(v, Err);
                                if (cs.Race is < 1 or > 8) throw Err($"a race is 1 to 8 (xi.race): {v}");
                                break;
                            case "gil": cs.Gil = (uint)Int(v, Err); break;
                            case "sjob":
                                cs.SJob = Array.IndexOf(Program.Jobs, v);
                                if (cs.SJob <= 0) throw Err($"unknown job {v}");
                                break;
                            case "slvl":
                                cs.SLvl = Int(v, Err);
                                if (cs.SLvl is < 1 or > 99) throw Err($"a support job's level is 1 to 99: {v}");
                                break;
                            default: throw Err($"char has no {k}");
                        }
                    }
                    if (!hasZone || !hasPos) throw Err("char needs zone= and pos=");
                    if ((cs.SJob > 0) != (cs.SLvl > 0)) throw Err("a support job takes sjob=<JOB> and slvl=<n>, both");
                    if (cs.SJob > 0 && cs.SJob == cs.Job) throw Err($"the support job is another job than the main one: sjob={Program.Jobs[cs.SJob]}");
                    f.Chars.Add(cs);
                    on.Add(false);
                    everOn.Add(false);
                    heres.Add(cs.Zone);
                    continue;
                }
                case "fame":
                    Args(2, 2);
                    st.Value = ResolveInt(typeof(FameArea), "fame area", w[1], Err);
                    if (st.Value >= XI.Zone.CharLoader.FameColumns.Length || XI.Zone.CharLoader.FameColumns[st.Value] == null)
                        throw Err($"fame area {w[1]} is derived, not stored");
                    st.Amount = Int(w[2], Err);
                    break;
                case "item":
                {
                    Args(1, 4);
                    st.Value = ItemOf(w[1], Err);
                    st.Amount = 1;
                    // item <Item> [x<n>] [wsp=<n>] held (carry-harness-3): a step whose addItem must add nothing (a
                    // Rare item the char already holds); the word comes off the end
                    if (w[^1] == "held")
                    {
                        if (setupRow) throw Err("held is a step's (after login): a setup line writes a row");
                        st.Held = true;
                        w = w[..^1];
                    }
                    if (w.Skip(2).Contains("held"))
                        throw Err("held is an item step's last word: item <Item> [x<n>] [wsp=<n>] held");
                    int next = 2;
                    if (w.Length > next && !w[next].StartsWith("wsp=", StringComparison.Ordinal)) st.Amount = Count(w[next++], Err);
                    if (w.Length > next)
                    {
                        // wsp=<n>: one weapon's weapon skill points
                        st.WsPoints = KeyInt(w[next++], "wsp", Err);
                        if (st.WsPoints is < 0 or > 65535) throw Err($"wsp=<n> takes 0 to 65535: {w[next - 1]}");
                        if (st.Amount != 1) throw Err("wsp= gives one weapon: no count, or x1");
                        if (weapons != null && !weapons(st.Value)) throw Err($"wsp= needs a weapon: {w[1]} is not one");
                    }
                    if (w.Length > next) throw Err($"item takes <Item> [x<n>] [wsp=<n>] [held]: {w[next]}");
                    break;
                }
                case "keyitem":
                case "seekeyitem":
                case "delkeyitem":
                    Args(1, 1);
                    st.Value = ResolveInt(typeof(KeyItem), "key item", w[1], Err);
                    break;
                case "charvar":
                case "localvar":
                    Args(2, 2);
                    st.Name = w[1];
                    st.Amount = verb == "localvar" ? LocalVarInt(w[2], Err) : Int(w[2], Err);
                    // setLocalVar takes a uint32 (lua_base_entity.cpp): a negative value would wrap
                    if (verb == "localvar" && st.Amount < 0) throw Err($"a local var is 0 or more (setLocalVar takes a uint32): {w[2]}");
                    break;
                case "npclocalvar":
                    // npclocalvar <Npc> <name> <n>: the NPC as trigger names it, then the var and its value
                    Args(3, 3);
                    NpcRef(st, here, w[1], npcs, Err);
                    st.Var = w[2];
                    st.Amount = LocalVarInt(w[3], Err);
                    // setLocalVar takes a uint32 (lua_base_entity.cpp), the NPC's as the char's
                    if (st.Amount < 0) throw Err($"a local var is 0 or more (setLocalVar takes a uint32): {w[3]}");
                    break;
                case "weather":
                    Args(1, 1);
                    st.Amount = Int(w[1], Err);
                    // xi::Weather: NONE (0) to DARKNESS (19); setWeather ignores anything else
                    if (st.Amount < 0 || st.Amount > 19) throw Err($"a weather is an xi.weather id, 0 to 19: {w[1]}");
                    break;
                case "quest":
                    Args(2, 2);
                    st.Name = ResolveQuestKey(w[1], Err);
                    if (w[2] is not ("available" or "accepted" or "completed")) throw Err($"a quest status is available, accepted or completed: {w[2]}");
                    st.Status = w[2];
                    break;
                case "mission":
                {
                    Args(3, 3);
                    st.Value = ResolveInt(typeof(XI.QuestContent.MissionLog), "mission log", w[1], Err);
                    var area = typeof(Mission).GetNestedType(w[1]);
                    st.Amount = area != null && Const(area, w[2]) is { } mf ? Convert.ToInt32(mf.GetRawConstantValue()) : throw Err($"unknown mission {w[2]}");
                    if (w[3] is not ("current" or "completed")) throw Err($"a mission status is current or completed: {w[3]}");
                    st.Status = w[3];
                    break;
                }
                case "rank":
                    Args(1, 2);
                    if (!f.Chars[actor].HasNation) throw Err("rank needs nation= on the char line");
                    if (f.Chars[actor].Nation is < 0 or > 2) throw Err($"rank needs a nation of 0 to 2: {f.Chars[actor].Nation}");
                    st.Amount = Int(w[1], Err);
                    if (st.Amount is < 0 or > 255) throw Err($"rank {st.Amount} is out of range");
                    st.Value = w.Length > 2 ? KeyInt(w[2], "points", Err) : -1;
                    if (w.Length > 2 && st.Value is < 0 or > 65535) throw Err($"rank points {st.Value} are out of range");
                    break;
                case "title":
                    Args(1, 1);
                    st.Value = ResolveInt(typeof(Title), "title", w[1], Err);
                    break;
                case "skill":
                    Args(2, 2);
                    st.Value = ResolveInt(typeof(Skill), "skill", w[1], Err);
                    st.Amount = Int(w[2], Err);
                    if (st.Amount is < 0 or > 65535) throw Err($"a skill value is 0 to 65535 (10x the level): {w[2]}");
                    break;
                case "levelcap":
                    Args(1, 1);
                    st.Amount = Int(w[1], Err);
                    if (st.Amount is < 1 or > 99) throw Err($"a level cap is 1 to 99: {w[1]}");
                    break;
                case "merits":
                    Args(1, 1);
                    st.Amount = Int(w[1], Err);
                    if (st.Amount is < 0 or > 255) throw Err($"merits are 0 to 255: {w[1]}");
                    break;
                case "storage":
                {
                    // storage <inventory|satchel> <n>: the container's size
                    Args(2, 2);
                    int k = Array.FindIndex(StorageContainers, sc => sc.Name == w[1]);
                    if (k < 0) throw Err($"storage takes inventory or satchel: {w[1]}");
                    var sc = StorageContainers[k];
                    st.Name = sc.Name;
                    st.Value = sc.Container;
                    st.Amount = Int(w[2], Err);
                    if (st.Amount < sc.Min || st.Amount > sc.Max) throw Err($"the {sc.Name}'s size is {sc.Min} to {sc.Max}: {w[2]}");
                    break;
                }
                case "setting":
                {
                    Args(2, 2);
                    // <NAME> is xi.settings.main's; map.<NAME> is xi.settings.map's (quest rework P6)
                    string settingName = w[1];
                    if (settingName.StartsWith("map.", StringComparison.Ordinal))
                    {
                        st.Table = "map";
                        settingName = settingName.Substring("map.".Length);
                    }
                    if (!System.Text.RegularExpressions.Regex.IsMatch(settingName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                        throw Err($"a setting name is an identifier, or map.<identifier>: {w[1]}");
                    st.Name = settingName;
                    if (w[2] is "true" or "false") { st.Status = "bool"; st.Amount = w[2] == "true" ? 1 : 0; }
                    else
                    {
                        st.Status = "number";
                        // a whole number as before; else a decimal (GIL_RATE 1.5): digits with one point, a sign allowed, no
                        // exponent, read invariantly
                        if (int.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) st.Amount = whole;
                        else if (double.TryParse(w[2], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var real) &&
                                 double.IsFinite(real))
                            st.Real = real;
                        else throw Err($"not a number: {w[2]}");
                    }
                    break;
                }
                case "login":
                case "fill-inventory":
                case "free-inventory":
                case "logout":
                    Args(0, verb == "login" ? 1 : 0);
                    if (verb == "login")
                    {
                        // login open: the zone-in's events left open
                        if (w.Length > 1 && w[1] != "open") throw Err($"login takes nothing or open: {w[1]}");
                        st.Open = w.Length > 1;
                        if (actor == 0) loggedIn = true;
                        on[actor] = everOn[actor] = true;
                    }
                    if (verb == "logout") on[actor] = false;
                    break;
                case "invite":
                    // invite <name>: GP_CLI_COMMAND_GROUP_SOLICIT_REQ (a party invite) for that char, logged in
                    Args(1, 1);
                    st.Target = CharRef(f, w[1], Err);
                    if (st.Target == actor) throw Err($"a char does not invite itself: {w[1]}");
                    if (!on[st.Target]) throw Err($"{w[1]} has not logged in");
                    // each invite answered before the next: the members join in the flow's order, one invite pending at most
                    if (pending >= 0) throw Err($"{f.Chars[pending].Name}'s invite is not answered yet (accept or decline first)");
                    pending = st.Target;
                    break;
                case "accept":
                case "decline":
                    // the answer to the invite pending (GP_CLI_COMMAND_GROUP_SOLICIT_RES), by the char invited
                    Args(0, 0);
                    if (pending != actor) throw Err($"{f.Chars[actor].Name} has no invite to answer");
                    pending = -1;
                    break;
                case "trigger":
                    Args(1, 2);
                    NpcRef(st, here, w[1], npcs, Err);
                    // here: from where the char stands (quest rework P6)
                    if (w.Length == 3 && w[2] == "here") st.Here = true;
                    else if (w.Length == 3)
                    {
                        if (!w[2].StartsWith("at=", StringComparison.Ordinal) ||
                            !float.TryParse(w[2].AsSpan(3), NumberStyles.Float, CultureInfo.InvariantCulture, out var at) || at < 0 || float.IsNaN(at) || float.IsInfinity(at))
                            throw Err($"trigger takes <Npc> [at=<yalms>|here]: {w[2]}");
                        st.At = at;
                    }
                    break;
                case "animatenpc":
                    // a GM's !animatenpc <npc id> <animation>: the NPC as trigger names it, the animation a uint8
                    Args(2, 2);
                    NpcRef(st, here, w[1], npcs, Err);
                    st.Amount = Int(w[2], Err);
                    if (st.Amount < 0 || st.Amount > 255) throw Err($"an animation is 0 to 255 (setAnimation takes a uint8): {w[2]}");
                    break;
                case "trade":
                {
                    Args(2, 999);
                    NpcRef(st, here, w[1], npcs, Err);
                    st.Items = new List<(int, uint)>();
                    int start = 2, end = w.Length;
                    // gil=<n> first: the gil (item 65535, inventory slot 0) offered in trade slot 0, where trade:getGil()
                    // reads it (the client's order); gil=<n> last: offered after the items
                    bool gilFirst = w.Length > 3 && w[2].StartsWith("gil=", StringComparison.Ordinal);
                    if (gilFirst) start++;
                    else if (w[^1].StartsWith("gil=", StringComparison.Ordinal)) end--;
                    if ((end - start) % 2 != 0 || end == start && end == w.Length) throw Err("trade takes <Npc>, [gil=<n>], <Item> x<n> pairs, then [gil=<n>]");
                    void Gil(string word)
                    {
                        int gil = KeyInt(word, "gil", Err);
                        if (gil <= 0) throw Err($"gil=<n> takes a positive amount: {word}");
                        st.Items.Add((GilItem, (uint)gil));
                    }
                    if (gilFirst) Gil(w[2]);
                    for (int i = start; i < end; i += 2)
                        st.Items.Add((ItemOf(w[i], Err), (uint)Count(w[i + 1], Err)));
                    if (end < w.Length) Gil(w[^1]);
                    break;
                }
                case "finish":
                case "finish-if-event":
                    Args(0, 1);
                    if (w.Length > 1 && w[1].StartsWith("option=\"", StringComparison.Ordinal))
                        throw Err($"{verb} takes a number option: a text option reaches only onEventUpdate (update <csid> option=\"<text>\"): {w[1]}");
                    if (w.Length > 1) Option(w[1]);
                    break;
                case "update":
                    Args(2, 2);
                    st.Value = Int(w[1], Err);
                    if (w[2].StartsWith("option=\"", StringComparison.Ordinal))
                    {
                        st.OptionText = OptionText(w[2], Err);
                        // option="$<name>": that char's name, typed
                        if (st.OptionText.StartsWith('$')) st.OptionTextChar = CharRef(f, st.OptionText.Substring(1), Err);
                    }
                    else Option(w[2]);
                    break;
                case "zoneline":
                    Args(1, 1);
                    st.ZoneId = heres[actor] = ResolveZone(w[1], Err);
                    break;
                case "route":
                    Args(1, 999);
                    st.Zones = w.Skip(1).Select(z => ResolveZone(z, Err)).ToArray();
                    heres[actor] = st.Zones[^1];
                    break;
                case "warp":
                    Args(2, 3);
                    (st.X, st.Y, st.Z) = Pos(w[1], Err);
                    st.ZoneId = heres[actor] = ResolveZone(w[2], Err);
                    // option=<n> answers each zone-in event; open leaves them open
                    if (w.Length > 3 && w[3] == "open") st.Open = true;
                    else if (w.Length > 3) Option(w[3]);
                    break;
                case "rezone":
                    Args(0, 1);
                    if (w.Length > 1 && w[1] == "open") st.Open = true;
                    else if (w.Length > 1) Option(w[1]);
                    break;
                case "walk":
                    Args(1, 1);
                    (st.X, st.Y, st.Z) = Pos(w[1], Err);
                    break;
                case "equip":
                    Args(2, 2);
                    st.Value = ResolveInt(typeof(Item), "item", w[1], Err);
                    st.Amount = ResolveInt(typeof(Slot), "slot", w[2], Err);
                    break;
                case "kill":
                case "claim":
                {
                    // kill <Mob> nokiller (quest rework P9): the mob dies with no killer (OnMobDeath(mob, nil))
                    if (verb == "kill" && w.Length == 3)
                    {
                        if (w[2] != "nokiller") throw Err($"kill takes <Mob> [nokiller]: {w[2]}");
                        st.NoKiller = true;
                    }
                    else Args(1, 1);
                    // #<id>: the mob by its id (quest rework P8; checked at the run)
                    if (w[1].StartsWith('#'))
                    {
                        if (!uint.TryParse(w[1].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var mobNumber) || mobNumber == 0)
                            throw Err($"{verb} #<id> takes a mob's id, a number from 1: {w[1]}");
                        st.MobNumber = mobNumber;
                        st.Name = w[1];
                        break;
                    }
                    // <Mob>+<n> / <Mob>-<n>: the key's id plus or minus n (a key is an identifier: no sign in it)
                    string mob = w[1];
                    int sign = mob.IndexOfAny(new[] { '+', '-' });
                    if (sign >= 0)
                    {
                        if (!int.TryParse(mob.AsSpan(sign + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var off) || off <= 0)
                            throw Err($"{verb} takes <Mob>, <Mob>+<n> or <Mob>-<n>, n from 1: {mob}");
                        st.Offset = mob[sign] == '-' ? -off : off;
                        mob = mob.Substring(0, sign);
                    }
                    st.Name = ResolveMob(here, mob, Err);
                    break;
                }
                case "tick":
                    Args(1, 1);
                    st.Value = Int(w[1], Err);
                    break;
                case "effect":
                    // effect <id>: a GM's addStatusEffect (quest rework P8)
                    Args(1, 1);
                    st.Value = Int(w[1], Err);
                    if (st.Value is < 1 or > MaxEffect) throw Err($"an effect is an xi.effect id, 1 to {MaxEffect}: {w[1]}");
                    break;
                case "npcstatus":
                    // npcstatus <Npc> <normal|disappear>: a GM's npc:setStatus (quest rework P8)
                    Args(2, 2);
                    NpcRef(st, here, w[1], npcs, Err);
                    if (!NpcStatuses.Contains(w[2])) throw Err($"npcstatus takes <Npc> normal or disappear: {w[2]}");
                    st.Status = w[2];
                    break;
                case "missionstatus":
                {
                    // missionstatus <MissionLog> <value> [pos=<n>]: a GM's setMissionStatus (quest rework P8)
                    Args(2, 3);
                    st.Value = ResolveInt(typeof(XI.QuestContent.MissionLog), "mission log", w[1], Err);
                    st.Amount = Int(w[2], Err);
                    if (w.Length > 3)
                    {
                        st.Pos = KeyInt(w[3], "pos", Err);
                        if (st.Pos is < 0 or > 7) throw Err($"a mission status position is 0 to 7: {w[3]}");
                        if (st.Amount is < 0 or > 15) throw Err($"a mission status at a position is 0 to 15 (4 bits): {w[2]}");
                    }
                    else if (st.Amount < 0) throw Err($"a mission status is 0 or more: {w[2]}");
                    break;
                }
                case "spell":
                    // spell <id>: a spell the char knows (quest rework P8): an xi.magic.spell id (no constant class)
                    Args(1, 1);
                    st.Value = Int(w[1], Err);
                    if (st.Value is < 1 or > 1023) throw Err($"a spell is an xi.magic.spell id, 1 to 1023: {w[1]}");
                    break;
                case "currency":
                    // currency <name> <n>: a GM's addCurrency (quest rework P8)
                    Args(2, 2);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(w[1], @"^[a-z][a-z0-9_]*$")) throw Err($"a currency is its lower-case name (bayld, cruor): {w[1]}");
                    st.Name = w[1];
                    st.Amount = Int(w[2], Err);
                    if (st.Amount < 1) throw Err($"currency adds 1 or more: {w[2]}");
                    break;
                case "bfwin":
                    // bfwin: the char's battlefield won by a GM (quest rework P8)
                    Args(0, 0);
                    break;
                case "heal":
                    // heal | heal stop: the client's /heal, on or off (GP_CLI_COMMAND_CAMP's mode: On 1, Off 2)
                    Args(0, 1);
                    if (w.Length > 1 && w[1] != "stop") throw Err($"heal takes nothing or stop: {w[1]}");
                    st.Amount = w.Length > 1 ? (int)XI.Map.CampMode.Off : (int)XI.Map.CampMode.On;
                    break;
                case "wait":
                    Args(1, 1);
                    if (w[1].StartsWith("vana=", StringComparison.Ordinal)) st.Name = "vana";
                    else if (w[1].StartsWith("jst=", StringComparison.Ordinal)) st.Name = "jst";
                    else throw Err($"wait takes vana=<hours> or jst=<days>: {w[1]}");
                    st.Value = KeyInt(w[1], st.Name, Err);
                    break;
            }
            if (setupRow || !isStep && !loggedIn)
            {
                if (st.ExpectError != null)
                    throw Err($"expect-error ends a step, not a setup line: {verb} before the login is a row");
                f.Setup.Add(st);
            }
            else f.Steps.Add(st);
        }
        if (f.Char == null) throw new FormatException($"{file}: no char line");
        if (!loggedIn) throw new FormatException($"{file}: no login");
        return f;
    }

    // a char of the flow by name (main, or a char line's): its index in Chars
    private static int CharRef(Flow f, string name, Func<string, Exception> err)
    {
        int i = f.Chars.FindIndex(cs => cs.Name == name);
        return i >= 0 ? i : throw err($"unknown char {name}");
    }

    private static int Int(string s, Func<string, Exception> err) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : throw err($"not a number: {s}");

    // a local var's value: an int32 here (the type is not widened), a number past it is out of range, not "not a number"
    private static int LocalVarInt(string s, Func<string, Exception> err) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) && (l > int.MaxValue || l < int.MinValue)
            ? throw err($"a local var value is out of range (int32 here, 2147483647 at most): {s}")
            : Int(s, err);

    // x<n>
    private static int Count(string s, Func<string, Exception> err) =>
        s.StartsWith('x') && int.TryParse(s.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : throw err($"a count is x<n>: {s}");

    // option="<text>": what GP_CLI_COMMAND_PASSWARDS carries, 16 bytes at most; printable ASCII, no '"' (a word: no space)
    public const int OptionTextMax = 16;
    private static string OptionText(string s, Func<string, Exception> err)
    {
        const string head = "option=\"";
        if (s.Length < head.Length + 1 || s[^1] != '"') throw err($"a text option is option=\"<text>\": {s}");
        string text = s.Substring(head.Length, s.Length - head.Length - 1);
        if (text.Length > OptionTextMax) throw err($"a text option is at most {OptionTextMax} characters: {s}");
        if (text.Any(ch => ch is < '!' or > '~' or '"')) throw err($"a text option is printable ASCII without '\"': {s}");
        return text;
    }

    // key=<n>
    private static int KeyInt(string s, string key, Func<string, Exception> err) =>
        s.StartsWith(key + "=", StringComparison.Ordinal) ? Int(s.Substring(key.Length + 1), err) : throw err($"expected {key}=<n>: {s}");

    private static (float, float, float) Pos(string s, Func<string, Exception> err)
    {
        var p = s.Split(',');
        float F(string v) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : throw err($"not a number: {v}");
        if (p.Length != 3) throw err($"a position is <x>,<y>,<z>: {s}");
        return (F(p[0]), F(p[1]), F(p[2]));
    }

    private static FieldInfo Const(Type t, string name) =>
        t.GetField(name, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } f ? f : null;

    public static ushort ResolveZone(string name, Func<string, Exception> err) =>
        Const(typeof(Zone), name) is { } f ? Convert.ToUInt16(f.GetRawConstantValue()) : throw err($"unknown zone {name}");

    private static int ResolveInt(Type t, string kind, string name, Func<string, Exception> err) =>
        Const(t, name) is { } f ? Convert.ToInt32(f.GetRawConstantValue()) : throw err($"unknown {kind} {name}");

    // an <Item>: an Item constant, or (quest rework P8) a number, the item's id: 1 to MaxItemId, an item_basic row when the
    // resolver is given
    private static int ResolveItem(string word, ItemResolver items, Func<string, Exception> err)
    {
        if (word.Length == 0 || !char.IsDigit(word[0])) return ResolveInt(typeof(Item), "item", word, err);
        if (!int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1 || id > MaxItemId)
            throw err($"an item id is 1 to {MaxItemId} (65535 is the gil: gil=<n>): {word}");
        if (items != null && !items(id)) throw err($"no item {id} (no item_basic row)");
        return id;
    }

    // QuestKey.<Area>.<Name> written <Area>.<Name>: the quest key string
    private static string ResolveQuestKey(string name, Func<string, Exception> err)
    {
        var p = name.Split('.');
        if (p.Length == 2 && typeof(QuestKey).GetNestedType(p[0]) is { } area && Const(area, p[1]) is { } f) return (string)f.GetRawConstantValue();
        throw err($"unknown quest {name}");
    }

    // the zone's class of a generated per-zone table (Npc.BastokMarkets), by the Zone constant's name
    private static Type ZoneClass(Type outer, ushort zone)
    {
        var zf = typeof(Zone).GetFields(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(f => f.IsLiteral && Convert.ToUInt16(f.GetRawConstantValue()) == zone);
        return zf == null ? null : outer.GetNestedType(zf.Name);
    }

    // the NPC's entity name: an Npc.<Zone> value (the scripted NPCs), else one the resolver knows (the zone data's npcs.yaml)
    private static string ResolveNpc(ushort zone, string name, NpcResolver npcs, Func<string, Exception> err) =>
        IsNpc(zone, name, npcs) ? name : throw err($"unknown npc {name}");

    private static bool IsNpc(ushort zone, string name, NpcResolver npcs)
    {
        var cls = ZoneClass(typeof(Npc), zone);
        if (cls != null && cls.GetFields().Any(f => f.IsLiteral && (string)f.GetRawConstantValue() == name)) return true;
        return npcs != null && npcs(zone, name);
    }

    // an IDs.lua key: all capitals, digits and _ (SARCOPHAGUS_OFFSET)
    private static readonly System.Text.RegularExpressions.Regex IdsKey = new(@"^[A-Z][A-Z0-9_]*$");

    // trigger / trade's NPC: <Npc>, <Npc>+<n>, <KEY>, <KEY>+<n> (n from 1). A word that is an NPC's name is that NPC (a
    // name may hold a '+'); else the part before its last '+' is the name or key, the rest the offset
    private static void NpcRef(Statement st, ushort zone, string word, NpcResolver npcs, Func<string, Exception> err)
    {
        string name = word;
        if (!IsNpc(zone, word, npcs))
        {
            int plus = word.LastIndexOf('+');
            if (plus > 0)
            {
                if (!int.TryParse(word.AsSpan(plus + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var off) || off <= 0)
                    throw err($"{st.Verb} takes <Npc>, <Npc>+<n>, <KEY> or <KEY>+<n>, n from 1: {word}");
                st.Offset = off;
                name = word.Substring(0, plus);
            }
        }
        if (!IsNpc(zone, name, npcs) && IdsKey.IsMatch(name)) st.NpcKey = true;
        st.Name = st.NpcKey ? name : ResolveNpc(zone, name, npcs, err);
    }

    // the Mob.<Zone> key (the zone's IDs.lua mob table)
    private static string ResolveMob(ushort zone, string name, Func<string, Exception> err)
    {
        var cls = ZoneClass(typeof(Mob), zone);
        return cls != null && Const(cls, name) != null ? name : throw err($"unknown mob {name}");
    }
}
