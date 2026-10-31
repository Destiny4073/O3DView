using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using O3DParse;
using O3DParse.Ini;

namespace O3DParse.Test;

internal class Program
{
    const int testIters = 100;

    static void Main(string[] args)
    {
        // Self-checking test routines; no OMSI installation required
        if (args.Length > 0 && args[0] == "selftest")
        {
            Environment.ExitCode = SelfTest.RunAll();
            return;
        }

        // Bulk smoke test over an OMSI installation: `smoke <directory>`
        if (args.Length >= 2 && args[0] == "smoke")
        {
            Smoke(args[1]);
            return;
        }

        // Parse a single o3d and print a summary: `probe <file.o3d>`
        if (args.Length >= 2 && args[0] == "probe")
        {
            O3DFile o3d = O3DParser.ReadO3D(args[1]);
            System.Numerics.Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
            foreach (var v in o3d.vertices)
            {
                lo = System.Numerics.Vector3.Min(lo, v.pos);
                hi = System.Numerics.Vector3.Max(hi, v.pos);
            }
            Console.WriteLine($"version={o3d.version} flags={o3d.extendedOptions} key=0x{o3d.encryptionKey:X} " +
                $"verts={o3d.vertices.Length} tris={o3d.triangles.Length} mats={o3d.materials.Length} bones={o3d.bones.Length}");
            Console.WriteLine($"bounds: ({lo.X:F3}, {lo.Y:F3}, {lo.Z:F3}) .. ({hi.X:F3}, {hi.Y:F3}, {hi.Z:F3})");
            return;
        }

        // Dump every o3d listed on stdin, in input order: `dumpfiles`
        if (args.Length >= 1 && args[0] == "dumpfiles")
        {
            string? line;
            while ((line = Console.In.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;
                Console.WriteLine($"## {line}");
                DumpOne(line);
            }
            return;
        }

        // Dump a single o3d in the same machine-readable format as the reference dump
        // (floats as exact f32 bit patterns), for cross-implementation diffing: `dump <file.o3d>`
        if (args.Length >= 2 && args[0] == "dump")
        {
            Console.WriteLine($"## {args[1]}");
            DumpOne(args[1]);
            return;
        }

        // Dump every o3d under a directory in the reference dump format: `dumpdir <dir>`
        if (args.Length >= 2 && args[0] == "dumpdir")
        {
            foreach (string file in Directory.EnumerateFiles(args[1], "*.o3d", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(args[1], "*.rdy", SearchOption.AllDirectories)))
            {
                Console.WriteLine($"## {file}");
                DumpOne(file);
            }
            return;
        }

        //string path = args[0];
        //string path = @"export_test.cfg"; // args[0]
        //string path = @"C:\Program Files\OMSI 2\Vehicles\GPM_C2\C2_NGT_V3.bus";
        //string path = @"C:\Program Files\OMSI 2\maps\Grand Paris-Moulon\tile_32_-21.map";
        //string path = @"C:\Program Files\OMSI 2\Vehicles\GPM_C2\Model\modeldata_C2_NGT_V3.cfg";
        //string path = @"C:\Program Files\OMSI 2\Vehicles\MB_C2_EN_BVG\MB_KI_C2_E6_Gn_UE_main.bus";
        string path = @"C:\Program Files\OMSI 2\Vehicles\MAN_SD202\Model\paths.cfg";

        Console.WriteLine("#############################");
        Console.WriteLine("#####  O3DParse Tests  ######");
        Console.WriteLine("#############################");

        string tmpPath = path;//@"C:\Program Files\OMSI 2\Vehicles\MB_C2_EN_BVG\MB_KI_C2_E6_Gn_UE_main.bus";
        using var fs = File.OpenRead(tmpPath);
        var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiPaths>(fs, recursive: true, filepath: tmpPath);

        SpeedTest<OmsiPaths>(path);
        UnparsedCommands<OmsiPaths>(path);
        ReExport<OmsiPaths>(path);

        foreach (var p in args)
        {
            UnparsedCommands<OmsiCFGFile>(p);
        }

        TestMany();

        Console.ReadKey();
    }

    public static void SpeedTest<T>(string path) where T : new()
    {
        Console.WriteLine("\n## Start Speed Test ##");

        using var rs = new MemoryStream(File.ReadAllBytes(path));
        using var ws = new MemoryStream(8192);
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < testIters; i++)
        {
            //using var fs = File.Open(args[0], FileMode.Open, FileAccess.Read, FileShare.Read);
            OmsiIniSerializer.DeserializeIniFile<T>(rs);
            rs.Position = 0;
        }
        TimeSpan readTime = sw.Elapsed / testIters;

        var cfg = OmsiIniSerializer.DeserializeIniFile<T>(rs);
        sw.Restart();
        for (int i = 0; i < testIters; i++)
        {
            OmsiIniSerializer.SerializeIniFile<T>(cfg, ws);
            ws.Position = 0;
        }
        TimeSpan writeTime = sw.Elapsed / testIters;
        sw.Stop();
        Console.WriteLine($"Parsed Ini file ({path}); deserialized in: {readTime}, serialized in: {writeTime}; averaged over {testIters} iterations!");
    }

