using O3DParse;
using O3DParse.Ini;
using Silk.NET.OpenGL;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace O3DView.Gizmos;

internal abstract class SplineMesh : GizmoMesh
{
    private static readonly ConcurrentDictionary<string, OmsiSpline> cachedSplineProfiles = [];

    protected OmsiSpline? profile;

    public OmsiSpline? Profile => profile;

    public SplineMesh(Renderer renderer) : base(renderer)
    {
        name = $"Spline_{meshNo}";
        //Update();
    }

    public SplineMesh(SplineMesh other) : base(other)
    {
        profile = other.profile;
        //Update(false);
    }

    /*public override SceneObject Clone()
    {
        return new CabinPathGizmo(this);
    }*/

    public static OmsiSpline GetSplineProfile(string path, string? basePath)
    {
        if (cachedSplineProfiles.TryGetValue(path, out var profile))
            return profile;

        string fullPath = basePath == null ? path : Path.Combine(basePath, path);
        using var stream = File.OpenRead(fullPath);
        var splCfg = OmsiIniSerializer.DeserializeIniFile<OmsiSpline>(stream);
        profile = splCfg;
        cachedSplineProfiles.TryAdd(path, profile);
        return profile;
    }
}

internal class PathMesh : SplineMesh
{
    private readonly OmsiComplMapObj mapObj;
    private int pathIndex;
    private readonly string splineDir;

    public ref PathCommand2 PathData
    {
        get
        {
            if (mapObj.paths == null || mapObj.paths.Length <= pathIndex)
                return ref Unsafe.NullRef<PathCommand2>();
            return ref mapObj.paths[pathIndex];
        }
    }

    public int PathIndex { get => pathIndex; set => pathIndex = value; }
    public OmsiComplMapObj MapObj => mapObj;

    public PathMesh(Renderer renderer, string splineDir, OmsiComplMapObj mapObj, int pathIndex, string path) : base(renderer)
    {
        this.mapObj = mapObj;
        this.pathIndex = pathIndex;
        this.splineDir = splineDir;
        this.matPath = Path.Combine(splineDir, "invis_street.sli");

        name = $"Path_{pathIndex}";
        
        Update();
    }

    public PathMesh(PathMesh other) : base(other)
    {
        mapObj = other.mapObj;
        pathIndex = other.pathIndex;
        splineDir = other.splineDir;
        matPath = other.matPath;

        Update();
    }

    public override SceneObject Clone()
    {
        return new PathMesh(this);
    }

    protected override O3DParse.SimpleMesh GenerateMesh(bool fast)
    {
        ref var path = ref PathData;
        transform.Pos = new(-path.pos.X, path.pos.Z, path.pos.Y);
        transform.Rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -path.heading * MathF.PI/180);
        profile = GetSplineProfile(path.type == OmsiPathType.Sidewalk ? "invis_sidewalk.sli" : "invis_street.sli", splineDir);
        var pathSpline = new Path2Spline(path, new SplineProfileInst(profile));
        var mesh = SplineMeshGenerator.GenerateSplineMesh(pathSpline, Program.Settings.SplineTessDist, Program.Settings.SplineCurveSag);
        if (profile.texture != null)
        {
            mesh.materials = [..profile.texture.Select(x => new O3DMaterial(O3DVec4.One, O3DVec3.Zero, O3DVec3.Zero, 1, x.path))];
        }
        
        return mesh;
    }
}

internal class GenericSplineMesh : SplineMesh
{
    private readonly GenericSplineCommand spline;
    private readonly string splineDir;

    public GenericSplineCommand SplineData => spline;

    public GenericSplineMesh(Renderer renderer, string splineDir, GenericSplineCommand spline, string path) : base(renderer)
    {
        this.spline = spline;
        this.splineDir = splineDir;
        this.matPath = Path.Combine(splineDir, spline.path ?? "invis_street.sli");
        profile = GetProfile();
        name = $"Spline_{spline.idCode}";

        Update();
    }

    public GenericSplineMesh(GenericSplineMesh other) : base(other)
    {
        spline = other.spline;
        splineDir = other.splineDir;
        matPath = other.matPath;
        profile = other.profile;

        Update();
    }

    public override SceneObject Clone()
    {
        return new GenericSplineMesh(this);
    }

    private OmsiSpline GetProfile() => GetSplineProfile(spline.path ?? "invis_street.sli", splineDir);

    protected override O3DParse.SimpleMesh GenerateMesh(bool fast)
    {
        transform.Pos = new(-spline.pos.X, spline.pos.Y, spline.pos.Z);
        transform.Rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -spline.heading * MathF.PI / 180);
        profile = GetProfile();
        var splineInst = new GenericSpline2Spline(spline, new SplineProfileInst(profile));
        var mesh = SplineMeshGenerator.GenerateSplineMesh(splineInst, Program.Settings.SplineTessDist, Program.Settings.SplineCurveSag);
        if (profile.texture != null)
        {
            mesh.materials = [.. profile.texture.Select(x => new O3DMaterial(O3DVec4.One, O3DVec3.Zero, O3DVec3.Zero, 1, x.path))];
        }

        return mesh;
    }
}

public readonly struct SplineProfileInst(OmsiSpline spline) : ISplineProfiles
{
    public IEnumerable<ProfileCommand> ProfileParts => spline.profiles ?? throw new NullReferenceException();
}

public readonly struct Path2Spline(PathCommand2 path, ISplineProfiles profiles) : ISplineInstance
{
    public ISplineProfiles Profile => profiles;
    public readonly float Length => path.length;
    public readonly float Radius => path.radius;
    public readonly float StartGrad => path.gradStart;
    public readonly float EndGrad => path.gradEnd;
    public readonly float CantStart => path.cantStart;
    public readonly float CantEnd => path.cantEnd;
    public readonly float SkewStart => 0;
    public readonly float SkewEnd => 0;
    public readonly bool UseDeltaHeight => false;
    public readonly float DeltaHeight => 0;
    public readonly bool Mirror => false;
    public readonly float Width => path.width/3;
}

public readonly struct GenericSpline2Spline(GenericSplineCommand spline, ISplineProfiles profiles) : ISplineInstance
{
    public ISplineProfiles Profile => profiles;
    public float Length => spline.length;
    public float Radius => spline.radius;
    public float StartGrad => spline.gradStart;
    public float EndGrad => spline.gradEnd;
    public float CantStart => spline.cantStart;
    public float CantEnd => spline.cantEnd;
    public float SkewStart => spline.skewStart;
    public float SkewEnd => spline.skewEnd;
    public bool UseDeltaHeight => spline.useDeltaHeight;
    public float DeltaHeight => spline.deltaHeight;
    public bool Mirror => spline.mirror;
    public float Width => 1;
}
