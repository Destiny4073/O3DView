using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DParse.Ini;

[Serializable]
//[OmsiIniCommand("spline")]
public class OmsiSpline
{
    //[OmsiIniCommandFile] public string? path;
    public LengthCommand? length;
    public TextureCommand[]? texture;
    public HeightProfileCommand[]? heightProfile;
    public ProfileCommand[]? profiles;
    public SplinePath2Command[]? paths;
    public HalfCantWidthCommand? halfCantWidth;
    public OnlyEditorCommand? onlyEditor;
    public TerrainHoleProfileCommand[]? terrainHoleProfiles;
}

[OmsiIniCommand("length")]
public class LengthCommand
{
    public float length;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("texture")]
public class TextureCommand
{
    public string path = string.Empty;

    public ScaleTexByLengthCommand? scaleTexByLength;
    public PatchworkChainCommand? patchworkChain;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("scaleTexByLength")]
public class ScaleTexByLengthCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("patchwork_chain")]
public class PatchworkChainCommand
{
    public float length;
    public string? transitions;
    public string? weights;
    public string? invertable;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("heightprofile")]
public class HeightProfileCommand
{
    public float start;
    public float end;
    public float height1;
    public float height2;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("profile")]
public class ProfileCommand
{
    public int textureIndex;

    public ProfilePointCommand[] points = [];

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("profilepnt")]
public class ProfilePointCommand
{
    public Vector2 pos;
    public Vector2 texcoord;

    [OmsiIniComments] public string? comments;
}

public enum OmsiSplinePathType : byte
{
    AIRoadTraffic,
    AIPeople,
    Rail
}

[OmsiIniCommand("path_2")]
[OmsiIniDerivedCommand<SplinePathCommand>]
public class SplinePath2Command
{
    public OmsiSplinePathType pathType;
    public Vector2 offset;
    public float width;
    public OmsiPathDirection direction;
    public float cant;

    public RailEnhCommand? rail;
    public ThirdRailCommand[]? thirdRails;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("path")]
public class SplinePathCommand
{
    public OmsiSplinePathType pathType;
    public Vector2 offset;
    public float width;
    public OmsiPathDirection direction;

    public RailEnhCommand? rail;
    public ThirdRailCommand[]? thirdRails;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("halfcantwidth")]
public class HalfCantWidthCommand
{
    public float width;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("terrainholeprofile")]
public class TerrainHoleProfileCommand
{
    public TerrainHoleProfilePointCommand[]? points;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("terrainholeprofilepnt")]
public class TerrainHoleProfilePointCommand
{
    public Vector3 point;

    [OmsiIniComments] public string? comments;
}
