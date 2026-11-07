using System;
using System.Linq;
using XI.Persistence;
using XI.QuestContent;
using XI.Zone;

// lane engine-infra-1: engine rows checked on the live zone host. A trade (0x036) with a mob whose status is Normal
// reaches InteractionGlobal.onTrade with the mob, as GP_CLI_COMMAND_ITEM_TRANSFER's GetEntity(ActIndex, TYPE_NPC |
// TYPE_MOB) finds it, and one that is not Normal does not; a char_effects write whose INSERT fails rolls back with its
// DELETE (effects::SaveEffectRows' transaction) and logs its persist: line
internal static partial class Program
{
    private static void RunEngineInfra1()
    {
        Console.WriteLine("\n== engine-infra-1: a trade with a mob, a failed char_effects write ==");
        RunMobTrade();
        RunEffectsWriteRollback();
        RunTickOrder();
    }

    private const int FireCrystal = 4096;

    // GP_CLI_COMMAND_ITEM_TRANSFER aimed at an entity of the char's zone by its id and act index
    private static void TradeWith(uint id, ushort targId, byte invSlot, uint qty)
    {
        Collect();
        Send(0x036, 64, b =>
        {
            BitConverter.GetBytes(id).CopyTo(b, 4);
            BitConverter.GetBytes(qty).CopyTo(b, 8);
            b[48] = invSlot;
            BitConverter.GetBytes(targId).CopyTo(b, 58);
            b[60] = 1;
        });
    }

    // 1. a Marsh Funguar of Carpenters' Landing traded a Fire Crystal: as spawned (status Update) nothing reaches
    // InteractionGlobal.onTrade (wrapped to record its call); with the status of an NPC (Normal) it gets the mob, the
    // trade's item and the npcs/ script file of the mob's name, and the offer stays open on the mob. The same mob gone
    // (status Disappear), another id or out of range: nothing reaches onTrade
    private static void RunMobTrade()
    {
        uint id = CharIds[0];
        const string Funguar = "Marsh_Funguar";
        Cleanup(db, id);
        Fixture(id, Zone.CarpentersLanding, -414.6f, 0f, -363.5f, 0, (FireCrystal, 1));
        Login();
        var mob = Here.Mobs.First(m => m.Name == Funguar && m.IsSpawned && m.IsAlive);
        byte status = mob.Status;
        try
        {
            Lua(@"
                __ei1trades = {}
                __ei1onTrade = InteractionGlobal.onTrade
                InteractionGlobal.onTrade = function(player, npc, trade, fallback)
                    __ei1trades[#__ei1trades + 1] = string.format('%s/%s/%d/%d/%s',
                        npc:getName(), tostring(npc:isMob()),
                        npc:getID(), trade:getItemQty(" + FireCrystal + @"), tostring(fallback))
                    return __ei1onTrade(player, npc, trade, fallback)
                end");
            string Traded() => Lua("report(table.concat(__ei1trades, ',')) __ei1trades = {}");

            MoveTo(mob.X + 1.5f, mob.Y, mob.Z);
            TradeWith(mob.Id, mob.TargId, 1, 1);
            Check("mob trade: a spawned mob (status Update, as a mob's is): onTrade not called",
                  $"{NpcEntity.StatusUpdate}/",
                  $"{mob.Status}/{Traded()}");

            // the status of an NPC, as a script's setStatus(xi.status.NORMAL) gives a mob that acts as one
            mob.Status = NpcEntity.StatusNormal;
            TradeWith(mob.Id, mob.TargId, 1, 1);
            Check("mob trade: a Normal mob in range: onTrade gets the mob and the offered crystal " +
                  "(no npcs/ script of its name)",
                  $"{Funguar}/true/{mob.Id}/1/nil", Traded());
            Check("  the script file is the npcs/ one of the mob's name",
                  $"./scripts/zones/Carpenters_Landing/npcs/{Funguar}.lua", c.EventPreparation.ScriptFile);
            Check("  the offer is open on the mob", $"{mob.Id}/{Funguar}",
                  c.NpcTrade == null ? "none" : $"{c.NpcTrade.NpcId}/{c.NpcTrade.NpcName}");

            mob.Status = NpcEntity.StatusDisappear;
            TradeWith(mob.Id, mob.TargId, 1, 1);
            Check("mob trade: the mob not Normal (Disappear): onTrade not called", "", Traded());
            mob.Status = NpcEntity.StatusNormal;

            TradeWith(mob.Id + 1, mob.TargId, 1, 1);
            Check("mob trade: the act index's mob with another id (UniqueNo): onTrade not called", "", Traded());

            MoveTo(mob.X + 7.0f, mob.Y, mob.Z);
            TradeWith(mob.Id, mob.TargId, 1, 1);
            Check("mob trade: 7' away: onTrade not called", "", Traded());
        }
        finally
        {
            mob.Status = status;
            Lua("if __ei1onTrade then InteractionGlobal.onTrade = __ei1onTrade end " +
                "__ei1onTrade = nil __ei1trades = nil");
            Logout();
            Cleanup(db, id);
        }
    }

    // 2. the char's effects written by the persist sweep, then a trigger on the lane's char_effects refuses this char's
    // INSERTs: the next write rolls back, its DELETE with it (the rows written before stay), and the persist: line says
    // so. Without the statement checks the DELETE would commit and the char would lose its rows
    private static void RunEffectsWriteRollback()
    {
        uint id = CharIds[1];
        const string Trigger = "qsl_ei1_refuse_effects";
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        string Rows() => DbRow("COUNT(*)", "FROM char_effects WHERE charid = ?");
        string EffectIds() =>
            DbRow("COALESCE(GROUP_CONCAT(effectid ORDER BY effectid), '')", "FROM char_effects WHERE charid = ?");
        try
        {
            Lua("player:addStatusEffect(xi.effect.PROTECT, { power = 10, duration = 1800, origin = player })");
            Tick(5000);
            Tick(5000);
            Check("effects write: Protect written by the persist sweep", "40", EffectIds());

            Sql(db, $"DROP TRIGGER IF EXISTS {Trigger}");
            // one statement (Execute refuses a ';'): this char's charid set to NULL, which the NOT NULL column refuses
            Sql(db, $"CREATE TRIGGER {Trigger} BEFORE INSERT ON char_effects FOR EACH ROW " +
                    $"SET NEW.charid = IF(NEW.charid = {id}, NULL, NEW.charid)");
            int mark = log.Count;
            Lua("player:addStatusEffect(xi.effect.SHELL, { power = 10, duration = 1800, origin = player })");
            Tick(5000);
            Tick(5000);
            Check("effects write: an INSERT refused: the DELETE rolled back with it, Protect's row kept", "40",
                  EffectIds());
            Check("  the persist: failure line",
                  $"persist: char {id}: writing its char_effects rows failed (rolled back)",
                  string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("persist: "))));
            Check("  one row", "1", Rows());
        }
        finally
        {
            Sql(db, $"DROP TRIGGER IF EXISTS {Trigger}");
            Logout();
            Cleanup(db, id);
        }
    }
}
