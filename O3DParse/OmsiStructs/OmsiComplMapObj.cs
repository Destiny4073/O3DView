using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DParse.Ini;

/// <summary>
/// Represents an SCO, CTI, BUS, or OVH file.
/// </summary>
[Serializable]
public class OmsiComplMapObj : OmsiCFGFile
{
    public ScriptShareCommand? scriptShare;
    public VarNameListCommand? varNameList;
    public StringVarNameListCommand? stringVarNameList;
    public ScriptCommand? script;
    public ConstFileCommand? constFile;
    public OmsiCFGFile? model;
    public SoundCommand? sound;
    public SoundAICommand? soundAI;
    public OmsiPaths? pathFiles;
    public OmsiPassengerCabin? passengerCabin;
    public OnlyEditorCommand? onlyEditor;
    public ComplexityCommand? complexity;
    public AbsHeightCommand? absHeight;
    public RenderTypeCommand? renderType;
    public CrossingHeightDeformationCommand? crossingHeightDeformation;
    public BusStopCommand? busStop;
    public EntryPointCommand? entryPoint;
    public CarParkPCommand? carParkP;
    public TrafficLightCommand? trafficLight;
    public SignalCommand? signal;
    public SwitchCommand? @switch;
    public HelpArrowCommand? helpArrow;
    public DepotCommand? depot;
    public PetrolStationCommand? petrolStation;
    public TreeCommand? tree;
    public FriendlyNameCommand? friendlyName;
    public GroupsCommand? groups;
    public LightMapMappingCommand? lightMapMapping;
    public NightMapModeCommand? nightMapMode;
    public SplineHelperCommand[]? splineHelpers;
    public AddCameraReflection2Command[]? reflectionCameras;
    public MassCommand? mass;
    public MomentOfInertiaCommand? momentOfInertia;
    public CogCommand? cog;
    public BoundingBoxCommand? boundingBox;
    public CollisionMeshCommand? collisionMesh;
    public NoCollisionCommand? noCollision;
    public FixedCommand? @fixed;
    public CrashModePoleCommmand? crashModePole;
    public SurfaceCommmand? surface;
    public NewAttachementCommmand[]? attachements;
    public MapLightCommmand[]? mapLights;
    public NoMapLightingCommmand? noMapLighting;
    public PathCommand2[]? paths;
    public TrafficLightsGroupCommand? trafficLightsGroup;
    public TriggerBoxNewCommand[]? triggerBoxes;

    //[OmsiIniComments] public string? comments;
    //[OmsiIniComments(true)] public string? postComments;
}

[OmsiIniCommand("scriptshare")]
public class ScriptShareCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("varnamelist")]
public class VarNameListCommand
{
    public string[] paths = [];

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("stringvarnamelist")]
public class StringVarNameListCommand
{
    public string[] paths = [];

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("script")]
public class ScriptCommand
{
    public string[] paths = [];

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("constfile")]
public class ConstFileCommand
{
    public string[] paths = [];

    [OmsiIniComments] public string? comments;
}

[Serializable]
[OmsiIniCommand("sound")]
public class SoundCommand
{
    public string path = string.Empty; // TODO:

    [OmsiIniComments] public string? comments;
}

[Serializable]
[OmsiIniCommand("sound_ai")]
public class SoundAICommand
{
    public string path = string.Empty; // TODO:

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("onlyeditor")]
public class OnlyEditorCommand
{
    [OmsiIniComments] public string? comments;
}

public enum OmsiComplexity
{
    VeryImportant,
    Important,
    Normal,
    Detail
}

[OmsiIniCommand("complexity")]
public class ComplexityCommand
{
    public OmsiComplexity complexity;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("absheight")]
public class AbsHeightCommand
{
    [OmsiIniComments] public string? comments;
}

public enum OmsiRenderPriority
{
    PreSurface = 0,
    Surface = 1,
    OnSurface = 2,
    One = 3,
    Two = 4,
    Three = 5,
    Four = 6
}

