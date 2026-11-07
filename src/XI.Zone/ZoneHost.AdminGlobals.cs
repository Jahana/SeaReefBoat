using System;
using System.Collections.Generic;
using XI.Common;
using XI.Script;

namespace XI.Zone
{
    // luautils globals the GM commands reach: SetTimeOffset (!addtime), ReloadSynthRecipes (!reloadrecipes),
    // ReloadExperienceData, the synergy recipe lookups xi.synergy calls (GetSynergyRecipeByID / ByTrade), Terminate
    // and ForceCrash (!crash)
    public sealed partial class ZoneHost
    {
        // luautils::Terminate's end, std::exit(1): XI.Host sets it to end the process. Null (a test host): Terminate
        // saves the chars, logs that nothing ends the process and returns
        public Action<int> ExitProcess;

        // the ForceCrash binding's crash(): XI.Host sets it to write the crash dump (main.GENERATE_CORE_DUMP) and end
        // the process with TerminateProcess (0xC0000005, CrashDump.Force). Null (a test host): a Lua error instead
        public Action ForceCrash;

        private void RegisterAdminGlobals()
        {
            // luautils::SetTimeOffset(offset): earth_time::reset_offset, then add_offset(seconds). The map process's
            // Earth clock, and the Vana'diel clock counted from it, move by the offset (EarthTime)
            lua_.SetGlobal("SetTimeOffset", l =>
            {
                EarthTime.ResetOffset();
                EarthTime.AddOffset((long)(int)l.ToIntegral(1) * 1000);
                return 0;
            });

            // synthutils::LoadSynthRecipes: "Loading synth recipes", then synth_recipes read again into an emptied
            // table
            lua_.SetGlobal("ReloadSynthRecipes", l =>
            {
                synthRecipes_ = null;
                SynthRecipes();
                return 0;
            });

            // charutils::LoadExpTable: the base experience table from the scripts, exp_base's experience to the next
            // level, then the /check difficulty curve
            lua_.SetGlobal("ReloadExperienceData", l =>
            {
                loader_?.Stats?.Growth?.LoadExpPerLevel(db_);
                LoadExpTables();
                return 0;
            });

            // luautils::GetSynergyRecipeByID(id): the recipe's table, nil without one
            lua_.SetGlobal("GetSynergyRecipeByID", l =>
            {
                uint id = l.ToUIntArg(1);
                return PushSynergyRecipe(l, SynergyRecipes().Find(r => r.Id == id));
            });

            // luautils::GetSynergyRecipeByTrade(trade): the item ids of the trade's first 8 slots (an empty slot left
            // out), sorted and padded with 0 to 8, matched against ingredient1..8
            lua_.SetGlobal("GetSynergyRecipeByTrade", l =>
            {
                var trade = (l.ToObject(1) as LuaTrade)?.Owner?.Trade;
                var ids = new List<ushort>();
                for (int i = 0; i < 8 && trade != null; i++)
                {
                    ushort itemId = trade.GetItemId(i);
                    if (itemId != 0) ids.Add(itemId);
                }
                ids.Sort();
                while (ids.Count < 8) ids.Add(0);
                return PushSynergyRecipe(l, SynergyRecipes().Find(r => SameIngredients(r.Ingredients, ids)));
            });

            // luautils::Terminate: every char of this process written (persist::flush on logout, SaveCharStats,
            // SaveCharExp), then std::exit(1)
            lua_.SetGlobal("Terminate", l =>
            {
                Terminate();
                return 0;
            });

            // the ForceCrash binding: crash(), a write through a null pointer, which the crash handler reports
            lua_.SetGlobal("ForceCrash", l =>
            {
                if (ForceCrash == null) throw new LuaException("ForceCrash: this host has no crash handler");
                ForceCrash();
                return 0;
            });
        }

        // MapEngine::do_init's synthutils::LoadSynthRecipes and synergyutils::LoadSynergyRecipes: XI.Host loads both
        // at its init; a host that does not (a test) loads each on its first use
        public void LoadRecipes()
        {
            SynthRecipes();
            SynergyRecipes();
        }

        // luautils::Terminate before its std::exit(1)
        internal void Terminate()
        {
            foreach (var c in new List<CharData>(charSessions_.Keys))
            {
                // persist::flush: the pending position too, for a char without an effects table as well
                if (c.StatusEffects.Table == null) PersistChar(c, true);
                else SaveStatusEffects(c, true);
                SaveCharStats(c);
                SaveCharExp(c, c.MJob);
            }
            if (ExitProcess == null)
            {
                log_("Terminate: the chars are saved; this host has no exit hook, the process goes on");
                return;
            }
            ExitProcess(1);
        }

        // the loaded synth recipes (loaded now if not yet), for the tests
        internal int SynthRecipeCount => SynthRecipes().Count;

