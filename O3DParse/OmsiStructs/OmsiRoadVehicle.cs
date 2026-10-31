using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DParse.Ini;

/// <summary>
/// Represents a BUS or OVH file.
/// </summary>
[Serializable]
public class OmsiRoadVehicle : OmsiComplMapObj
{
    public new VehFriendlyNameCommand? friendlyName;
    public DescriptionCommand? description;
    public TypeCommand? type;
    public AiVehTypeCommand? aiVehType;
    public CouplingBackCommand? couplingBack;
    public CouplingFrontCommand? couplingFront;
    public ControlCableBackCommand[]? controlCableBacks;
    public ControlCableFrontCommand[]? controlCableFronts;
    public CoupleBackCommand? coupleBack;
    public CoupleFrontCommand? coupleFront;
    public CoupleFrontOpenForSoundCommand? coupleFrontOpenForSound;
    public CouplingFrontCharacterCommand? couplingFrontCharacter;
    public BogiesCommand? bogies;
    public SinusCommand? sinus;
    public RailBodyOscCommand? railBodyOsc;
    public ContactShoeCommand[]? contactShoes;
    public RowdyFactorCommand? rowdyFactor;
    public AiBrakePerformanceCommand? aiBrakePerformance;
    public CameraDriverCommand[]? driverCameras;
    public CameraPaxCommand[]? paxCameras;
    public SetCameraStdCommand? stdCamera;
    public SetCameraOutsideCenterCommand? outsideCameraCenter;
    /// <summary>
    /// Height of the centre of mass.
    /// </summary>
    public SchwerpunktCommand? schwerpunkt;
    public RollwiderstandCommand? rollwiderstand;
    public RotPointLongCommand? rotPointLong;
    public InvMinTurnRadiusCommand? invMinTurnRadius;
    public AiDeltaHeightCommand? aiDeltaHeight;
    public AchseCommand[]? achses;
    public NumberCommand? number;
    public RegistrationAutomaticCommand? registrationAutomatic;
    public RegistrationListCommand? registrationList;
    public RegistrationFreeCommand? registrationFree;
    public KmCounterInitCommand? kmCounterInit;
}

[OmsiIniCommand("description")]
[OmsiIniCommandSerializer<OmsiDescriptionSerializer>]
public struct DescriptionCommand
{
    public string description;

    [OmsiIniComments] public string? comments;
}

public class OmsiDescriptionSerializer : IOmsiIniCommandCustomSerializer
{
    public void Deserialize(ref object target, Type targetType, string command, IniFileReader reader, ref int lineNumber, object? parent)
    {
        DescriptionCommand dst = default;

        Span<char> line = stackalloc char[256];
        StringBuilder sb = new();
        while (true)
        {
            var read = reader.ReadLine(line);
            if (read < 0)
                break; // A description that runs to the end of the file just ends there
            if (read > line.Length)
            {
                sb.Append(line);
                continue;
            }

            var lineSub = line[..read];

            if (lineSub.SequenceEqual("[end]"))
                break;

            sb.Append(lineSub);
            sb.AppendLine();
        } 

        dst.description = sb.ToString();
        target = dst;
    }

    public void Serialize(object? obj, Type objType, string? command, TextWriter writer, object? parent)
    {
        if (obj is not DescriptionCommand dsc)
            return;

        writer.WriteLine(command);

        writer.Write(dsc.description);
        writer.WriteLine("[end]");
    }
}

public enum VehicleType : byte
{
    None = 0,
    Bus = 1,
    Train = 2,
    Aircraft = 3
}

[OmsiIniCommand("type")]
public struct TypeCommand
{
    public VehicleType type;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("friendlyname")]
[OmsiIniDerivedCommand<VehFriendlyNameInvCommand>]
public struct VehFriendlyNameCommand
{
    public string manufacturer;
    public string friendlyName;
    public string stdColourScheme;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("friendlyname_inv")]
