using O3DParse.Ini;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.Gizmos;

internal class PathPointGizmo<T> : BillboardGizmo<T>
    where T : class
{
    private readonly OmsiPaths paths;
    private readonly Func<T, OmsiPaths, Vector3> pointSelector;

    public PathPointGizmo(OmsiPaths paths, Func<T, OmsiPaths, Vector3> pointSelector, string? texturePath, 
        string? basePath, string? name, T? metadata, Renderer renderer)
        : base(texturePath, basePath, name, metadata, renderer)
    {
        this.paths = paths;
        this.pointSelector = pointSelector;
    }

    internal override void OnRender(Camera camera, Renderer renderer, Program program)
    {
        base.OnRender(camera, renderer, program);

        transform.Pos = pointSelector(metadata!, paths);
    }
}
