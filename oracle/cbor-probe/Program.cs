// Introspect the ACTUAL System.Formats.Cbor surface available offline (the same
// assembly XI.Ipc binds to), so the DTO code uses real method names + the real
// exception type instead of guessed ones.
using System;
using System.Linq;
using System.Reflection;
using System.Formats.Cbor;

internal static class Program
{
    static int Main()
    {
        Console.WriteLine("--- CborWriter Write* methods ---");
        foreach (var m in typeof(CborWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                 .Where(x => x.Name.StartsWith("Write") && x.GetParameters().Length == 1)
                 .OrderBy(x => x.Name))
            Console.WriteLine($"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");

        Console.WriteLine("--- CborReader Read* methods ---");
        foreach (var m in typeof(CborReader).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                 .Where(x => x.Name.StartsWith("Read") && !x.Name.Contains("State") && !x.Name.Contains("Peek"))
                 .OrderBy(x => x.Name))
            Console.WriteLine($"{m.Name}() -> {m.ReturnType.Name}");

        // Discover the exception type(s) by triggering a decode error.
        Console.WriteLine("--- decode-error exception type ---");
        try
        {
            var r = new CborReader(new byte[] { 0xFF }); // bogus
            r.ReadInt32();
        }
        catch (Exception ex)
        {
            for (var t = ex.GetType(); t != null && t != typeof(Exception); t = t.BaseType)
                Console.WriteLine("  " + t.FullName);
            Console.WriteLine("  base: " + ex.GetType().BaseType?.FullName);
        }

        Console.WriteLine("--- encode-error exception type ---");
        try
        {
            var w = new CborWriter();
            w.WriteEndMap(); // no matching start
            w.Encode();
        }
        catch (Exception ex)
        {
            Console.WriteLine("  " + ex.GetType().FullName);
        }
        return 0;
    }
}