public class OmsiRenderTypeSerializer : IOmsiIniCommandCustomSerializer
{
    public void Deserialize(ref object target, Type targetType, string command, IniFileReader reader, ref int lineNumber, object? parent)
    {
        RenderTypeCommand renderType = (target as RenderTypeCommand) ?? new();
        Span<char> line = stackalloc char[256];
        var read = reader.ReadLine(line);
        if (read < 0)
            throw new OmsiIniSerializationException($"Reached the end of the file unexpectedly!", null, null, lineNumber);
        if (read > line.Length)
            throw new OmsiIniSerializationException($"Value of '{line}' couldn't be parsed as a {targetType.Name}! Line was too long to be parsed.", null, null, lineNumber);
        line = line[..read];

        renderType.priority = line switch
        {
            "surface" => OmsiRenderPriority.Surface,
            "presurface" => OmsiRenderPriority.PreSurface,
            "on_surface" => OmsiRenderPriority.OnSurface,
            "1" => OmsiRenderPriority.One,
            "2" => OmsiRenderPriority.Two,
            "3" => OmsiRenderPriority.Three,
            "4" => OmsiRenderPriority.Four,
            _ => OmsiRenderPriority.Two,
        };
        lineNumber++;
        target = renderType;
    }

    public void Serialize(object? obj, Type objType, string? command, TextWriter writer, object? parent)
    {
        if (obj is not RenderTypeCommand renderType)
            throw new OmsiIniSerializationException();

        writer.WriteLine(command);

        string val = renderType.priority switch
        {
            OmsiRenderPriority.Surface => "surface",
            OmsiRenderPriority.PreSurface => "presurface",
            OmsiRenderPriority.OnSurface => "on_surface",
            OmsiRenderPriority.One => "1",
            OmsiRenderPriority.Two => "2",
            OmsiRenderPriority.Three => "3",
            OmsiRenderPriority.Four => "4",
            _ => "2",
        };
        writer.WriteLine(val);
    }
}

[OmsiIniCommand("rendertype")]
[OmsiIniCommandSerializer<OmsiRenderTypeSerializer>]
public class RenderTypeCommand
{
    public OmsiRenderPriority priority;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("crossing_heightdeformation")]
public class CrossingHeightDeformationCommand
{
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("busstop")]
public class BusStopCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("entrypoint")]
public class EntryPointCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("carpark_p")]
public class CarParkPCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("trafficlight")]
public class TrafficLightCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("signal")]
public class SignalCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("switch")]
public class SwitchCommand
{
    public int switchInd;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("helparrow")]
public class HelpArrowCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("depot")]
public class DepotCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("petrolstation")]
public class PetrolStationCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("tree")]
public class TreeCommand
{
    public string texture = string.Empty;
    public float minHeight;
    public float maxHeight;
    public float minRatio;
    public float maxRatio;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("friendlyname")]
public class FriendlyNameCommand
{
    public string name = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("groups")]
public class GroupsCommand
{
    public string[] groups = [];

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("LightMapMapping")]
public class LightMapMappingCommand
{
    [OmsiIniComments] public string? comments;
}

public enum OmsiNightMapMode
{
    StreetLight,
    Continuous,
    ResidentialBuilding,
    CommercialBuilding,
    School
}

[OmsiIniCommand("NightMapMode")]
public class NightMapModeCommand
{
    public OmsiNightMapMode mode;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("splinehelper")]
public class SplineHelperCommand
{
    public string path = string.Empty;
    public Vector3 position;
    public float heading;
    public float pitch;
    public float cant;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("add_camera_reflexion")]
public class AddCameraReflectionCommand
{
    public Vector3 position;
    public float orbitDistance;
    public float fov;
    public float pitch;
    public float yaw;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("add_camera_reflexion_2")]
[OmsiIniDerivedCommand<AddCameraReflectionCommand>]
public class AddCameraReflection2Command
{
    public Vector3 position;
    public float orbitDistance;
    public float fov;
    public float pitch;
    public float yaw;
    public float renderDistance;

    [OmsiIniComments] public string? comments;
}

#region Physics Commands
[OmsiIniCommand("mass")]
public class MassCommand
{
    public float mass;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("momentofintertia")]
public class MomentOfInertiaCommand
{
    public Vector3 moment;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("cog")]
public class CogCommand
{
    public Vector3 cog;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("boundingbox")]
public class BoundingBoxCommand
{
    public Vector3 size;
    public Vector3 centre;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("collision_mesh")]
public class CollisionMeshCommand
{
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("nocollision")]
public class NoCollisionCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("fixed")]
public class FixedCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("crashmode_pole")]
public class CrashModePoleCommmand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("surface")]
public class SurfaceCommmand
{
    [OmsiIniComments] public string? comments;
}
#endregion

[OmsiIniCommand("new_attachment")]
public class NewAttachementCommmand
{
    // TODO: Implement this command's sub commands
    //  - attach_trans
    //  - attach_rot_x
    //  - attach_rot_y
    //  - attach_rot_z
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("maplight")]
public class MapLightCommmand
{
    public Vector3 pos;
    public Vector3 colour;
    public float radius;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("nomaplighting")]