public struct VehFriendlyNameInvCommand
{
    public string manufacturer;
    public string friendlyName;
    public string stdColourScheme;

    [OmsiIniComments] public string? comments;
}

public enum AiVehType : byte
{
    Car = 0,
    Taxi = 1,
    Bus = 2,
    Truck = 3
}

[OmsiIniCommand("ai_veh_type")]
public struct AiVehTypeCommand
{
    public AiVehType type;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("coupling_back")]
public struct CouplingBackCommand
{
    public Vector3 pos;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("coupling_front")]
public struct CouplingFrontCommand
{
    public Vector3 pos;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("control_cable_front")]
public struct ControlCableFrontCommand
{
    public char side; // Can be one of C, L, or R
    public int number;
    public string readVar;
    public string writeVar;
    public string couplingVar;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("control_cable_back")]
public struct ControlCableBackCommand
{
    public char side; // Can be one of C, L, or R
    public int number;
    public string readVar;
    public string writeVar;
    public string couplingVar;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("couple_back")]
public class CoupleBackCommand : OmsiRoadVehicle
{
    [OmsiIniCommandFile] public new string path = "";
    public string reverse = "false"; // This should be a bool...

    //[OmsiIniComments] public string? comments;
}

[OmsiIniCommand("couple_front")]
public class CoupleFrontCommand : OmsiRoadVehicle
{
    [OmsiIniCommandFile] public new string path = "";
    public string reverse = "false"; // This should be a bool...

    //[OmsiIniComments] public string? comments;
}

public enum CouplingType : byte
{
    Truck = 0, // Three degrees of freedom
    Bus = 1 // Two degrees of freedom
}

[OmsiIniCommand("coupling_front_character")]
public struct CouplingFrontCharacterCommand
{
    public float minMaxAlpha; // Rotation constraint in degrees on the horizontal plane
    public float minBeta; // Rotation constraint in degrees on the vertical plane
    public float maxBeta;
    public CouplingType type;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("boogies")]
public struct BogiesCommand
{
    public float y1;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("sinus")]
public struct SinusCommand
{
    public float wheelRadius;
    public float wheelSpan;
    public float wheelTapering;
    public float damping;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("rail_body_osc")]
public struct RailBodyOscCommand
{
    public float verticalMoment;
    public float oscRotFreqY;
    public float oscRotDampY;
    public float oscRotFreqX;
    public float oscRotDampX;
    public float oscTransFreqZ;
    public float oscTransDampZ;

    [OmsiIniComments] public string? comments;
}

public enum ContactShoeBogie : byte
{
    Front,
    Rear
}

[Flags]
public enum ContactShoeType : byte
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Side = 1 << 2,
}

[OmsiIniCommand("contact_shoe")]
public struct ContactShoeCommand
{
    public ContactShoeBogie bogie;
    public float xMin;
    public float xMax;
    public float zMin;
    public float zMax;
    public ContactShoeType type;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("rowdy_factor")]
public struct RowdyFactorCommand
{
    public float min;
    public float max;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("ai_brakeperformance")]
public struct AiBrakePerformanceCommand
{
    public float avgBrakeDeceleration;
    public float variation;
    public float frictionInfluence;
    public float finalBrakeStrength;
    public float stopPointOffset;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("add_camera_driver")]
public struct CameraDriverCommand
{
    public Vector3 pos;
    public float pivotDistance;
    public float fov;
    public Vector2 rotation;

    public ViewScheduleCommand? viewSchedule;
    public ViewTicketSellingCommand? viewTicketSelling;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("add_camera_pax")]
public struct CameraPaxCommand
{
    public Vector3 pos;
    public float pivotDistance;
    public float fov;
    public Vector2 rotation;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("view_schedule")]
public struct ViewScheduleCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("view_ticketselling")]
public struct ViewTicketSellingCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("set_camera_std")]
public struct SetCameraStdCommand
{
    public int index;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("set_camera_outside_center")]
