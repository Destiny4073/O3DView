using System.Globalization;
using System.Numerics;
using System.Text;
using O3DParse;
using O3DParse.Ini;

namespace O3DParse.Test;

/// <summary>
/// Self-checking test routines, ported from openOMSI's test suites (omsi-o3d lib.rs,
/// omsi-cfg number.rs/codepage.rs) plus round-trip and robustness checks. Run with
/// <c>dotnet run --project O3DParse.Test -- selftest</c>; exits non-zero on failure.
/// </summary>
internal static class SelfTest
{
    private static int failures = 0;
    private static string currentTest = "";

    private static void Check(bool cond, string msg)
    {
        if (!cond)
        {
            failures++;
            Console.WriteLine($"    FAIL [{currentTest}]: {msg}");
        }
    }

    private static void CheckClose(float a, float b, string msg, float tol = 2e-5f)
        => Check(Math.Abs(a - b) <= tol, $"{msg} ({a} vs {b})");

    private static void Run(string name, Action test)
    {
        currentTest = name;
        int before = failures;
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine($"    FAIL [{name}]: unhandled {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        Console.WriteLine($"{(failures == before ? "PASS" : "FAIL")}  {name}");
        currentTest = "";
    }

    public static int RunAll()
    {
        Console.WriteLine("#############################");
        Console.WriteLine("######  O3DParse SelfTest  ##");
        Console.WriteLine("#############################\n");

        Run("O3D round trip (all versions/flags/keys)", O3DRoundTrip);
        Run("O3D scramble state machine", O3DScrambleStates);
        Run("O3D frac_byte", O3DFracByte);
        Run("O3D junk bytes skipped & bad triangles dropped", O3DJunkBytesAndBadTriangles);
        Run("O3D scramble differs from plain", O3DScrambledDiffersFromPlain);
        Run("O3D Windows-1252 texture names", O3DTextureName1252);
        Run("Numbers: lenient float parsing", NumbersFloat);
        Run("Numbers: lenient integer parsing", NumbersInt);
        Run("Numbers: booleans", NumbersBool);
        Run("Numbers: culture independence", NumbersCulture);
        Run("Encoding detection", EncodingDetection);
        Run("IniFileReader decodes detected encodings", EncodingReader);
        Run("Serializer: missing parameter tolerance", MissingParamTolerance);
        Run("Serializer: disabled blocks", DisabledBlocks);
        Run("Serializer: registration_list parameters", RegistrationList);
        Run("Serializer: legacy positional newachse", LegacyNewAchse);
        Run("Serializer: keyword newachse round trip", KeywordNewAchse);
        Run("Serializer: new_attachment ops", NewAttachment);
        Run("Serializer: spotlight_2", Spotlight2);
        Run("Serializer: particle emitter block", ParticleEmitter);
        Run("Serializer: texchanges array", TexChangesArray);
        Run("Serializer: matl extensions", MatlExtensions);
        Run("Serializer: optional parameters", OptionalParams);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} FAILURES");
        return failures == 0 ? 0 : 1;
    }

    #region O3D

    private static O3DFile MakeSample(byte version, O3DExtendedOptions flags, uint key)
    {
        O3DFile f = new()
        {
            version = version,
            extendedOptions = flags,
            encryptionKey = key,
            vertices =
            [
                new(new O3DVec3(1.25f, -2.5f, 3.75f), new O3DVec3(0f, 0.6f, 0.8f), new O3DVec2(0.1f, 0.9f)),
                new(new O3DVec3(10.3f, 4.7f, -0.9f), new O3DVec3(1f, 0f, 0f), new O3DVec2(0.5f, 0.25f)),
                new(new O3DVec3(-7.11f, 0.33f, 2.2f), new O3DVec3(0f, 0f, 1f), new O3DVec2(0.75f, 0f)),
            ],
            triangles = [new O3DTri(0, 1, 2, 0)],
            materials =
            [
                new O3DMaterial(new O3DVec4(0.5f, 0.6f, 0.7f, 1f), new O3DVec3(0.1f, 0.2f, 0.3f),
                    new O3DVec3(0f, 0f, 0f), 12f, "bus.bmp"),
            ],
            bones =
            [
                new O3DBone("bone", [new O3DWeight(2, 0.5f)]),
            ],
            transform = new O3DTransform(
                1, 0, 0, 4,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1),
        };
        return f;
    }

