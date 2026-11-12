using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework C3 Prep: what the quest, mission and battlefield bridges share: a container class found and
    // compiled, the load order, a refusal and the loop of a register pass. Each kind's own rules stay in its bridge.
    public sealed partial class ZoneHost
    {
        // one container class of XI.QuestContent, or of the verify suites
        private class DiscoveredScript<TScript> where TScript : ContainerScript
        {
            public string TypeName;
            public TScript Script;
            // what the constructor or Compile threw
            public string Error;
        }

        private class CompiledScript<TScript, TDef> : DiscoveredScript<TScript>
            where TScript : ContainerScript where TDef : class
        {
            public TDef Def;
        }

        // the classes of the kind among the containers (DiscoverContainers), in type-name order
        private static List<TFound> DiscoverScripts<TFound, TScript>(List<DiscoveredContainer> containers)
            where TFound : DiscoveredScript<TScript>, new() where TScript : ContainerScript =>
            containers.Where(d => typeof(TScript).IsAssignableFrom(d.Type))
                .Select(c => new TFound { TypeName = c.Type.FullName, Script = (TScript)c.Script, Error = c.Error })
                .ToList();

        // the class compiled once; one that throws is kept with the error
        private static void CompileScript<TScript, TDef>(CompiledScript<TScript, TDef> c, Func<TScript, TDef> compile)
            where TScript : ContainerScript where TDef : class
        {
            if (c.Script == null) return;
            try
            {
                c.Def = compile(c.Script);
            }
            catch (Exception e)
            {
                c.Error = InnerMessage(e);
            }
        }

        // those with a LegacyPath first (they take their slots by it, in any order), then those without, by key (they
        // go to the end)
        private static IEnumerable<T> LegacyFirst<T>(IEnumerable<T> all, Func<T, string> legacyPath,
                                                     Func<T, string> sortKey)
        {
            var list = all.ToList();
            return list.Where(x => legacyPath(x) != null)
                .Concat(list.Where(x => legacyPath(x) == null).OrderBy(sortKey, StringComparer.Ordinal));
        }

        // a refused container: with no Lua file to fall back to, the no-fallback line and the key in QuestLoadFailures;
        // else "not registered", one line for each of each when given. Always false
        private bool RefuseContainer(string kind, string key, bool fallback, string reason, IList<string> each)
        {
            if (!fallback)
            {
                log_($"quests: C# {kind} {key} failed to load and has no Lua fallback: {reason}");
                QuestLoadFailures.Add(key);
            }
            else if (each != null) foreach (var p in each) log_($"quests: {key} not registered: {p}");
            else log_($"quests: {key} not registered: {reason}");
            return false;
        }

        private delegate bool RegisterOne<T>(T item, ref string name, ref bool duplicate, out QuestScriptBinding bound);

        // one kind's loop of a register pass, how many registered. A container's unexpected throw leaves it to Lua and
        // the others registered: this runs inside loadContainers, unprotected, before addContainers. after: run once a
        // container's key is known (name holds it then), unless another class has its key
        private int RegisterEach<T>(LuaState l, IEnumerable<T> ordered, int top, Func<T, string> typeName,
                                    RegisterOne<T> one, Action<T, string, QuestScriptBinding> after = null)
        {
            int registered = 0;
            foreach (var item in ordered)
            {
                string type = typeName(item);
                string name = type;
                QuestScriptBinding bound = null;
                bool duplicate = false;
                try
                {
                    if (one(item, ref name, ref duplicate, out bound)) registered++;
                    else bound = null;
                }
                catch (Exception e)
                {
                    log_($"quests: {name} not registered: {e.Message}");
                    bound = null;
                }
                finally
                {
                    l.SetTop(top);
                }
                if (name != type && !duplicate) after?.Invoke(item, name, bound);
            }
            return registered;
        }
    }
}
