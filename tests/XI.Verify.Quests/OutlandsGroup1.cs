// XI.Verify.Quests -- quest rework B3b Outlands group 1, Forge Your Destiny: the two SpawnMobWithNpcCooldown calls.
// GetSystemTime() reads the harness's wall clock, a constant (1800000000) that only `wait jst=<days>` moves, by whole
// days, so a trace shows the qm cooldown only as "ahead of now" after the despawn; these checks pin the seconds the
// Lua's DESPAWN listeners add (Konschtat qm3: GetSystemTime() + 120; Zi'Tah qm2: + 60 * 10), the mob and the NPC var.
// They also take the SpawnMob nil arm (the Lua's `if not mob then return xi.action.noAction`), which no flow can reach:
// the zone's mob id always spawns once the "not spawned" test before it has passed.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup1()
    {
        Console.WriteLine("== quest rework B3b Outlands group 1: Forge Your Destiny's qm spawns (cooldown seconds, SpawnMob nil) ==");
        var def = new QC.Outlands.ForgeYourDestiny().Compile();
        const int qm3Id = 17220016, qm2Id = 17273404; // any ids: the call must pass the handler NPC's own

        // Konschtat's qm3: the oriental steel alone, within 1.6, the Forger down, the cooldown over
        string Qm3(bool spawns)
        {
            var f = new Fake { Status = QuestStatus.Accepted, TradeResult = true, Now = 1800000000, SpawnResult = spawns };
            f.Npc = new QuestNpcFake(qm3Id, f.Calls) { PlayerDistance = 1.0 };
            f.TradeItems[QC.Item.LumpOfOrientalSteel] = 1;
            f.RecordReply(OL1Trade(def, QC.Zone.KonschtatHighlands, QC.Npc.KonschtatHighlands.qm3).Reply(f));
            return string.Join(";", f.Calls);
        }

        // Zi'Tah's qm2: the Hatchet alone with the Sacred Sprig held, the Guardian Treant down, the cooldown over
        string Qm2(bool spawns)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Now = 1800000000, SpawnResult = spawns };
            f.Npc = new QuestNpcFake(qm2Id, f.Calls);
            f.Items.Add(QC.Item.SacredSprig);
            f.TradeItems[QC.Item.Hatchet] = 1;
            f.RecordReply(OL1Trade(def, QC.Zone.TheSanctuaryOfZitah, QC.Npc.TheSanctuaryOfZitah.qm2).Reply(f));
            return string.Join(";", f.Calls);
        }

        string qm3 = Qm3(true), qm3Nil = Qm3(false), qm2 = Qm2(true), qm2Nil = Qm2(false);
        Console.WriteLine("  qm3: " + qm3);
        Console.WriteLine("  qm3, SpawnMob nil: " + qm3Nil);
        Console.WriteLine("  qm2: " + qm2);
        Console.WriteLine("  qm2, SpawnMob nil: " + qm2Nil);

        Check("outlands g1:FYD qm3 spawns the Forger with the qm's forgerNextPopAllowedTime cooldown of exactly 120 s, then takes the steel",
              $"Player.SpawnMobWithNpcCooldown(FORGER,{qm3Id},forgerNextPopAllowedTime,120);Trade.Complete();Reply.MessageSpecial({QC.Zone.KonschtatHighlands}:PLACE_BLACKENED_SPOT,{QC.Item.LumpOfOrientalSteel})",
              qm3);
        Check("outlands g1:FYD qm2 spawns the Guardian Treant with the qm's treantNextPopAllowedTime cooldown of exactly 600 s, then takes the hatchet",
              $"Player.SpawnMobWithNpcCooldown(GUARDIAN_TREANT,{qm2Id},treantNextPopAllowedTime,600);Trade.Complete();Reply.MessageSpecial({QC.Zone.TheSanctuaryOfZitah}:SENSE_STRONG_EVIL_PRESENCE)",
              qm2);
        Check("outlands g1:FYD qm3, SpawnMob nil (Lua 74-77): NoAction, the steel not taken",
              $"Player.SpawnMobWithNpcCooldown(FORGER,{qm3Id},forgerNextPopAllowedTime,120);Reply.NoAction()",
              OL1NilArm(qm3Nil));
        Check("outlands g1:FYD qm2, SpawnMob nil (Lua 259-262): NoAction, the hatchet not taken",
              $"Player.SpawnMobWithNpcCooldown(GUARDIAN_TREANT,{qm2Id},treantNextPopAllowedTime,600);Reply.NoAction()",
              OL1NilArm(qm2Nil));
    }

    static QuestHandlerDecl OL1Trade(QuestScriptDefinition def, ushort zone, string npc) =>
        def.States.SelectMany(s => s.Handlers).Single(h => h.Kind == QuestHandlerKind.OnTrade && h.Zone == zone && h.Entity == npc);

    // the calls, or a marker when the nil arm took the trade
    static string OL1NilArm(string calls) => calls.Contains("Trade.Complete") ? "Trade.Complete called: " + calls : calls;
}
