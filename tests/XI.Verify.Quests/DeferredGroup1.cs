// XI.Verify.Quests -- quest rework B3b Deferred 1: Scattered into Shadow (jeuno 61) and Mimeo Stone (hidden quest
// mimeoStone), the two ports that waited on P10's deferred callbacks. The golden flows take every arm a char reaches
// (jeuno.scattered_into_shadow*, hiddenQuests.mimeo_stone*), the timers ticked to and past their deadlines, the
// listener with two chars and the wall clock moved. These checks pin what a flow cannot tell apart: the replies'
// kinds where the lookup's turns hide them, the timers' delays and callbacks on the fake's clock (f.Timers,
// FireTimers), the listener's arms one by one (its boundary GetSystemTime() == respawnTime, its self-removal, the Tebhi
// lookup by id), Mimeo Stone's self-arming chain (the draws in order, the stop rules, the rewards by fades) and the
// callbacks' labels 0/-/callback:<name>.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunDeferredGroup1()
    {
        Console.WriteLine("== quest rework B3b Deferred 1: Scattered into Shadow, Mimeo Stone ==");
        RunDeferredScattered();
        RunDeferredMimeo();
    }

    static string D1Say(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);

    // the handler of that kind (and entity / id) in the state at that index
    static QuestHandlerDecl D1Handler(QuestScriptDefinition d, int state, QuestHandlerKind kind, string entity = null, int id = -1) =>
        d.States[state].Handlers.Single(h => h.Kind == kind && (entity == null || h.Entity == entity) && (id < 0 || h.Id == id));

    const int D1Tebhi = 17396142, D1Normal = 0, D1CutsceneOnly = 6;

    static void RunDeferredScattered()
    {
        var def = new QC.Jeuno.ScatteredIntoShadow().Compile();
        Check("deferred d1 scattered:clean, steps 1..5, three callbacks", "|1,2,3,4,5|tebhiFade,tebhiGone,tebhiRespawn",
              $"{string.Join(" | ", QuestScriptValidator.Validate(def))}|{string.Join(",", def.DeclaredSteps)}|" +
              string.Join(",", def.Callbacks.Select(c => c.Name)));

        // the replies' kinds (the lookup's turns with Path of the Beastmaster's 20 and Brutus's script hide some)
        string Brutus(int state, int step, params int[] keyItems)
        {
            var f = new Fake { Step = step };
            foreach (var k in keyItems) f.KeyItems[k] = true;
            var h = D1Handler(def, state, QuestHandlerKind.OnTrigger, QC.Npc.UpperJeuno.Brutus);
            return D1Say(h.Fixed ?? h.Reply(f));
        }
        Check("deferred d1 scattered:Brutus's replies: 141 / 143 (ProgressEvent), 142 / 149 (Event), 144 / 135 " +
              "(ProgressEvent), nil at Prog 1 / 2, 151 (Event, importantOnce)",
              "ProgressEvent(141)|ProgressEvent(143)|Event(142)|Event(142)|Event(142)|ProgressEvent(144)|Event(149)|" +
              "ProgressEvent(135)|null|null|Event(151):importantOnce()",
              string.Join("|", Brutus(0, 0), Brutus(0, 1), Brutus(1, 1, QC.KeyItem.Aquaflora1),
                  Brutus(1, 3, QC.KeyItem.Aquaflora2), Brutus(1, 5, QC.KeyItem.Aquaflora3), Brutus(1, 3), Brutus(1, 4),
                  Brutus(1, 5), Brutus(1, 1), Brutus(1, 2), Brutus(2, 0)));
        string Osker(int step) => D1Say(D1Handler(def, 1, QuestHandlerKind.OnTrigger, QC.Npc.UpperJeuno.Osker).Reply(new Fake { Step = step }));
        Check("deferred d1 scattered:Osker: 145 (Event) at Prog 4 only", "null|Event(145)|null", $"{Osker(3)}|{Osker(4)}|{Osker(5)}");

        // the available check: Wings of Gold completed, AF2_QUEST_LEVEL as a number, a beastmaster
        bool Offered(int lvl, double setting, int job, bool wings = true)
        {
            var f = new Fake { MainLevel = lvl, MainJob = job, Status = QuestStatus.Available };
            f.MainNumbers["AF2_QUEST_LEVEL"] = setting;
            if (wings) f.CompletedQuests.Add($"{QuestLog.Jeuno}/WINGS_OF_GOLD");
            return def.States[0].Matches.All(m => m.When == null || m.When(f));
        }
        Check("deferred d1 scattered:offered at 50 >= 50; not at 49, not at 50 < 50.4, not a warrior, not without Wings of Gold" +
              ", not a white mage",
              "True|False|False|False|False|True|False",
              $"{Offered(50, 50, QC.Job.Bst)}|{Offered(49, 50, QC.Job.Bst)}|{Offered(50, 50.4, QC.Job.Bst)}|" +
              $"{Offered(50, 50, QC.Job.War)}|{Offered(50, 50, QC.Job.Bst, wings: false)}|{Offered(51, 50.6, QC.Job.Bst)}|" +
              $"{Offered(50, 50, QC.Job.Whm)}");

        // Tebhi's trade: NORMAL and exactly the collar: the calls in the Lua's order, the timers on Tebhi
        var trade = D1Handler(def, 1, QuestHandlerKind.OnTrade, QC.Npc.CastleOztroja.Tebhi);
        Fake Tebhi(int status, params int[] items)
        {
            var f = new Fake { Definition = def, Step = 4, Now = 1_800_000_000 };
            f.Npc = new QuestNpcFake(D1Tebhi, f.Calls) { Status = status };
            foreach (var i in items) f.TradeItems[i] = f.TradeItems.TryGetValue(i, out var n) ? n + 1 : 1;
            return f;
        }
        var t = Tebhi(D1Normal, QC.Item.BeastCollar);
        string reply = D1Say(trade.Reply(t));
        Check("deferred d1 scattered:Tebhi takes the collar: traded, Prog 5, stopped, turned to all, respawnTime + 180, " +
              "the timers 2000 / 4000, TEBHI_ACCEPTS (MessageSpecial)",
              "Trade.Complete();Quest.SetStep(5);Npc.ClearPath(True);Npc.FacePlayer(False);Npc.SetLocalVar(respawnTime,1800000180);" +
              $"Npc.Timer(2000,tebhiFade);Npc.Timer(4000,tebhiGone)|MessageSpecial(151:TEBHI_ACCEPTS,{QC.Item.BeastCollar})",
              $"{string.Join(";", t.Calls)}|{reply}");
        t.Calls.Clear();
        string Fire(long ms)
        {
            t.FireTimers(ms);
            string s = string.Join(";", t.Calls);
            t.Calls.Clear();
            return s;
        }
        Check("deferred d1 scattered:tebhiFade not at 2000, 'kesu' past it; tebhiGone not at 4000, CUTSCENE_ONLY and " +
              "continuePath past it", "|Npc.EntityAnimationPacket(kesu)||Npc.SetStatus(6);Npc.ContinuePath()|0",
              $"{Fire(2000)}|{Fire(2001)}|{Fire(4000)}|{Fire(4001)}|{t.Timers.Count}");
        Check("deferred d1 scattered:Tebhi refuses: not NORMAL, a bone chip, the collar and a bone chip, two collars",
              "null|null|null|null|0", $"{D1Say(trade.Reply(Tebhi(D1CutsceneOnly, QC.Item.BeastCollar)))}|" +
              $"{D1Say(trade.Reply(Tebhi(D1Normal, QC.Item.BoneChip)))}|" +
              $"{D1Say(trade.Reply(Tebhi(D1Normal, QC.Item.BeastCollar, QC.Item.BoneChip)))}|" +
              $"{D1Say(trade.Reply(Tebhi(D1Normal, QC.Item.BeastCollar, QC.Item.BeastCollar)))}|" +
              Tebhi(D1CutsceneOnly, QC.Item.BeastCollar).Timers.Count);

        // the zone-in: Prog 4 and Tebhi there: the listener; else nothing
        var zoneIn = D1Handler(def, 1, QuestHandlerKind.OnZoneIn);
        string ZoneIn(int step, bool tebhi)
        {
            var f = new Fake { Definition = def, Step = step, Zone = QC.Zone.CastleOztroja };
            f.NpcIds["TEBHI"] = D1Tebhi;
            if (tebhi) f.NpcsById[D1Tebhi] = new QuestNpcFake(D1Tebhi, f.Calls);
            string r = D1Say(zoneIn.Reply(f));
            return $"{r} {string.Join(";", f.Calls)} {string.Join(",", f.Listeners)}".Trim();
        }
        Check("deferred d1 scattered:the zone-in adds TEBHI_RESPAWN at Prog 4 with Tebhi there; not at 3 or 5, not without him",
              "null Player.AddListener(TICK,TEBHI_RESPAWN,tebhiRespawn) TICK/TEBHI_RESPAWN/tebhiRespawn|null|null|null",
              $"{ZoneIn(4, true)}|{ZoneIn(3, true)}|{ZoneIn(5, true)}|{ZoneIn(4, false)}");

        // the listener's arms: the zone or the step left: removed; Tebhi NORMAL: nothing; hidden: NORMAL and 'deru'
        // once GetSystemTime() >= respawnTime
        var respawn = def.Callbacks.Single(c => c.Name == "tebhiRespawn");
        string Listen(ushort zone, int step, int status, int now, int respawnTime)
        {
            var f = new Fake { Definition = def, Step = step, Zone = zone, Now = now };
            f.NpcIds["TEBHI"] = D1Tebhi;
            var tebhi = new QuestNpcFake(D1Tebhi, f.Calls) { Status = status };
            tebhi.LocalVars["respawnTime"] = respawnTime;
            f.NpcsById[D1Tebhi] = tebhi;
            f.AddListener("TICK", "TEBHI_RESPAWN", respawn);
            f.Calls.Clear();
            f.FireListeners();
            return $"{string.Join(";", f.Calls)}/{f.Listeners.Count}";
        }
        ushort co = QC.Zone.CastleOztroja;
        Check("deferred d1 scattered:tebhiRespawn: another zone or Prog 3 / 5: removed; NORMAL: nothing; hidden before " +
              "respawnTime: nothing; at it and past it: NORMAL, 'deru'",
              "Player.RemoveListener(TEBHI_RESPAWN)/0|Player.RemoveListener(TEBHI_RESPAWN)/0|Player.RemoveListener(TEBHI_RESPAWN)/0|" +
              "/1|/1|Npc.SetStatus(0);Npc.EntityAnimationPacket(deru)/1|Npc.SetStatus(0);Npc.EntityAnimationPacket(deru)/1",
              string.Join("|", Listen(QC.Zone.UpperJeuno, 4, D1CutsceneOnly, 100, 0), Listen(co, 3, D1CutsceneOnly, 100, 0),
                  Listen(co, 5, D1CutsceneOnly, 100, 0), Listen(co, 4, D1Normal, 100, 0), Listen(co, 4, D1CutsceneOnly, 179, 180),
                  Listen(co, 4, D1CutsceneOnly, 180, 180), Listen(co, 4, D1CutsceneOnly, 181, 180)));

        // the pools, the ghost, the chest and the finishes
        var pool = D1Handler(def, 1, QuestHandlerKind.OnTrigger, QC.Npc.Feiyin.UndergroundPool);
        string Pool(int offset, int step, bool popped, params int[] keyItems)
        {
            var f = new Fake { Step = step, PopResult = popped };
            f.NpcIds["UNDERGROUND_POOL_OFFSET"] = 17613247;
            f.Npc = new QuestNpcFake(17613247 + offset, f.Calls);
            foreach (var k in keyItems) f.KeyItems[k] = true;
            string r = D1Say(pool.Reply(f));
            var calls = f.Calls.Where(c => !c.StartsWith("Player.Has")).ToList();
            return calls.Count == 0 ? r : $"{r} {string.Join(";", calls)}";
        }
        string ghost = $"Player.PopFromQm({QC.Mob.Feiyin.DABOTZS_GHOST},True,0)";
        Check("deferred d1 scattered:the pools: 20 / 21 / 18 (ProgressEvent), the ghost popped claimed unhidden (NoAction), " +
              "a pop that fails, the sprigs missing, offset 3: nil",
              $"ProgressEvent(20)|null|ProgressEvent(21)|null|ProgressEvent(18)|NoAction() {ghost}|null {ghost}|null|null",
              string.Join("|", Pool(0, 1, true, QC.KeyItem.Aquaflora2), Pool(0, 1, true, QC.KeyItem.Aquaflora1),
                  Pool(2, 1, true, QC.KeyItem.Aquaflora1), Pool(2, 1, true, QC.KeyItem.Aquaflora2),
                  Pool(1, 2, true, QC.KeyItem.Aquaflora3), Pool(1, 1, true, QC.KeyItem.Aquaflora3),
                  Pool(1, 1, false, QC.KeyItem.Aquaflora3), Pool(1, 3, true), Pool(3, 1, true, QC.KeyItem.Aquaflora1,
                      QC.KeyItem.Aquaflora2, QC.KeyItem.Aquaflora3)));
        var chest = D1Handler(def, 1, QuestHandlerKind.OnTrade, QC.Npc.CastleOztroja.TreasureChest);
        string Chest(int step, int stage, bool collar)
        {
            var f = new Fake { Step = step };
            f.Vars["Stage"] = stage;
            if (collar) f.Items.Add(QC.Item.BeastCollar);
            string r = D1Say(chest.Reply(f));
            return f.Calls.Count == 0 ? r : $"{r} {string.Join(";", f.Calls)}";
        }
        Check("deferred d1 scattered:the chest: Prog 4, Stage 7, no collar: the treasure trade with the collar (NoAction); else nil",
              $"NoAction() Trade.TreasureTrade(1,{QC.Item.BeastCollar})|null|null|null|null",
              $"{Chest(4, 7, false)}|{Chest(4, 7, true)}|{Chest(4, 6, false)}|{Chest(3, 7, false)}|{Chest(5, 7, false)}");
        string Finish(int state, ushort zone, int csid, int option, Action<Fake> set = null)
        {
            var f = new Fake { Option = option };
            set?.Invoke(f);
            D1Handler(def, state, QuestHandlerKind.OnEventFinish, id: csid).Run(f);
            return string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.Has")));
        }
        string begun = $"Quest.Begin();Player.AddKeyItem({QC.KeyItem.Aquaflora1});Player.AddKeyItem({QC.KeyItem.Aquaflora2});" +
                       $"Player.AddKeyItem({QC.KeyItem.Aquaflora3});Text.Special(244:YOU_ARE_GIVEN_THREE_SPRIGS,{QC.KeyItem.Aquaflora1})";
        Check("deferred d1 scattered:the finishes: 141 / 143 on 1 (begun) and not, 18, 20, 21, 144, 135 completed or not, the ghost's death",
              $"{begun}|Quest.SetStep(1)|{begun}||Player.DelKeyItem({QC.KeyItem.Aquaflora3});Quest.SetStep(3);Quest.SetVarBit(Stage,2)|" +
              $"Player.DelKeyItem({QC.KeyItem.Aquaflora2});Quest.SetVarBit(Stage,1)|" +
              $"Player.DelKeyItem({QC.KeyItem.Aquaflora1});Quest.SetVarBit(Stage,0)|Quest.SetStep(4)|" +
              "Quest.Complete();Player.AddFame(0,7);Player.AddFame(1,7);Player.AddFame(2,7)|Quest.Complete()|Quest.SetStep(2)|",
              string.Join("|", Finish(0, QC.Zone.UpperJeuno, 141, 1), Finish(0, QC.Zone.UpperJeuno, 141, 0),
                  Finish(0, QC.Zone.UpperJeuno, 143, 1), Finish(0, QC.Zone.UpperJeuno, 143, 2),
                  Finish(1, QC.Zone.Feiyin, 18, 0), Finish(1, QC.Zone.Feiyin, 20, 0), Finish(1, QC.Zone.Feiyin, 21, 0),
                  Finish(1, QC.Zone.UpperJeuno, 144, 0), Finish(1, QC.Zone.UpperJeuno, 135, 0),
                  Finish(1, QC.Zone.UpperJeuno, 135, 0, f => f.CompleteResult = false),
                  Death(true), Death(false)));
        string Death(bool aquaflora3)
        {
            var f = new Fake();
            if (aquaflora3) f.KeyItems[QC.KeyItem.Aquaflora3] = true;
            D1Handler(def, 1, QuestHandlerKind.OnMobDeath).Run(f);
            return string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.Has")));
        }

        Check("deferred d1 scattered:the callbacks' labels, after the states' handlers",
              "0/-/callback:tebhiFade,0/-/callback:tebhiGone,0/-/callback:tebhiRespawn",
              string.Join(",", def.Callbacks.Select(c => $"0/-/callback:{c.Name}")));
    }

    static void RunDeferredMimeo()
    {
        var def = new QC.HiddenQuests.MimeoStone().Compile();
        Check("deferred d1 mimeo:clean, one callback", "|stoneTimer",
              $"{string.Join(" | ", QuestScriptValidator.Validate(def))}|{string.Join(",", def.Callbacks.Select(c => c.Name))}");
        Check("deferred d1 mimeo:the callback's label, after the states' handlers", "0/-/callback:stoneTimer",
              string.Join(",", def.Callbacks.Select(c => $"0/-/callback:{c.Name}")));
        const int Stone = QC.KeyItem.MimeoStone;
        Fake M(bool stone = false, int fades = 0, int progress = 0, params int[] rolls)
        {
            var f = new Fake { Hidden = true, Definition = def };
            if (stone) f.KeyItems[Stone] = true;
            f.QuestLocalVars["NumFades"] = fades;
            f.QuestLocalVars["Progress"] = progress;
            f.TextIds[QC.Text.AttohwaChasm.MIMEO_JEWEL_OFFSET.ToString()] = 7341;
            f.TextIds[QC.Text.AttohwaChasm.MIMEO_STONE_BRIGHTNESS_OFFSET.ToString()] = 7346;
            foreach (var r in rolls) f.Rolls.Enqueue(r);
            return f;
        }
        bool Holds(int state, Fake f) => def.States[state].Matches.All(m => m.When == null || m.When(f));
        Check("deferred d1 mimeo:the sections: no stone / the stone", "True|False|False|True",
              $"{Holds(0, M())}|{Holds(1, M())}|{Holds(0, M(stone: true))}|{Holds(1, M(stone: true))}");

        // the Luminant: the stone, PICKUP, the zone global, the locals, the timer RandomInt(50, 90) * 1000
        var lum = D1Handler(def, 0, QuestHandlerKind.OnTrigger, QC.Npc.AttohwaChasm.Luminant);
        var f = M(rolls: new[] { 50, 90, 77, 60, 51, 88 });
        f.Npc = new QuestNpcFake(16806290, f.Calls);
        string r = D1Say(lum.Reply(f));
        Check("deferred d1 mimeo:a Luminant: the stone (npcUtil.giveKeyItem), PICKUP, handleLuminantUsed, NumFades / Progress 0, " +
              "the timer 50 s (the first draw); nil",
              $"Player.GiveKeyItem({Stone});Text.Special(7:MIMEO_STONE_PICKUP,{Stone});Npc.AttohwaChasmHandleLuminantUsed();" +
              "Quest.SetLocalVar(NumFades,0);Quest.SetLocalVar(Progress,0);Player.RandomInt(50,90);Player.Timer(50000,stoneTimer)|null",
              $"{string.Join(";", f.Calls)}|{r}");

        // the chain on the fake's clock: each run fades one step and arms itself with the next draw, until the fifth
        f.Calls.Clear();
        string Fire(long ms)
        {
            f.FireTimers(ms);
            string s = string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.Has")));
            f.Calls.Clear();
            return s;
        }
        string Fade(int n, int ms) =>
            $"Quest.SetLocalVar(NumFades,{n});Text.Special(#{7341 + n - 1},{Stone});Player.RandomInt(50,90);Player.Timer({ms},stoneTimer)";
        Check("deferred d1 mimeo:stoneTimer's chain: not at 50000; fades 1-4 past each deadline with the draws 90, 77, 60, 51 " +
              "(re-armed each time); the fifth run: + 4, the stone taken, no timer",
              $"|{Fade(1, 90000)}|{Fade(2, 77000)}|{Fade(3, 60000)}|{Fade(4, 51000)}|Text.Special(#7345,{Stone});Player.DelKeyItem({Stone})|0",
              // each deadline is the firing tick + the next draw: 50001 + 90000, then + 1 past it, and so on
              string.Join("|", Fire(50000), Fire(50001), Fire(140002), Fire(217003), Fire(277004), Fire(328005), f.Timers.Count));
        string Stop(bool stone, int progress)
        {
            var g = M(stone: stone, progress: progress);
            g.RunCallback(def.Callbacks[0], null);
            return $"{string.Join(";", g.Calls.Where(c => !c.StartsWith("Player.Has")))}/{g.Timers.Count}";
        }
        Check("deferred d1 mimeo:stoneTimer stops with no stone or at Progress 1; runs at Progress 0 (and 2)",
              $"/0|/0|{Fade(1, 50000)}/1|{Fade(1, 50000)}/1",
              $"{Stop(false, 0)}|{Stop(true, 1)}|{Stop(true, 0)}|{Stop(true, 2)}");

        // the Cradle: BRIGHTNESS + fades, the reward by fades held: CANNOT_OBTAIN_ITEM (NoAction); else 'krtu' on the
        // next NPC (when there is one), Progress 1, 2 (ProgressEvent)
        var cradle = D1Handler(def, 1, QuestHandlerKind.OnTrigger, QC.Npc.AttohwaChasm.CradleofRebirth);
        int[] rewards = { QC.Item.RegainFeather, QC.Item.RebirthFeather, QC.Item.ReviveFeather, QC.Item.BlazeFeather, QC.Item.FireFeather };
        string Cradle(int fades, int progress, bool held, bool next = true)
        {
            var g = M(stone: true, fades: fades, progress: progress);
            g.Npc = new QuestNpcFake(16806358, g.Calls);
            if (next) g.NpcsById[16806359] = new QuestNpcFake(16806359, g.Calls);
            if (held) g.Items.Add(rewards[fades]);
            string s = D1Say(cradle.Reply(g));
            var calls = g.Calls.Where(c => !c.StartsWith("Player.Has")).ToList();
            return calls.Count == 0 ? s : $"{s} {string.Join(";", calls)}";
        }
        string Plays(int fades) =>
            $"ProgressEvent(2) Text.Special(#{7346 + fades},{Stone});Npc.EntityAnimationPacket(krtu);Quest.SetLocalVar(Progress,1)";
        Check("deferred d1 mimeo:the Cradle at fades 0-4: BRIGHTNESS + fades, 'krtu', Progress 1, 2; the reward held: " +
              "CANNOT_OBTAIN_ITEM; no next NPC: no animation; Progress 1: nil",
              string.Join("|", Enumerable.Range(0, 5).Select(Plays)) + "|" +
              string.Join("|", Enumerable.Range(0, 5).Select(n => $"NoAction() Text.Special(#{7346 + n},{Stone});Text.Special(7:CANNOT_OBTAIN_ITEM,{rewards[n]})")) +
              $"|ProgressEvent(2) Text.Special(#7346,{Stone});Quest.SetLocalVar(Progress,1)|null",
              string.Join("|", Enumerable.Range(0, 5).Select(n => Cradle(n, 0, false))) + "|" +
              string.Join("|", Enumerable.Range(0, 5).Select(n => Cradle(n, 0, true))) + "|" +
              $"{Cradle(0, 0, false, next: false)}|{Cradle(0, 1, false)}");
        string Fin(int fades)
        {
            var g = M(stone: true, fades: fades);
            D1Handler(def, 1, QuestHandlerKind.OnEventFinish, id: 2).Run(g);
            return string.Join(";", g.Calls);
        }
        Check("deferred d1 mimeo:2's finish: the stone taken, the reward by fades (npcUtil.giveItem)",
              string.Join("|", rewards.Select(i => $"Player.DelKeyItem({Stone});Player.GiveItem({i})")),
              string.Join("|", Enumerable.Range(0, 5).Select(Fin)));
        var g2 = M(stone: true);
        D1Handler(def, 1, QuestHandlerKind.OnZoneOut).Run(g2);
        var g3 = M(stone: true);
        g3.Npc = new QuestNpcFake(16806295, g3.Calls);
        string r3 = D1Say(D1Handler(def, 1, QuestHandlerKind.OnTrigger, QC.Npc.AttohwaChasm.Luminant).Reply(g3));
        Check("deferred d1 mimeo:the zone-out with the stone: + 4, the stone taken; a Luminant with it: NOTHING_OUT_OF_ORDINARY, nil",
              $"Text.Special(#7345,{Stone});Player.DelKeyItem({Stone})|Text.Special(7:NOTHING_OUT_OF_ORDINARY)|null",
              $"{string.Join(";", g2.Calls)}|{string.Join(";", g3.Calls)}|{r3}");
    }
}