    private static void CompareO3D(O3DFile expected, O3DFile actual, string what)
    {
        Check(actual.version == expected.version, $"{what}: version {actual.version} != {expected.version}");
        Check(actual.extendedOptions == expected.extendedOptions, $"{what}: flags differ");
        Check(actual.encryptionKey == expected.encryptionKey, $"{what}: key differs");

        Check(actual.vertices != null && actual.vertices.Length == expected.vertices.Length,
            $"{what}: vertex count {actual.vertices?.Length} != {expected.vertices.Length}");
        for (int i = 0; i < expected.vertices.Length && i < (actual.vertices?.Length ?? 0); i++)
        {
            O3DVert e = expected.vertices[i], a = actual.vertices![i];
            CheckClose(a.pos.x, e.pos.x, $"{what}: vert {i} pos.x");
            CheckClose(a.pos.y, e.pos.y, $"{what}: vert {i} pos.y");
            CheckClose(a.pos.z, e.pos.z, $"{what}: vert {i} pos.z");
            CheckClose(a.normal.x, e.normal.x, $"{what}: vert {i} normal.x");
            CheckClose(a.normal.y, e.normal.y, $"{what}: vert {i} normal.y");
            CheckClose(a.normal.z, e.normal.z, $"{what}: vert {i} normal.z");
            CheckClose(a.uv.x, e.uv.x, $"{what}: vert {i} uv.x");
            CheckClose(a.uv.y, e.uv.y, $"{what}: vert {i} uv.y");
        }

        Check(actual.triangles != null && actual.triangles.Length == expected.triangles.Length,
            $"{what}: triangle count {actual.triangles?.Length} != {expected.triangles.Length}");
        for (int i = 0; i < expected.triangles.Length && i < (actual.triangles?.Length ?? 0); i++)
        {
            O3DTri e = expected.triangles[i], a = actual.triangles![i];
            Check(a.a == e.a && a.b == e.b && a.c == e.c && a.mat == e.mat, $"{what}: triangle {i} differs");
        }

        Check(actual.materials != null && actual.materials.Length == expected.materials.Length,
            $"{what}: material count differs");
        for (int i = 0; i < expected.materials.Length && i < (actual.materials?.Length ?? 0); i++)
        {
            O3DMaterial e = expected.materials[i], a = actual.materials![i];
            CheckClose(a.diffuse.x, e.diffuse.x, $"{what}: mat {i} diffuse.x");
            CheckClose(a.diffuse.w, e.diffuse.w, $"{what}: mat {i} diffuse.w");
            CheckClose(a.spec.y, e.spec.y, $"{what}: mat {i} spec.y");
            CheckClose(a.specPower, e.specPower, $"{what}: mat {i} specPower");
            Check(a.textureName == e.textureName, $"{what}: mat {i} texture '{a.textureName}' != '{e.textureName}'");
        }

        Check(actual.bones != null && actual.bones.Length == expected.bones.Length,
            $"{what}: bone count {actual.bones?.Length} != {expected.bones.Length}");
        for (int i = 0; i < expected.bones.Length && i < (actual.bones?.Length ?? 0); i++)
        {
            O3DBone e = expected.bones[i], a = actual.bones![i];
            Check(a.name == e.name, $"{what}: bone {i} name differs");
            Check(a.weights.Length == e.weights.Length, $"{what}: bone {i} weight count differs");
            if (a.weights.Length == e.weights.Length)
            {
                CheckClose(a.weights[0].weight, e.weights[0].weight, $"{what}: bone {i} weight");
                Check(a.weights[0].index == e.weights[0].index, $"{what}: bone {i} weight index");
            }
        }

        O3DTransform t = actual.transform;
        CheckClose(t.m03, expected.transform.m03, $"{what}: transform.m03");
        CheckClose(t.m00, expected.transform.m00, $"{what}: transform.m00");
        CheckClose(t.m11, expected.transform.m11, $"{what}: transform.m11");
        CheckClose(t.m22, expected.transform.m22, $"{what}: transform.m22");
    }

