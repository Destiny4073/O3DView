using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DParse.Ini;

[Serializable]
[OmsiIniCommand("map")]
public class OmsiMapTile
{
    [OmsiIniCommandFile] public string? path;

    public VersionCommand? version; // 14 is the latest
    public TerrainCommand? terrain;
    public VariableTerrainLightmapCommand? variableTerrainLightmap;
    public VariableTerrainCommand? variableTerrain;
    public WaterCommand? water;
    public ObjectCommand[]? sceneObjects;
    public AttachObjectCommand[]? attachObjects;
    public GenericSplineCommand[]? splines;
    public GenericSplineAttachement[]? splineAttachements;
    //public SplineTerrainAlign2Command? splineTerrainAlign;
}

[OmsiIniCommand("version")]
public class VersionCommand
{
    public int version;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("terrain")]
public class TerrainCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("variable_terrainlightmap")]
public class VariableTerrainLightmapCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("variable_terrain")]
public class VariableTerrainCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("water")]
public class WaterCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("scenObj")]
public class SceneObjectCommand
{
    public byte complexity; // TODO: This field should only be read if version > 8
    public string path = string.Empty;
    // public int obj; // version < 2
    public int idCode; // version >= 6
    public float posX;
    public float posZ;
    public float posY;
    public float heading;
    public float pitch; // version >= 12
    public float bank; // version >= 12
    public string[] parameters = []; // version > 3

    public VarparentCommand? varparent;
    public SplineTerrainAlignCommand? splineTerrainAlign;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("object")]
[OmsiIniDerivedCommand<SceneObjectCommand>]
public class ObjectCommand
{
    public byte complexity; // TODO: This field should only be read if version > 8
    public string path = string.Empty;
    // public int obj; // version < 2
    public int idCode; // version >= 6
    public float posX;
    public float posZ;
    public float posY;
    public float heading;
    public float pitch; // version >= 12
    public float bank; // version >= 12
    public string[] parameters = []; // version > 3

    public VarparentCommand? varparent;
    public SplineTerrainAlignCommand? splineTerrainAlign;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("attachObject")]
public class AttachObjectCommand
{
    public byte complexity; // TODO: This field should only be read if version > 8
    public string path = string.Empty;
    // public int obj; // version < 2
    public int idCode; // version >= 6
    public int attachObjId;
    public int attachedInstances; // must be 0 in current version
    public int attachPoint;
    public float heading; // version > 7
    public float pitch; // version >= 12
    public float bank; // version >= 12
    public string[] parameters = []; // version > 3

    public VarparentCommand? varparent;
    public SplineTerrainAlignCommand? splineTerrainAlign;

    [OmsiIniComments] public string? comments;
}

public class OmsiSplineSerializer : IOmsiIniCommandCustomSerializer
{
    public void Deserialize(ref object target, Type targetType, string command, IniFileReader reader, ref int lineNumber, object? parent)
    {
        if (parent is not OmsiMapTile paths)
            throw new OmsiIniSerializationException($"OmsiSplineSerializer expected the [spline_*] command to belong to an {nameof(OmsiMapTile)}!");

        GenericSplineCommand spline;
        bool deltaHeight = false;
        if (targetType == typeof(SplineCommand))
            spline = new SplineCommand();
        else if (targetType == typeof(SplineHCommand))
        {
            spline = new SplineHCommand();
            deltaHeight = true;
        }
        else if (targetType == typeof(SplineAbschnittCommand))
            spline = new SplineAbschnittCommand();
        else
            throw new OmsiIniSerializationException();
        spline.useDeltaHeight = deltaHeight;

        int version = 99;
        if (parent is OmsiMapTile map)
            version = map.version?.version ?? version;

        if (version >= 9)
            spline.complexity = IOmsiIniCommandCustomSerializer.ReadByte(reader, ref lineNumber);
        spline.path = IOmsiIniCommandCustomSerializer.ReadString(reader, ref lineNumber);
        // There's some version specific logic here I haven't understood
        spline.idCode = IOmsiIniCommandCustomSerializer.ReadInt(reader, ref lineNumber);
        spline.prevIDCode = IOmsiIniCommandCustomSerializer.ReadInt(reader, ref lineNumber);
        if (version >= 11)
            spline.nextIDCode = IOmsiIniCommandCustomSerializer.ReadInt(reader, ref lineNumber);
        spline.pos = new(
            IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber),
            IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber),
            IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber)
        );
        spline.heading = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        spline.length = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        spline.radius = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        spline.gradStart = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        spline.gradEnd = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        if (spline.useDeltaHeight)
            spline.deltaHeight = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        if (version >= 5)
        {
            spline.cantStart = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
            spline.cantEnd = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        }
        if (version >= 14)
        {
            spline.skewStart = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
            spline.skewEnd = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        }
        if (version >= 11)
            spline.startZ = IOmsiIniCommandCustomSerializer.ReadFloat(reader, ref lineNumber);
        if (version >= 7)
            spline.mirror = IOmsiIniCommandCustomSerializer.ReadBool("mirror", reader, ref lineNumber);
        target = spline;
    }

    public void Serialize(object? obj, Type objType, string? commandName, TextWriter writer, object? parent)
    {
        int version = 99;
        if (parent is OmsiMapTile map)
            version = map.version?.version ?? version;

        if (obj is not GenericSplineCommand spline)
            throw new OmsiIniSerializationException("Expected obj to be a GenericSplineCommand");

        if (spline.useDeltaHeight)
            writer.WriteLine("[spline_h]");
        else
            writer.WriteLine("[spline]");
        if (version >= 9)
            writer.WriteLine(spline.complexity);
        writer.WriteLine(spline.path);
        writer.WriteLine(spline.idCode);
        writer.WriteLine(spline.prevIDCode);
        if (version >= 6)
            writer.WriteLine(spline.nextIDCode);
        writer.WriteLine(spline.pos.X);
        writer.WriteLine(spline.pos.Y);
        writer.WriteLine(spline.pos.Z);
        writer.WriteLine(spline.heading);
        writer.WriteLine(spline.length);
        writer.WriteLine(spline.radius);
        writer.WriteLine(spline.gradStart);
        writer.WriteLine(spline.gradEnd);
        if (spline.useDeltaHeight)
            writer.WriteLine(spline.deltaHeight);
        if (version >= 5)
        {
            writer.WriteLine(spline.cantStart);
            writer.WriteLine(spline.cantEnd);
        }
        if (version >= 14)
        {
            writer.WriteLine(spline.skewStart);
            writer.WriteLine(spline.skewEnd);
        }
        if (version >= 11)
            writer.WriteLine(spline.startZ);
        if (version >= 7)
            writer.WriteLine(spline.mirror ? "mirror" : "false");
    }
}