    public static void UnparsedCommands<T>(string path) where T : new()
    {
        Console.WriteLine("\n## Start Unparsed Commands Test ##");

        HashSet<(string parent, string command)> unparsed = [];
        using var fs = File.OpenRead(path);
        //OmsiIniSerializer.DeserializeIniFile<OmsiCFGFile>(fs, unparsedCommands: unparsed);
        OmsiIniSerializer.DeserializeIniFile<T>(fs,
#if DEBUG
            unparsedCommands: unparsed, 
#endif
            filepath: path, recursive: true);

        Console.WriteLine($"{unparsed.Count} unparsed commands in {path}:");
        List<(string parent, string command)> unparsedSorted = new(unparsed);
        unparsedSorted.Sort((x, y) => x.command.CompareTo(y.command));
        foreach (var (parent, command) in unparsedSorted)
            Console.WriteLine($"\t[{command}] after [{parent}]");
    }

    public static void ReExport<T>(string path) where T : new()
    {
        Console.WriteLine("\n## Start Re-Export Test ##");

        using var fs = File.OpenRead(path);
        using var ws = new MemoryStream(8192);
        //var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiCFGFile>(fs);
        var cfg = OmsiIniSerializer.DeserializeIniFile<T>(fs, filepath: path, recursive: true);
        OmsiIniSerializer.SerializeIniFile(cfg, ws);

        var s = Encoding.Default.GetString(ws.ToArray());
        File.WriteAllText("re-export-test.cfg", s);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            IncludeFields = true,
            WriteIndented = true,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
        };
        string json = JsonSerializer.Serialize(cfg, jsonOptions);
        File.WriteAllText("re-export-test.json", json);
    }

    public static void TestMany()
    {
        Console.WriteLine("\n## Start Many Files Test ##");

        // Test on every *.sco|*.cfg|*.bus|*.map|*.cti in my Omsi dir
        //if (!File.Exists("test_files.txt"))
        //    return;

        //var lines = File.ReadAllLines("test_files.txt");
        string dir = @"C:\Program Files\OMSI 2\";
        var lines = Directory.EnumerateFiles(dir, "*.sco", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(dir, "*.cfg", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(dir, "*.bus", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(dir, "*.map", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(dir, "*.cti", SearchOption.AllDirectories))
            .ToArray();

        ProgressBarString progress = new(30);
        int total = lines.Length;
        int curr = 0;
        int consoleWidth = Console.BufferWidth;
        Console.WriteLine($"Processing {lines.Length} files...\n");
        var progressUpdater = Task.Run(() =>
        {
            while (curr < total)
            {
                progress.SetProgress(curr, total);

                Console.Write("\x1b[1A");
                Console.WriteLine($"Parsing [{progress}] [{curr + 1,6}/{total}] '{System.IO.Path.GetFileName(lines[Math.Min(curr, total - 1)])}'..."
                    .PadRight(consoleWidth - 1)[..(consoleWidth - 1)]);

                Thread.Sleep(20);
            }
        });

        HashSet<(string parent, string command)> unparsed = [];
        ConcurrentBag<(Exception ex, string file)> errors = [];

        var cfgSerializer = new ThreadLocal<OmsiIniSerializer<OmsiCFGFile>>(() => new());// new OmsiIniSerializer<OmsiCFGFile>();
        var cmoSerializer = new ThreadLocal<OmsiIniSerializer<OmsiComplMapObj>>(() => new());

        Stopwatch sw = Stopwatch.StartNew();
        foreach (var line in lines)
        //Parallel.ForEach(lines, line =>
        {
            Interlocked.Increment(ref curr);

            try
            {
                var cfg = LoadOmsiFile(line, unparsed);
            }
            catch (Exception ex)
            {
                errors.Add((ex, line));
            }

            //UnparsedCommands(line);
        }//);
        curr = total;
        Console.WriteLine($"DONE IN {sw.Elapsed}!");

        // Print all the unparsed commands...
        Console.WriteLine($"{unparsed.Count} unparsed commands in:");
        List<(string parent, string command)> unparsedSorted = new(unparsed);
        unparsedSorted.Sort((x, y) => x.command.CompareTo(y.command));
        foreach (var (parent, command) in unparsedSorted)
            Console.WriteLine($"\t[{command}] after [{parent}]");

        // Print all the errors commands...
        Console.WriteLine($"{errors.Count} exceptions while parsing:");
        foreach (var (ex, file) in errors)
            Console.WriteLine($"\t[{file}] {ex.Message}");
    }

    private static string FloatBits(float f) => BitConverter.SingleToInt32Bits(f).ToString("X8");

    private static string Escape(string s) => string.Concat(s.Select(c => c >= 0x20 && c < 0x7F ? c.ToString() : $"\\u{(int)c:X4}"));

    /// <summary>Dumps one o3d in the reference format (floats as exact f32 bit patterns).</summary>
    private static void DumpOne(string file)
    {
        try
        {
            O3DFile o3d = O3DParser.ReadO3D(file);
            bool longIndices = (o3d.extendedOptions & O3DExtendedOptions.LONG_TRIANGLE_INDICES) != 0;
            // Files below version 4 carry no key; the struct defaults it to 0xFFFFFFFF
            uint rawKey = o3d.version >= 4 ? o3d.encryptionKey : 0;
            Console.WriteLine($"FILE {o3d.version} {(longIndices ? 1 : 0)} 0x{rawKey:X8}");
            Console.WriteLine($"COUNTS {o3d.vertices.Length} {o3d.triangles.Length} {o3d.materials.Length} {o3d.bones.Length} 1");
            Console.WriteLine("TR " + TransformBits(o3d.transform));
            for (int i = 0; i < o3d.vertices.Length; i++)
                Console.WriteLine($"VX {i} {VecBits(o3d.vertices[i].pos)} {VecBits(o3d.vertices[i].normal)} {VecBits(o3d.vertices[i].uv)}");
            for (int i = 0; i < o3d.triangles.Length; i++)
                Console.WriteLine($"TRI {i} {o3d.triangles[i].a} {o3d.triangles[i].b} {o3d.triangles[i].c} {o3d.triangles[i].mat}");
            for (int i = 0; i < o3d.materials.Length; i++)
            {
                O3DMaterial m = o3d.materials[i];
                Console.WriteLine($"MAT {i} {FloatBits(m.diffuse.x)} {FloatBits(m.diffuse.y)} {FloatBits(m.diffuse.z)} {FloatBits(m.diffuse.w)} " +
                    $"{FloatBits(m.spec.x)} {FloatBits(m.spec.y)} {FloatBits(m.spec.z)} " +
                    $"{FloatBits(m.emission.x)} {FloatBits(m.emission.y)} {FloatBits(m.emission.z)} {FloatBits(m.specPower)} {Escape(m.textureName)}");
            }
            for (int i = 0; i < o3d.bones.Length; i++)
            {
                O3DBone b = o3d.bones[i];
                Console.WriteLine($"BONE {i} {Escape(b.name)} " + string.Join(" ", b.weights.Select(w => $"{w.index}:{FloatBits(w.weight):08X}")));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR {ex.GetType().Name} {ex.Message}");
        }
    }

    private static string VecBits(O3DVec3 v) => $"{FloatBits(v.x)} {FloatBits(v.y)} {FloatBits(v.z)}";

    private static string VecBits(O3DVec2 v) => $"{FloatBits(v.x)} {FloatBits(v.y)}";

    private static string TransformBits(O3DTransform t) =>
        string.Join(" ", new[] { t.m00, t.m01, t.m02, t.m03, t.m10, t.m11, t.m12, t.m13, t.m20, t.m21, t.m22, t.m23, t.m30, t.m31, t.m32, t.m33 }.Select(FloatBits));

    /// <summary>
    /// Parses every OMSI file under a directory with the matching parser and reports
    /// exceptions, unparsed commands and total time.
    /// </summary>
    public static void Smoke(string dir)
    {
        Console.WriteLine($"\n## Smoke test over '{dir}' ##");

        IEnumerable<string> Enum(string ext) => Directory.EnumerateFiles(dir, ext, SearchOption.AllDirectories);
        string[] exts = ["*.o3d", "*.bus", "*.ovh", "*.cfg", "*.sco", "*.map", "*.cti", "*.sli", "*.rdy", "*.x"];
        List<string> files = exts.SelectMany(Enum).ToList();
        Console.WriteLine($"Processing {files.Count} files...");

        HashSet<(string parent, string command)> unparsed = [];
        List<(string file, Exception ex)> errors = [];
        int parsed = 0;
        int done = 0;
        Stopwatch sw = Stopwatch.StartNew();

        foreach (string file in files)
        {
            done++;
            if (done % 1000 == 0)
                Console.WriteLine($"  {done}/{files.Count} ({sw.Elapsed})...");
            try
            {
                switch (Path.GetExtension(file).ToLowerInvariant())
                {
                    case ".o3d":
                    case ".rdy":
                        O3DParser.ReadO3D(file);
                        break;
                    case ".sli":
                        using (var fs = File.OpenRead(file))
                            OmsiIniSerializer.DeserializeIniFile<OmsiSpline>(fs);
                        break;
                    case ".x":
                        continue; // .x files are not supported by O3DParse
                    default:
                        using (var fs = File.OpenRead(file))
                        {
                            switch (Path.GetExtension(file).ToLowerInvariant())
                            {
                                case ".bus":
                                case ".ovh":
                                    OmsiIniSerializer.DeserializeIniFile<OmsiRoadVehicle>(fs,
#if DEBUG
                                        unparsedCommands: unparsed,
#endif
                                        recursive: true, filepath: file);
                                    break;
                                case ".sco":
                                case ".cti":
                                case ".map":
                                    OmsiIniSerializer.DeserializeIniFile<OmsiComplMapObj>(fs,
#if DEBUG
                                        unparsedCommands: unparsed,
#endif
                                        recursive: true, filepath: file);
                                    break;
                                default:
                                    OmsiIniSerializer.DeserializeIniFile<OmsiCFGFile>(fs,
#if DEBUG
                                        unparsedCommands: unparsed,
#endif
                                        recursive: true, filepath: file);
                                    break;
                            }
                        }
                        break;
                }
                parsed++;
            }
            catch (Exception ex)
            {
                errors.Add((file, ex));
            }
        }

        Console.WriteLine($"Parsed {parsed}/{files.Count} files in {sw.Elapsed}.");
        Console.WriteLine($"{errors.Count} exceptions:");
        foreach (var (file, ex) in errors.Take(30))
            Console.WriteLine($"\t[{file}] {ex.Message}");
        if (errors.Count > 30)
            Console.WriteLine($"\t... and {errors.Count - 30} more");

#if DEBUG
        Console.WriteLine($"{unparsed.Count} unparsed (parent, command) pairs:");
        List<(string parent, string command)> unparsedSorted = [.. unparsed];
        unparsedSorted.Sort((x, y) => x.command.CompareTo(y.command));
        foreach (var (parent, command) in unparsedSorted.Take(60))
            Console.WriteLine($"\t[{command}] after [{parent}]");
        if (unparsed.Count > 60)
            Console.WriteLine($"\t... and {unparsed.Count - 60} more");
#endif
    }

    private static object? LoadOmsiFile(string line, HashSet<(string parent, string command)> unparsedCommands)
    {
        using var fs = File.OpenRead(line);
        object? cfg = null;
        switch (System.IO.Path.GetExtension(line))
        {
            case ".cfg":
                if (line.Contains("maps")
                    || line.Contains("Weather")
                    || line.Contains("texture", StringComparison.CurrentCultureIgnoreCase)
                    || line.Contains("template")
                    || line.Contains("sounds"))
                    break;

                cfg = OmsiIniSerializer.DeserializeIniFile<OmsiCFGFile>(fs,
#if DEBUG
            unparsedCommands: unparsedCommands, 
#endif
                    recursive: true, filepath: line);
                break;
            case ".sco":
            case ".cti":
            case ".map":
                cfg = OmsiIniSerializer.DeserializeIniFile<OmsiComplMapObj>(fs,
#if DEBUG
            unparsedCommands: unparsedCommands, 
#endif
                    recursive: true, filepath: line);
                break;
            case ".bus":
            case ".ovh":
                cfg = OmsiIniSerializer.DeserializeIniFile<OmsiRoadVehicle>(fs,
#if DEBUG
            unparsedCommands: unparsedCommands, 
#endif
                    recursive: true, filepath: line);
                break;
        }

        return cfg;
    }
}