public class NoMapLightingCommmand
{
    [OmsiIniComments] public string? comments;
}

#region Path Commands
public enum OmsiPathType
{
    Street,
    Sidewalk,
    Railroad
}

public enum OmsiPathDirection
{
    Forward,
    Reverse,
    Both
}

public enum OmsiPathBlinker
{
    None,
    Straight,
    Left,
    Right
}

public enum OmsiPathTrafficLights
{
    None,
    Railway,
    Street
}

[OmsiIniCommand("path")]
public class PathCommand
{
    public Vector3 pos;
    public float heading;
    public float radius;
    public float length;
    public float gradStart;
    public float gradEnd;
    public OmsiPathType type;
    public float width;
    public OmsiPathDirection direction;
    public OmsiPathBlinker blinker;

    public UseTrafficLightCommand? trafficLight;
    public SwitchDirCommand? switchDir;
    public CrossingProblemCommand? crossingProblem;
    public BlockPathCommand[]? blockPaths;
    public RailEnhCommand? rail;
    public ThirdRailCommand[]? thirdRails;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("path_2")]
[OmsiIniDerivedCommand<PathCommand>]
public class PathCommand2
{
    public Vector3 pos;
    public float heading;
    public float radius;
    public float length;
    public float gradStart;
    public float gradEnd;
    public OmsiPathType type;
    public float width;
    public OmsiPathDirection direction;
    public OmsiPathBlinker blinker;
    public float cantStart;
    public float cantEnd;

    public UseTrafficLightCommand? trafficLight;
    public SwitchDirCommand? switchDir;
    public CrossingProblemCommand? crossingProblem;
    public BlockPathCommand[]? blockPaths;
    public RailEnhCommand? rail;
    public ThirdRailCommand[]? thirdRails;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("use_traffic_light")]
public class UseTrafficLightCommand
{
    public int index;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("switchdir")]
public class SwitchDirCommand
{
    public byte direction;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("crossingproblem")]
public class CrossingProblemCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("blockpath")]
public class BlockPathCommand
{
    public int blockedPath;
    public byte param;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("rail_enh")]
public class RailEnhCommand
{
    public float length;
    public int thrusts;
    public float trackDistortionWavelength;
    public float trackDistortionAmplitude;
    public float trackDistortionPot;
    public float trackDistortionWavelengthZ;
    public float trackDistortionAmplitudeZ;
    public float trackDistortionPotZ;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("third_rail")]
public class ThirdRailCommand
{
    public float posX;
    public float posZ;
    public byte flags;
    public float voltage;
    public float frequency;
    public float sigA;

    [OmsiIniComments] public string? comments;
}

#region Traffic Light Commands
[OmsiIniCommand("traffic_lights_group")]
public class TrafficLightsGroupCommand
{
    public float cycleDuration;

    public TrafficLightDefCommand[]? lights;
    public TrafficLightStopCommand[]? stops;
    public TrafficLightJumpCommand[]? jumps;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("traffic_light")]
public struct TrafficLightDefCommand
{
    public string name;
    //public float actPhase = 12;
    //public byte blockingPhase = 3;
    //public float request;
    //public uint requestFrame;

    public PhaseCommand[]? phases;
    public ApproachDistCommand? approachDist;

    [OmsiIniComments] public string? comments;
}

public enum OmsiTrafficLightPhase : byte
{
    Red,
    Red_1,
    Red_2,
    Yellow_RG,
    Yellow_RG_1,
    Yellow_RG_2,
    Green,
    Green_1,
    Green_2,
    Yellow_GR,
    Yellow_GR_1,
    Yellow_GR_2,
    Off,
}

[OmsiIniCommand("phase")]
public struct PhaseCommand
{
    public OmsiTrafficLightPhase phase;
    public float time;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("approachdist")]
public struct ApproachDistCommand
{
    public float approachDist;// = 10;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("traffic_light_stop")]
public struct TrafficLightStopCommand
{
    public int trafficLight;
    public float time;
    public bool ifNoApproach;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("traffic_light_jump")]
public struct TrafficLightJumpCommand
{
    public int trafficLight;
    public float time;
    public bool ifNoApproach;
    public float jumpTime;

    [OmsiIniComments] public string? comments;
}
#endregion
#endregion

[OmsiIniCommand("triggerbox_new")]
public class TriggerBoxNewCommand
{
    public Vector3 size;
    public Vector3 centre;

    public TriggerBoxSetReverbCommand? setReverb;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("triggerbox_setreverb")]
public class TriggerBoxSetReverbCommand
{
    public float time;

    [OmsiIniComments] public string? comments;
}