[OmsiIniCommand("__spline")]
[OmsiIniCommandSerializer<OmsiSplineSerializer>]
[OmsiIniDerivedCommand<SplineCommand>]
[OmsiIniDerivedCommand<SplineHCommand>]
[OmsiIniDerivedCommand<SplineAbschnittCommand>]
public abstract class GenericSplineCommand
{
    public byte complexity; // version > 8
    public string? path;
    // public int obj; // version < 2
    public int idCode; // version >= 6
    //public int prevIndex; // version <= 12  not sure exactly how they are different, but they are
    //public int nextIndex; // version <= 12  computed automatically
    public int prevIDCode; // version > 12
    public int nextIDCode; // version > 10
    public Vector3 pos;
    public float heading;
    public float length;
    public float radius;
    public float gradStart;
    public float gradEnd;
    public bool useDeltaHeight;
    public float deltaHeight; // Only spline h
    public float cantStart; // version >=5
    public float cantEnd; // version >= 5
    public float skewStart; // version >= 14
    public float skewEnd; // version >= 14
    public float startZ; // version > 10
    public bool mirror; // version > 6

    public SplineTerrainAlign2Command? splineTerrainAlign;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("spline_h")]
public class SplineHCommand : GenericSplineCommand { }

[OmsiIniCommand("spline")]
public class SplineCommand : GenericSplineCommand { }

[OmsiIniCommand("splineAbschnitt")]
public class SplineAbschnittCommand : GenericSplineCommand { }

[OmsiIniCommand("__splineAttachement")]
[OmsiIniDerivedCommand<SplineAttachementRepeaterCommand>]
[OmsiIniDerivedCommand<SplineAttachementCommand>]
public abstract class GenericSplineAttachement
{
    public byte complexity; // version > 8
    public int tile;
    public int repeaterOffset;
    public string? path;
    public int idCode;
    public int splineId;
    public Vector3 pos;
    public float heading;
    public float pitch; // version >= 12
    public float bank; // version >= 12
    public float spacing;
    public float distance;
    public bool tangential; // version >= 11
    public string[]? parameters; // version >= 2

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("splineAttachement_repeater")]
public class SplineAttachementRepeaterCommand : GenericSplineAttachement
{

}

[OmsiIniCommand("splineAttachement")]
public class SplineAttachementCommand : GenericSplineAttachement
{
    [OmsiIniSkip]
    public new int tile;
    [OmsiIniSkip]
    public new int repeaterOffset;
}

[OmsiIniCommand("varparent")]
public class VarparentCommand
{
    //public int tile;
    //public int obj;
    public int idCode; // version >= 6

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("spline_terrain_align_2")]
[OmsiIniDerivedCommand<SplineTerrainAlignCommand>]
public class SplineTerrainAlign2Command
{
    public byte alignTerrain;// = 1;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("spline_terrain_align")]
public class SplineTerrainAlignCommand
{
    [OmsiIniComments] public string? comments;
}

// TODO: [rule] [kill_rule] [selobject] [selspline] [delete] [typ][relabel]