public struct SetCameraOutsideCenterCommand
{
    public Vector3 pos;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The height of the centre of mass.
/// </summary>
[OmsiIniCommand("schwerpunkt")]
public struct SchwerpunktCommand
{
    public float height;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// Rolling resistance in newtons.
/// </summary>
[OmsiIniCommand("rollwiderstand")]
public struct RollwiderstandCommand
{
    public float resistance;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("rot_pnt_long")]
public struct RotPointLongCommand
{
    public float longitude;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("inv_min_turnradius")]
public struct InvMinTurnRadiusCommand
{
    public float invRadius;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("ai_deltaheight")]
public struct AiDeltaHeightCommand
{
    public float height;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// Creates a new axle.
/// </summary>
[OmsiIniCommand("newachse")]
[OmsiIniCommandSerializer<OmsiAchseSerializer>]
public struct AchseCommand
{
    public AchseLongCommand? longitude;
    public AchseMaxWidthCommand? maxWidth;
    public AchseMinWidthCommand? minWidth;
    public AchseRaddurMesserCommand? raddurMesser;
    public AchseFederCommand? feder;
    public AchseMaxForceCommand? maxForce;
    public AchseDaempferCommand? daempfer;
    public AchseAntriebCommand? antrieb;
    public AchseInertiaInvCommand? inertiaInv;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// Deserializer for the axle block, which comes in two forms: the keyword form
/// (<c>achse_long</c>, <c>achse_feder</c>, ...), which the generic parser handles, and the
/// legacy positional form of nine bare number lines that stock AI cars use.
/// </summary>
public class OmsiAchseSerializer : IOmsiIniCommandCustomSerializer
{
    public void Deserialize(ref object target, Type targetType, string command, IniFileReader reader, ref int lineNumber, object? parent)
    {
        // If the next line opens a keyword (an achse_* line or the next block), the generic
        // parser takes over: read nothing and let it match the verbatim child commands. The
        // legacy positional form is only recognized when the first line reads as a number.
        int next = reader.Peek();
        bool positional = next is >= '0' and <= '9' or '-' or '+' or '.';
        if (!positional)
            return;

        // Legacy positional form: long, max_width, min_width, wheel_diameter, spring,
        // max_force, damper, driven, inertia_inv
        AchseCommand dst = default;
        dst.longitude = new() { pos = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.maxWidth = new() { width = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.minWidth = new() { width = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.raddurMesser = new() { diameter = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.feder = new() { springConst = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.maxForce = new() { load = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.daempfer = new() { damping = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        dst.antrieb = new() { driven = ReadDriven(reader, ref lineNumber) };
        dst.inertiaInv = new() { inertia = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber) };
        target = dst;
    }

    /// <summary>Whether an axle is driven: any non-zero number (a drive share like 0.2
    /// counts as driven) or the word "true".</summary>
    private static bool ReadDriven(IniFileReader reader, ref int lineNumber)
    {
        Span<char> line = stackalloc char[256];
        var read = reader.ReadLine(line);
        if (read < 0)
            return false;
        if (read > line.Length)
            throw new OmsiIniSerializationException($"Value of '{line}' couldn't be parsed as a bool! Line was too long to be parsed.", null, null, lineNumber);
        line = line[..read];
        lineNumber++;

        ReadOnlySpan<char> trimmed = line.Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        return OmsiNumbers.ParseFloat(trimmed) != 0f;
    }

    public void Serialize(object? obj, Type objType, string? commandName, TextWriter writer, object? parent)
    {
        if (obj is not AchseCommand achse)
            return;

        if (commandName != null)
            writer.WriteLine(commandName);

        if (achse.longitude is { } longitude) { writer.WriteLine("achse_long"); writer.WriteLine(longitude.pos); }
        if (achse.maxWidth is { } maxWidth) { writer.WriteLine("achse_maxwidth"); writer.WriteLine(maxWidth.width); }
        if (achse.minWidth is { } minWidth) { writer.WriteLine("achse_minwidth"); writer.WriteLine(minWidth.width); }
        if (achse.raddurMesser is { } raddurMesser) { writer.WriteLine("achse_raddurchmesser"); writer.WriteLine(raddurMesser.diameter); }
        if (achse.feder is { } feder) { writer.WriteLine("achse_feder"); writer.WriteLine(feder.springConst); }
        if (achse.maxForce is { } maxForce) { writer.WriteLine("achse_maxforce"); writer.WriteLine(maxForce.load); }
        if (achse.daempfer is { } daempfer) { writer.WriteLine("achse_daempfer"); writer.WriteLine(daempfer.damping); }
        if (achse.antrieb is { } antrieb) { writer.WriteLine("achse_antrieb"); writer.WriteLine(antrieb.driven ? 1 : 0); }
        if (achse.inertiaInv is { } inertiaInv) { writer.WriteLine("achse_inertia_inv"); writer.WriteLine(inertiaInv.inertia); }
    }
}

/// <summary>
/// Position along the length of the bus.
/// </summary>
[OmsiIniCommand("achse_long")]
[OmsiIniVerbatimCommand]
public struct AchseLongCommand
{
    public float pos;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The maximum width of the axle, inluding the width of the tyres.
/// </summary>
[OmsiIniCommand("achse_maxwidth")]
[OmsiIniVerbatimCommand]
public struct AchseMaxWidthCommand
{
    public float width;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The minimum width of the axle, not inluding the width of the tyres.
/// </summary>
[OmsiIniCommand("achse_minwidth")]
[OmsiIniVerbatimCommand]
public struct AchseMinWidthCommand
{
    public float width;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The wheel diameter.
/// </summary>
[OmsiIniCommand("achse_raddurchmesser")]
[OmsiIniVerbatimCommand]
public struct AchseRaddurMesserCommand
{
    public float diameter;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The suspension's spring constant in KN/m.
/// </summary>
[OmsiIniCommand("achse_feder")]
[OmsiIniVerbatimCommand]
public struct AchseFederCommand
{
    public float springConst;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The maximum load in KN.
/// </summary>
[OmsiIniCommand("achse_maxforce")]
[OmsiIniVerbatimCommand]
public struct AchseMaxForceCommand
{
    public float load;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The Damper constant in KNs/m.
/// </summary>
[OmsiIniCommand("achse_daempfer")]
[OmsiIniVerbatimCommand]
public struct AchseDaempferCommand
{
    public float damping;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// Whether the axle is driven.
/// </summary>
[OmsiIniCommand("achse_antrieb")]
[OmsiIniVerbatimCommand]
public struct AchseAntriebCommand
{
    public bool driven;

    [OmsiIniComments] public string? comments;
}

/// <summary>
/// The reciprocal of the axle's rotational inertia.
/// </summary>
[OmsiIniCommand("achse_inertia_inv")]
[OmsiIniVerbatimCommand]
public struct AchseInertiaInvCommand
{
    public float inertia;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("number")]
public struct NumberCommand
{
    public string path;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("registration_automatic")]
public struct RegistrationAutomaticCommand
{
    public string registrationPrefix;
    public string registrationPostfix;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("registration_list")]
public struct RegistrationListCommand
{
    public string path;
    public string registrationPrefix;
    public string registrationPostfix;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("registration_free")]
public struct RegistrationFreeCommand
{
    [OmsiIniComments] public string? comments;
}

/// <summary>
/// Marks the front coupling as open for sound purposes.
/// </summary>
[OmsiIniCommand("couple_front_open_for_sound")]
public struct CoupleFrontOpenForSoundCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("kmcounter_init")]
public struct KmCounterInitCommand
{
    public int startYear;
    public int kmPerYear;

    [OmsiIniComments] public string? comments;
}
