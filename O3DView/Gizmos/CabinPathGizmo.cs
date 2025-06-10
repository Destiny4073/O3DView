using O3DParse.Ini;
using O3DParse;
using Silk.NET.OpenGL;
using System.Numerics;

namespace O3DView.Gizmos;

internal class CabinPathGizmo : GizmoMesh
{
    private readonly OmsiPaths paths;

    public OmsiPaths Paths => paths;

    public CabinPathGizmo(OmsiPaths paths, Renderer renderer) : base(renderer)
    {
        this.paths = paths;
        name = $"Cabin Paths";
        Update(false);
    }

    public CabinPathGizmo(CabinPathGizmo other) : base(other)
    {
        paths = other.paths;
        Update(false);
    }

    public override SceneObject Clone()
    {
        return new CabinPathGizmo(this);
    }

    protected override SimpleMesh GenerateMesh(bool fast)
    {
        if (paths.links == null || paths.points == null)
            return new SimpleMesh() { vertices = [], materials = [], triangles = [] };

        O3DVert[] verts = new O3DVert[paths.links.Length * 6];
        O3DTri[] tris = new O3DTri[paths.links.Length * 2];
        uint vInd = 0;
        uint tInd = 0;
        Vector3 up = Vector3.UnitY;
        float width = 0.1f;
        foreach(var link in paths.links)
        {
            var a = paths.points[link.a].position.XZY();
            var b = paths.points[link.b].position.XZY();
            var fwd = b - a;

            var left = Vector3.Normalize(Vector3.Cross(up, fwd)) * width;
            var right = -left;

            var al = a + left;
            var ar = a + right;
            var bl = b + left;
            var br = b + right;

            verts[vInd++] = new(ar, up, O3DVec2.Zero);
            verts[vInd++] = new(al, up, O3DVec2.Zero);
            verts[vInd++] = new(bl, up, O3DVec2.Zero);

            verts[vInd++] = new(ar, up, O3DVec2.Zero);
            verts[vInd++] = new(bl, up, O3DVec2.Zero);
            verts[vInd++] = new(br, up, O3DVec2.Zero);

            tris[tInd++] = new(vInd - 4, vInd - 5, vInd - 6, 0);
            tris[tInd++] = new(vInd - 1, vInd - 2, vInd - 3, 0);
        }

        SimpleMesh mesh = new()
        {
            vertices = verts,
            triangles = tris,
            materials = [new(new(1f, 0.4f, 0.1f, 1), O3DVec3.Zero, O3DVec3.Zero, 0, string.Empty)]
        };
        return mesh;
    }
}
