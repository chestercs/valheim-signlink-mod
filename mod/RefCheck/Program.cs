using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Every assembly the plugin references must be one the game loads: a file in the game's Managed folder or BepInEx's
// core folder. A reference the compiler resolved from the build's reference packs alone (System.ValueTuple for a tuple,
// 0.13.0) builds fine and fails in the game with a TypeLoadException.
//   dotnet run -- <plugin.dll> <folder> [<folder> ...]
static class Program
{
    static int Main(string[] args)
    {
        string dll = args[0];
        string[] folders = args.Skip(1).ToArray();
        using var pe = new PEReader(File.OpenRead(dll));
        MetadataReader md = pe.GetMetadataReader();
        int missing = 0;
        foreach (AssemblyReferenceHandle h in md.AssemblyReferences)
        {
            string name = md.GetString(md.GetAssemblyReference(h).Name);
            bool found = folders.Any(f => File.Exists(Path.Combine(f, name + ".dll")));
            Console.WriteLine($"{(found ? "ok     " : "MISSING")} {name}");
            if (!found) missing++;
        }
        Console.WriteLine(missing == 0 ? "references: all found in the game" : $"references: {missing} MISSING in the game");
        return missing == 0 ? 0 : 1;
    }
}