    private static void O3DRoundTrip()
    {
        string path = Path.Combine(Path.GetTempPath(), "o3dparse-selftest.o3d");
        try
        {
            // (version, flags, key); 0xFFFFFFFF is the "open" key
            (byte v, O3DExtendedOptions flags, uint key)[] cases =
            [
                (1, O3DExtendedOptions.NONE, 0xFFFFFFFF),
                (2, O3DExtendedOptions.NONE, 0xFFFFFFFF),
                (3, O3DExtendedOptions.NONE, 0xFFFFFFFF),
                (3, O3DExtendedOptions.LONG_TRIANGLE_INDICES, 0xFFFFFFFF),
                (4, O3DExtendedOptions.NONE, 0xFFFFFFFF),
                (5, O3DExtendedOptions.NONE, 0xFFFFFFFF),
                (7, O3DExtendedOptions.NONE, 0xFFFFFFFF),
                (4, O3DExtendedOptions.NONE, 0),
                (4, O3DExtendedOptions.ALT_ENCRYPTION_SEED, 0),
                (4, O3DExtendedOptions.NONE, 12345),
                (5, O3DExtendedOptions.NONE, 777),
                (5, O3DExtendedOptions.LONG_TRIANGLE_INDICES | O3DExtendedOptions.ALT_ENCRYPTION_SEED, 0xDEAD),
                (6, O3DExtendedOptions.ALT_ENCRYPTION_SEED, 40000),
                (7, O3DExtendedOptions.LONG_TRIANGLE_INDICES | O3DExtendedOptions.ALT_ENCRYPTION_SEED, 4242),
            ];

            foreach (var (v, flags, key) in cases)
            {
                O3DFile f = MakeSample(v, flags, key);
                O3DParser.WriteO3D(path, f);
                O3DFile read = O3DParser.ReadO3D(path);
                CompareO3D(f, read, $"v{v} flags={flags} key=0x{key:X}");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void O3DScrambleStates()
    {
        O3DVec3 RunPos(ushort s)
        {
            O3DVec3 p = new(1, 2, 3);
            O3DVec3 n = O3DVec3.Zero;
            O3DVec2 uv = O3DVec2.Zero;
            O3DScramble.Unscramble(s, ref p, ref n, ref uv);
            return p;
        }

        O3DVec3 p = RunPos(999);
        Check(p.x == 2 && p.y == 1 && p.z == 3, $"state 999: got ({p.x}, {p.y}, {p.z})");
        p = RunPos(1000);
        Check(p.x == 3 && p.y == 2 && p.z == 1, $"state 1000: got ({p.x}, {p.y}, {p.z})");
        p = RunPos(2999);
        Check(p.x == 3 && p.y == 2 && p.z == 1, $"state 2999: got ({p.x}, {p.y}, {p.z})");
        p = RunPos(3000);
        Check(p.x == 1 && p.y == 2 && p.z == 3, $"state 3000: got ({p.x}, {p.y}, {p.z})");
        p = RunPos(7000);
        Check(p.x == 1 && p.y == 2 && p.z == 3, $"state 7000: got ({p.x}, {p.y}, {p.z})");
        p = RunPos(7001);
        Check(p.x == 1 && p.y == 3 && p.z == 2, $"state 7001: got ({p.x}, {p.y}, {p.z})");

        O3DVec3 RunNormal(ushort s)
        {
            O3DVec3 p = O3DVec3.Zero;
            O3DVec3 n = new(1, 2, 3);
            O3DVec2 uv = O3DVec2.Zero;
            O3DScramble.Unscramble(s, ref p, ref n, ref uv);
            return n;
        }

        O3DVec3 n = RunNormal(601);
        Check(n.x == 1 && n.y == 2 && n.z == 3, $"normal 601: got ({n.x}, {n.y}, {n.z})");
        n = RunNormal(4);
        Check(n.x == -1 && n.y == 3 && n.z == 2, $"normal 4: got ({n.x}, {n.y}, {n.z})");
        n = RunNormal(12);
        Check(n.x == -1 && n.y == 3 && n.z == -2, $"normal 12: got ({n.x}, {n.y}, {n.z})");
        n = RunNormal(4502);
        Check(n.x == 2 && n.y == 1 && n.z == 3, $"normal 4502: got ({n.x}, {n.y}, {n.z})");
        n = RunNormal(4500);
        Check(n.x == -1 && n.y == -2 && n.z == 3, $"normal 4500: got ({n.x}, {n.y}, {n.z})");

        // The dead s > 6500 branch: no state may swap the normal's x and z
        for (ushort s = 0; s < 8000; s++)
        {
            O3DVec3 p0 = O3DVec3.Zero;
            O3DVec3 n0 = new(1, 2, 4);
            O3DVec2 uv0 = O3DVec2.Zero;
            O3DScramble.Unscramble(s, ref p0, ref n0, ref uv0);
            Check(Math.Abs(n0.x) != 4f, $"state {s} swapped the normal's x and z");
        }

        O3DVec2 RunUv(ushort s)
        {
            O3DVec3 p = O3DVec3.Zero;
            O3DVec3 n = O3DVec3.Zero;
            O3DVec2 uv = new(1, 1);
            O3DScramble.Unscramble(s, ref p, ref n, ref uv);
            return uv;
        }

        O3DVec2 uv1 = RunUv(1);
        Check(uv1.x == 1 && uv1.y == 1, $"uv 1: got ({uv1.x}, {uv1.y})");
        O3DVec2 uv2 = RunUv(10);
        CheckClose(uv2.x, 0.99f, "uv 10");
        O3DVec2 uv3 = RunUv(30);
        CheckClose(uv3.x, 0.91f, "uv 30 x");
        CheckClose(uv3.y, 0.64f, "uv 30 y");
        O3DVec2 uv4 = RunUv(1002);
        CheckClose(uv4.y, 1f - 4f / 2500f, "uv 1002 y");
    }

    private static void O3DFracByte()
    {
        Check(O3DScramble.FracByte(new O3DVec3(1, 2, 3)) == 0, "frac_byte(1,2,3) != 0");
        Check(O3DScramble.FracByte(new O3DVec3(1.5f, -2.5f, 3.5f)) == 75, "frac_byte(1.5,-2.5,3.5) != 75");
    }

    private static void O3DJunkBytesAndBadTriangles()
    {
        // Hand-made file: junk bytes before a section, a junk byte between sections, and a
        // triangle naming a vertex the file doesn't have. Version 3: flags byte, u32 counts.
        MemoryStream ms = new();
        using (BinaryWriter w = new(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write((byte)0x84); w.Write((byte)0x19);
            w.Write((byte)3);       // version
            w.Write((byte)0);       // flags
            w.Write((byte)0x00); w.Write((byte)0xAB); // junk
            w.Write((byte)0x17);
            w.Write(1u);            // 1 vertex
            foreach (float f in new[] { 1f, 2f, 3f, 0f, 0f, 1f, 0.5f, 0.5f })
                w.Write(f);
            w.Write((byte)0x33);    // junk
            w.Write((byte)0x49);
            w.Write(2u);            // 2 triangles; the second names vertex 5
            w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)0); w.Write((ushort)5); w.Write((ushort)0); w.Write((ushort)0);
            ms.Position = 0;
        }

        string path = Path.Combine(Path.GetTempPath(), "o3dparse-selftest-junk.o3d");
        File.WriteAllBytes(path, ms.ToArray());
        try
        {
            O3DFile f = O3DParser.ReadO3D(path);
            Check(f.vertices.Length == 1, $"junk: expected 1 vertex, got {f.vertices.Length}");
            Check(f.triangles.Length == 1, $"junk: expected 1 triangle (bad dropped), got {f.triangles.Length}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void O3DScrambledDiffersFromPlain()
    {
        // key 0 restarts the state at every vertex; the first one gets state 0 -> x/y swap
        O3DFile scrambled = MakeSample(4, O3DExtendedOptions.NONE, 0);
        O3DFile plain = MakeSample(4, O3DExtendedOptions.NONE, 0xFFFFFFFF);

        string p1 = Path.Combine(Path.GetTempPath(), "o3dparse-selftest-scrambled.o3d");
        string p2 = Path.Combine(Path.GetTempPath(), "o3dparse-selftest-plain.o3d");
        try
        {
            O3DParser.WriteO3D(p1, scrambled);
            O3DParser.WriteO3D(p2, plain);
            byte[] b1 = File.ReadAllBytes(p1);
            byte[] b2 = File.ReadAllBytes(p2);
            Check(!b1.AsSpan(6).SequenceEqual(b2.AsSpan(6)), "scrambled and plain files are identical");
        }
        finally
        {
            File.Delete(p1);
            File.Delete(p2);
        }
    }

    private static void O3DTextureName1252()
    {
        // Material texture names decode as Windows-1252: 0xFC is ü, not the Latin-1 control
        O3DFile f = MakeSample(3, O3DExtendedOptions.NONE, 0xFFFFFFFF);
        f.materials[0] = new O3DMaterial(O3DVec4.One, O3DVec3.Zero, O3DVec3.Zero, 1f, "T\u00FCr.x");
        // Write via the writer (which uses 1252), then hand-corrupt: rewrite the name byte to
        // check decoding: instead just round-trip and check the character survives
        string path = Path.Combine(Path.GetTempPath(), "o3dparse-selftest-1252.o3d");
        try
        {
            O3DParser.WriteO3D(path, f);
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes.Contains((byte)0xFC), "the ü was not written as byte 0xFC");
            O3DFile read = O3DParser.ReadO3D(path);
            Check(read.materials[0].textureName == "T\u00FCr.x",
                $"texture name decoded as '{read.materials[0].textureName}'");
        }
        finally
        {
            File.Delete(path);
        }
    }

    #endregion

    #region Numbers

    private static void NumbersFloat()
    {
        Check(OmsiNumbers.ParseDouble("6.21874087223886E-7") == 6.21874087223886E-7, "scientific notation");
        Check(OmsiNumbers.ParseDouble(" -13.4285734512165 ") == -13.4285734512165, "negative with spaces");
        Check(OmsiNumbers.ParseDouble("junk") == 0, "junk -> 0");
        Check(OmsiNumbers.ParseDouble("") == 0, "empty -> 0");
        Check(OmsiNumbers.ParseDouble("1.") == 1, "1. -> 1");
        Check(OmsiNumbers.ParseDouble("5 (metres)") == 5, "5 (metres) -> 5");
        Check(OmsiNumbers.ParseDouble("1,5") == 1.5, "1,5 -> 1.5");
        Check(OmsiNumbers.ParseFloat("2.5") == 2.5f, "2.5");
        foreach (string s in new[] { "nan", "NaN", "inf", "-inf", "Infinity", "1e999", "-1e999" })
            Check(OmsiNumbers.ParseDouble(s) == 0.0, $"{s} -> 0");
        Check(OmsiNumbers.ParseFloat("1e39") == 0f, "1e39 -> 0 (out of f32 range)");
    }

    private static void NumbersInt()
    {
        Check(OmsiNumbers.ParseLong("4192") == 4192, "4192");
        Check(OmsiNumbers.ParseLong("3.0") == 3, "3.0 -> 3");
        Check(OmsiNumbers.ParseLong("junk") == 0, "junk -> 0");
        Check(OmsiNumbers.ParseLong("") == 0, "empty -> 0");
        Check(OmsiNumbers.ParseInt32("-5") == -5, "-5");
        Check(OmsiNumbers.ParseInt32("99999999999999") == int.MaxValue, "huge -> clamped to int.MaxValue");
        Check(OmsiNumbers.ParseUInt32("-2") == 0, "-2 -> 0 (unsigned)");
        Check(OmsiNumbers.ParseByte("300") == 255, "300 -> clamped to 255");
    }

    private static void NumbersBool()
    {
        Check(OmsiNumbers.ParseBool("1"), "1");
        Check(!OmsiNumbers.ParseBool("0"), "0");
        Check(OmsiNumbers.ParseBool("true"), "true");
        Check(OmsiNumbers.ParseBool("TRUE"), "TRUE");
        Check(!OmsiNumbers.ParseBool("false"), "false");
        Check(!OmsiNumbers.ParseBool(""), "empty");
        Check(OmsiNumbers.ParseBool("0.2"), "0.2 (drive share counts as driven)");
    }

    private static void NumbersCulture()
    {
        CultureInfo old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Check(OmsiNumbers.ParseFloat("1.5") == 1.5f, "de-DE: '1.5' must still read as 1.5");
            Check(OmsiNumbers.ParseFloat("1,5") == 1.5f, "de-DE: '1,5' reads as 1.5");
            Check(OmsiNumbers.ParseInt32("1234") == 1234, "de-DE: int");
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    #endregion

    #region Encoding

    private static void EncodingDetection()
    {
        Check(OmsiEncoding.DetectTextEncoding("plain ascii [mesh]"u8) == OmsiEncoding.Windows1252, "ASCII -> 1252");
        Check(OmsiEncoding.DetectTextEncoding("Überlandbus"u8) == Encoding.UTF8, "UTF-8 detected");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1251.GetBytes("[friendlyname]\r\nЛиАЗ 5292.20\r\nЗаводская\r\n")) == OmsiEncoding.Windows1251, "Cyrillic -> 1251");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1251.GetBytes("верх.png")) == OmsiEncoding.Windows1251, "Cyrillic short -> 1251");
        // German never has three accented letters in a row, even with "Größe"
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1252.GetBytes("Größe der Straße, Bahnübergang, Müllerstraße")) == OmsiEncoding.Windows1252, "German -> 1252");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1252.GetBytes("Volumenstrom in m³/s, Dichte in g/m³")) == OmsiEncoding.Windows1252, "m³/s -> 1252");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1250.GetBytes("Łazarz, Śródka, Żegrze, Dworzec Główny")) == OmsiEncoding.Windows1250, "Polish -> 1250");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1250.GetBytes("Praha,Třebenická\r\nPředboj,rozcestí\r\nKojetice,Tůmovka\r\n")) == OmsiEncoding.Windows1250, "Czech -> 1250");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1250.GetBytes("Sremčica, Ćuprija, Đurđevo, Šabac")) == OmsiEncoding.Windows1250, "Serbian -> 1250");
        // Without a Š or Ž beside them the letters read the same as French è and Danish æ
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1250.GetBytes("Sremčica, Ćuprija")) == OmsiEncoding.Windows1252, "Serbian without caron -> 1252");
        // Western text with these letters stays 1252: Italian at the end of a word, Danish with æ/å
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1252.GetBytes("Città più bella, così però")) == OmsiEncoding.Windows1252, "Italian -> 1252");
        Check(OmsiEncoding.DetectTextEncoding(OmsiEncoding.Windows1252.GetBytes("Københavns Hovedbanegård, Nørreport, Ærø")) == OmsiEncoding.Windows1252, "Danish -> 1252");
    }

    private static void EncodingReader()
    {
        static string ReadAll(byte[] bytes)
        {
            MemoryStream ms = new(bytes);
            using IniFileReader reader = new(ms);
            StringBuilder sb = new();
            Span<char> line = stackalloc char[256];
            while (reader.ReadLine(line) is int read && read >= 0)
            {
                sb.Append(line[..read]);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        Check(ReadAll(OmsiEncoding.Windows1251.GetBytes("[station]\r\nЛиАЗ 5292\r\n")) == "[station]\nЛиАЗ 5292\n",
            "1251 bytes decoded as Cyrillic");
        Check(ReadAll("Überlandbus\n"u8.ToArray()) == "Überlandbus\n", "UTF-8 bytes decoded");
        Check(ReadAll([(byte)0xFC, (byte)0xE4, (byte)'\r', (byte)'\n']) == "üä\n", "1252 high bytes decoded");
        Check(ReadAll(OmsiEncoding.Windows1250.GetBytes("Praha,Třebenická\nPředboj,rozcestí\n")) == "Praha,Třebenická\nPředboj,rozcestí\n",
            "1250 bytes decoded");
        // UTF-16 LE with BOM
        byte[] utf16 = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("[mesh]\r\nbus.o3d\r\n")];
        Check(ReadAll(utf16) == "[mesh]\nbus.o3d\n", "UTF-16 LE BOM decoded");
    }

    #endregion

    #region Serializer

    private static T Parse<T>(string text) where T : new()
    {
        using MemoryStream ms = new(Encoding.UTF8.GetBytes(text));
        return OmsiIniSerializer.DeserializeIniFile<T>(ms);
    }

    private static void MissingParamTolerance()
    {
        // A command whose parameter is missing must not eat the next command's line
        var cfg = Parse<OmsiCFGFile>(
            "[detail_factor]\n" +
            "[tex_detail_factor]\n" +
            "0.5\n");
        Check(cfg.detailFactor != null, "detail_factor parsed");
        Check(cfg.texDetailFactor != null, "tex_detail_factor parsed");
        Check(cfg.texDetailFactor!.detailFactor == 0.5f, "tex_detail_factor value");
    }

    private static void DisabledBlocks()
    {
        var cfg = Parse<OmsiCFGFile>(
            "-<DISABLED>-\n" +
            "[mesh]\n" +
            "disabled.o3d\n" +
            "-<ENABLED>-\n" +
            "[mesh]\n" +
            "enabled.o3d\n");
        Check(cfg.meshes != null && cfg.meshes.Length == 1, $"expected 1 mesh, got {cfg.meshes?.Length}");
        Check(cfg.meshes![0].path == "enabled.o3d", $"mesh path '{cfg.meshes[0].path}'");
    }

    private static void RegistrationList()
    {
        var bus = Parse<OmsiRoadVehicle>(
            "[registration_list]\n" +
            "plates.txt\n" +
            "BVG-\n" +
            " (Berlin)\n");
        Check(bus.registrationList != null, "registration_list parsed");
        Check(bus.registrationList!.Value.path == "plates.txt", $"path '{bus.registrationList.Value.path}'");
        Check(bus.registrationList.Value.registrationPrefix == "BVG-", $"prefix '{bus.registrationList.Value.registrationPrefix}'");
        Check(bus.registrationList.Value.registrationPostfix == " (Berlin)", $"postfix '{bus.registrationList.Value.registrationPostfix}'");
    }

    private static void LegacyNewAchse()
    {
        var bus = Parse<OmsiRoadVehicle>(
            "[newachse]\n" +
            "5.2\n" +   // long
            "1.1\n" +   // max width
            "0.9\n" +   // min width
            "0.55\n" +  // wheel diameter
            "200000\n" +// spring
            "65000\n" + // max force
            "9000\n" +  // damper
            "1\n" +     // driven
            "0.02\n");  // inertia
        Check(bus.achses != null && bus.achses.Length == 1, $"expected 1 axle, got {bus.achses?.Length}");
        if (bus.achses is { Length: 1 } axles)
        {
            AchseCommand a = axles[0];
            Check(a.longitude != null && a.longitude!.Value.pos == 5.2f, $"long {a.longitude?.pos}");
            Check(a.maxWidth != null && a.maxWidth!.Value.width == 1.1f, "max width");
            Check(a.minWidth != null && a.minWidth!.Value.width == 0.9f, "min width");
            Check(a.raddurMesser != null && a.raddurMesser!.Value.diameter == 0.55f, "wheel diameter");
            Check(a.feder != null && a.feder!.Value.springConst == 200000f, "spring");
            Check(a.maxForce != null && a.maxForce!.Value.load == 65000f, "max force");
            Check(a.daempfer != null && a.daempfer!.Value.damping == 9000f, "damper");
            Check(a.antrieb != null && a.antrieb!.Value.driven, "driven");
            Check(a.inertiaInv != null && a.inertiaInv!.Value.inertia == 0.02f, "inertia");
        }
    }

    private static void KeywordNewAchse()
    {
        var bus = Parse<OmsiRoadVehicle>(
            "[newachse]\n" +
            "achse_long\n5.2\n" +
            "achse_antrieb\n1\n" +
            "achse_feder\n200000\n");
        Check(bus.achses != null && bus.achses.Length == 1, $"expected 1 axle, got {bus.achses?.Length}");
        if (bus.achses is { Length: 1 } axles)
        {
            AchseCommand a = axles[0];
            Check(a.longitude != null && a.longitude!.Value.pos == 5.2f, "long");
            Check(a.antrieb != null && a.antrieb!.Value.driven, "driven");
            Check(a.feder != null && a.feder!.Value.springConst == 200000f, "spring");
            Check(a.maxWidth == null, "max width not read");
        }

        // And it round-trips through the custom serializer in keyword form
        using MemoryStream ws = new();
        OmsiIniSerializer.SerializeIniFile(bus, ws);
        ws.Position = 0;
        var bus2 = OmsiIniSerializer.DeserializeIniFile<OmsiRoadVehicle>(ws);
        Check(bus2.achses is { Length: 1 } && bus2.achses[0].longitude!.Value.pos == 5.2f, "round trip long");
        Check(bus2.achses is { Length: 1 } && bus2.achses[0].feder!.Value.springConst == 200000f, "round trip spring");
        Check(bus2.achses is { Length: 1 } && bus2.achses[0].antrieb!.Value.driven, "round trip driven");
    }

    private static void NewAttachment()
    {
        var sco = Parse<OmsiComplMapObj>(
            "[new_attachment]\n" +
            "attach_trans\n" +
            "1\n2\n3\n" +
            "attach_rot_z\n" +
            "45\n");
        Check(sco.attachements != null && sco.attachements.Length == 1, $"expected 1 attachment, got {sco.attachements?.Length}");
        if (sco.attachements is { Length: 1 } atts)
        {
            AttachmentOp[] ops = atts[0].ops ?? [];
            Check(ops.Length == 2, $"expected 2 ops, got {ops.Length}");
            if (ops.Length == 2)
            {
                Check(ops[0].op == "attach_trans" && ops[0].x == 1 && ops[0].y == 2 && ops[0].z == 3, $"op 0: {ops[0].op} ({ops[0].x}, {ops[0].y}, {ops[0].z})");
                Check(ops[1].op == "attach_rot_z" && ops[1].z == 45, $"op 1: {ops[1].op} z={ops[1].z}");
            }
        }
    }

    private static void Spotlight2()
    {
        // Flag left out: mirrored (the missing parameter reads as 0 -> < 0.5)
        var cfg = Parse<OmsiCFGFile>(
            "[spotlight_2]\n" +
            "1\n0\n0\n" +
            "0\n0\n1\n" +
            "1\n1\n1\n" +
            "50\n" +
            "10\n" +
            "25\n" +
            "door_light\n");
        Check(cfg.spotLights2 != null && cfg.spotLights2.Length == 1, $"expected 1 spotlight_2, got {cfg.spotLights2?.Length}");
        if (cfg.spotLights2 is { Length: 1 } spots)
        {
            Check(spots[0].var == "door_light", $"var '{spots[0].var}'");
            Check(spots[0].IsMirrored, "absent flag means mirrored");
            Check(spots[0].range == 50f, "range");
        }

        // Written flag: 1 -> not mirrored, 0 -> mirrored
        var cfg2 = Parse<OmsiCFGFile>(
            "[spotlight_2]\n" +
            "1\n0\n0\n0\n0\n1\n1\n1\n1\n50\n10\n25\n" +
            "door_light\n" +
            "0\n");
        if (cfg2.spotLights2 is { Length: 1 } spots2)
            Check(spots2[0].IsMirrored, "flag 0 means mirrored");
        var cfg3 = Parse<OmsiCFGFile>(
            "[spotlight_2]\n" +
            "1\n0\n0\n0\n0\n1\n1\n1\n1\n50\n10\n25\n" +
            "door_light\n" +
            "1\n");
        if (cfg3.spotLights2 is { Length: 1 } spots3)
            Check(!spots3[0].IsMirrored, "flag 1 means not mirrored");
    }

    private static void ParticleEmitter()
    {
        var cfg = Parse<OmsiCFGFile>(
            "[mesh]\n" +
            "bus.o3d\n" +
            "[particle_emitter]\n" +
            "smoke.ps\n" +
            "ps_freq\n" +
            "20\n" +
            "ps_g\n" +
            "-9.81\n" +
            "[mesh]\n" +
            "other.o3d\n");
        Check(cfg.meshes != null && cfg.meshes.Length == 2, $"expected 2 meshes, got {cfg.meshes?.Length}");
        ParticleEmitterCommand? emitter = cfg.meshes?[0].particleEmitters is { Length: 1 } e ? e[0] : null;
        Check(emitter != null, "emitter parsed");
        Check(emitter?.lines is string[] lines && lines.Length == 5
            && lines[0] == "smoke.ps" && lines[1] == "ps_freq" && lines[2] == "20"
            && lines[3] == "ps_g" && lines[4] == "-9.81", $"emitter lines: [{string.Join(", ", emitter?.lines ?? [])}]");
    }

    private static void TexChangesArray()
    {
        var cfg = Parse<OmsiCFGFile>(
            "[mesh]\n" +
            "bus.o3d\n" +
            "[texchanges]\n" +
            "texture\\chtex_a.cfg\n" +
            "[texchanges]\n" +
            "texture\\chtex_b.cfg\n");
        var meshes = cfg.meshes;
        Check(meshes?.Length == 1, $"expected 1 mesh, got {meshes?.Length}");
        int texCount = meshes is { Length: 1 } ? meshes[0].texChanges?.Length ?? 0 : -1;
        Check(texCount == 2, $"expected 2 texchanges, got {texCount}");
        if (meshes is { Length: 1 } && meshes[0].texChanges is TexChangesCommand[] { Length: 2 } t)
            Check(t[1].path == "texture\\chtex_b.cfg", $"texchanges path '{t[1].path}'");
    }

    private static void MatlExtensions()
    {
        var cfg = Parse<OmsiCFGFile>(
            "[mesh]\n" +
            "bus.o3d\n" +
            "[matl]\n" +
            "paint.bmp\n" +
            "0\n" +
            "[matl_glow]\n" +
            "glowmask.bmp\n" +
            "0.8\n" +
            "[led_glow_effect]\n" +
            "0.7\n" +
            "[matl_envmaprealtime]\n" +
            "[matl_zbias]\n" +
            "3\n" +
            "[matl_noZwrite]\n");
        var meshes = cfg.meshes;
        Check(meshes?.Length == 1, $"expected 1 mesh, got {meshes?.Length}");
        var matls = meshes is { Length: 1 } ? meshes[0].matls : null;
        Check(matls?.Length == 1, "expected 1 matl");
        if (matls is { Length: 1 } && matls[0] is MatlCommand m)
        {
            Check(m.glow != null && m.glow!.path == "glowmask.bmp" && m.glow.value == 0.8f, $"glow: {m.glow?.path} {m.glow?.value}");
            Check(m.ledGlowEffect != null && m.ledGlowEffect!.value == 0.7f, "led_glow_effect");
            Check(m.envmapRealtime != null, "envmaprealtime");
            Check(m.zBias != null && m.zBias!.zBias == 3, "zbias");
            Check(m.noZWrite != null, "noZwrite still parsed");
        }
    }

    private static void OptionalParams()
    {
        // [matl_lightmap]'s variable parameter is optional; the next command must survive
        var cfg = Parse<OmsiCFGFile>(
            "[mesh]\n" +
            "bus.o3d\n" +
            "[matl]\n" +
            "paint.bmp\n" +
            "0\n" +
            "[matl_lightmap]\n" +
            "lightmap.bmp\n" +
            "[matl_nightmap]\n" +
            "night.bmp\n");
        var meshes = cfg.meshes;
        Check(meshes?.Length == 1, $"expected 1 mesh, got {meshes?.Length}");
        var matls = meshes is { Length: 1 } ? meshes[0].matls : null;
        Check(matls?.Length == 1, "matl parsed");
        if (matls is { Length: 1 } && matls[0] is MatlCommand m)
        {
            Check(m.lightmap != null && m.lightmap!.path == "lightmap.bmp", $"lightmap path '{m.lightmap?.path}'");
            Check(m.lightmap!.var == null, $"optional var is null, got '{m.lightmap.var}'");
            Check(m.nightmap != null && m.nightmap!.path == "night.bmp", "next command survived");
        }
    }

    #endregion
}
