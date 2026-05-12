using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCobra.OVL;
using OvlTestBench.Tests;

namespace OvlTestBench;

internal static class Program {
    static int Main(string[] args) {
        bool runPlugins = args.Length == 0 || args.Contains("--plugins");
        string? ovlDir = null;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--ovl-dir") ovlDir = args[i + 1];

        int failures = 0;

        if (runPlugins)
            failures += RunPluginTests();

        if (ovlDir != null)
            failures += RunOvlTests(ovlDir);

        return failures > 0 ? 1 : 0;
    }

    static int RunPluginTests() {
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginsDir)) {
            Console.WriteLine("SKIP plugins — no plugins/ directory found");
            return 0;
        }
        var wasmFiles = Directory.GetFiles(pluginsDir, "*.wasm");
        int failures = 0;
        foreach (var wasm in wasmFiles) {
            var name = Path.GetFileNameWithoutExtension(wasm);
            bool groupPassed = true;
            foreach (var test in PluginTests.All) {
                try { test.Test(wasm); } catch (Exception ex) { Assert.AddError(ex.Message); }
                var r = Assert.Result(test.Name);
                Console.WriteLine($"  {(r.Passed ? "OK  " : "FAIL")} {name}/{r.Name}" +
                                  (r.Passed ? "" : $" — {r.Error}"));
                if (!r.Passed) { groupPassed = false; failures++; }
            }
            Console.WriteLine($"{(groupPassed ? "PASS" : "FAIL")} {name}");
        }
        return failures;
    }

    static int RunOvlTests(string ovlDir) {
        var pairs = DiscoverPairs(ovlDir);
        if (pairs.Count == 0) {
            Console.WriteLine($"SKIP ovl — no OVL pairs found in {ovlDir}");
            return 0;
        }
        int failures = 0;
        foreach (var pair in pairs) {
            bool groupPassed = true;
            foreach (var test in LoadOvls.All) {
                try { test.Test(pair); } catch (Exception ex) { Assert.AddError(ex.Message); }
                var r = Assert.Result(test.Name);
                Console.WriteLine($"  {(r.Passed ? "OK  " : "FAIL")} {pair.Name}/{r.Name}" +
                                  (r.Passed ? "" : $" — {r.Error}"));
                if (!r.Passed) { groupPassed = false; failures++; }
            }
            Console.WriteLine($"{(groupPassed ? "PASS" : "FAIL")} {pair.Name}");
        }
        return failures;
    }

    static List<OvlPair> DiscoverPairs(string dir) {
        var allFiles = Directory.GetFiles(dir, "*.ovl", SearchOption.AllDirectories);
        var common = allFiles.Where(f => Path.GetFileName(f).Contains(".common.")).ToList();
        var unique  = allFiles.Where(f => Path.GetFileName(f).Contains(".unique.")).ToList();
        var pairs = new List<OvlPair>();
        foreach (var c in common) {
            var prefix = Path.GetFileName(c).Split('.')[0];
            var u = unique.FirstOrDefault(f => Path.GetFileName(f).StartsWith(prefix));
            if (u == null) continue;
            pairs.Add(new OvlPair {
                Name = prefix, CommonPath = c, UniquePath = u,
                Files = [
                    new OvlFile { Path = c, Type = OvlType.Common },
                    new OvlFile { Path = u, Type = OvlType.Unique },
                ],
            });
        }
        return pairs;
    }
}