        // synergyutils::SynergyRecipe
        private sealed class SynergyRecipe
        {
            public uint Id;
            public byte PrimarySkill, PrimaryRank, SecondarySkill, SecondaryRank, TertiarySkill, TertiaryRank;
            public readonly byte[] Fewell = new byte[8]; // fire, ice, wind, earth, lightning, water, light, dark
            public readonly ushort[] Ingredients = new ushort[8];
            public ushort Result, ResultHq1, ResultHq2, ResultHq3;
            public byte ResultQty, ResultHq1Qty, ResultHq2Qty, ResultHq3Qty;
            public string ResultName;
        }

        private static readonly string[] FewellNames =
            { "fire", "ice", "wind", "earth", "lightning", "water", "light", "dark" };

        private List<SynergyRecipe> synergyRecipes_;

        // synergyutils::LoadSynergyRecipes (LoadRecipes, else the first lookup): synergy_recipes in the server's order
        private List<SynergyRecipe> SynergyRecipes()
        {
            if (synergyRecipes_ != null) return synergyRecipes_;
            synergyRecipes_ = new List<SynergyRecipe>();
            var r = Exec("SELECT id, primary_skill, primary_rank, secondary_skill, secondary_rank, " +
                         "tertiary_skill, tertiary_rank, cost_fire_fewell, cost_ice_fewell, cost_wind_fewell, " +
                         "cost_earth_fewell, cost_lightning_fewell, cost_water_fewell, cost_light_fewell, " +
                         "cost_dark_fewell, ingredient1, ingredient2, ingredient3, ingredient4, ingredient5, " +
                         "ingredient6, ingredient7, ingredient8, result, resultHQ1, resultHQ2, resultHQ3, resultQty, " +
                         "resultHQ1Qty, resultHQ2Qty, resultHQ3Qty, resultName FROM synergy_recipes");
            while (r != null && r.Next())
            {
                var recipe = new SynergyRecipe
                {
                    Id = r.Get<uint>("id"),
                    PrimarySkill = r.Get<byte>("primary_skill"), PrimaryRank = r.Get<byte>("primary_rank"),
                    SecondarySkill = r.Get<byte>("secondary_skill"), SecondaryRank = r.Get<byte>("secondary_rank"),
                    TertiarySkill = r.Get<byte>("tertiary_skill"), TertiaryRank = r.Get<byte>("tertiary_rank"),
                    Result = r.Get<ushort>("result"), ResultHq1 = r.Get<ushort>("resultHQ1"),
                    ResultHq2 = r.Get<ushort>("resultHQ2"), ResultHq3 = r.Get<ushort>("resultHQ3"),
                    ResultQty = r.Get<byte>("resultQty"), ResultHq1Qty = r.Get<byte>("resultHQ1Qty"),
                    ResultHq2Qty = r.Get<byte>("resultHQ2Qty"), ResultHq3Qty = r.Get<byte>("resultHQ3Qty"),
                    ResultName = r.IsNull("resultName") ? "" : r.Get<string>("resultName"),
                };
                for (int i = 0; i < 8; i++) recipe.Fewell[i] = r.Get<byte>($"cost_{FewellNames[i]}_fewell");
                for (int i = 0; i < 8; i++) recipe.Ingredients[i] = r.Get<ushort>($"ingredient{i + 1}");
                synergyRecipes_.Add(recipe);
            }
            return synergyRecipes_;
        }

        private static bool SameIngredients(ushort[] recipe, List<ushort> ids)
        {
            for (int i = 0; i < 8; i++)
                if (recipe[i] != ids[i]) return false;
            return true;
        }

        // the table GetSynergyRecipeByID / ByTrade return (nil for no recipe)
        private static int PushSynergyRecipe(LuaState l, SynergyRecipe recipe)
        {
            if (recipe == null)
            {
                l.PushNil();
                return 1;
            }
            l.NewTable();
            void Field(string key, double value)
            {
                l.Push(value);
                l.SetField(-2, key);
            }
            Field("id", recipe.Id);
            Field("primary_skill", recipe.PrimarySkill);
            Field("primary_rank", recipe.PrimaryRank);
            Field("secondary_skill", recipe.SecondarySkill);
            Field("secondary_rank", recipe.SecondaryRank);
            Field("tertiary_skill", recipe.TertiarySkill);
            Field("tertiary_rank", recipe.TertiaryRank);
            for (int i = 0; i < 8; i++) Field($"cost_{FewellNames[i]}_fewell", recipe.Fewell[i]);
            for (int i = 0; i < 8; i++) Field($"ingredient{i + 1}", recipe.Ingredients[i]);
            Field("result", recipe.Result);
            Field("resultHQ1", recipe.ResultHq1);
            Field("resultHQ2", recipe.ResultHq2);
            Field("resultHQ3", recipe.ResultHq3);
            Field("resultQty", recipe.ResultQty);
            Field("resultHQ1Qty", recipe.ResultHq1Qty);
            Field("resultHQ2Qty", recipe.ResultHq2Qty);
            Field("resultHQ3Qty", recipe.ResultHq3Qty);
            l.PushString(recipe.ResultName);
            l.SetField(-2, "resultName");
            return 1;
        }
    }
}
