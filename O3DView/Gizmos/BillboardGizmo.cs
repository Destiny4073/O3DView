using O3DParse.Ini;
using O3DParse;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.Gizmos;

internal interface IBillboardGizmo
{
    public object? Metadata { get; }
    public void OnModify(object? metadata, bool deleted);
}

internal class BillboardGizmo<T> : GizmoMesh, IBillboardGizmo
    where T : class
{
    private string? texturePath;
    private string? basePath;

    private static O3DVert[] billboardVerts;
    private static O3DTri[] billboardTris;

    public T? metadata;
    public event Action<T?, bool>? OnModified;

    public object? Metadata => metadata;

    public string? TexturePath
    {
        get => texturePath;
        set
        {
            texturePath = value;
            Update(false);
        }
    }

    public string? BasePath
    {
        get => basePath;
        set
        {
            basePath = value;
            Update(false);
        }
    }

    static BillboardGizmo()
    {
        billboardVerts = new O3DVert[4];
        billboardTris = new O3DTri[2];
        billboardVerts[0] = new(new(-1, -1, 2), new(0, 0, 1), new(1, 1));
        billboardVerts[1] = new(new(1, 1, 2), new(0, 0, 1), new(0, 0));
        billboardVerts[2] = new(new(-1, 1, 2), new(0, 0, 1), new(1, 0));
        billboardVerts[3] = new(new(1, -1, 2), new(0, 0, 1), new(0, 1));

        billboardTris[0] = new(0, 1, 2, 0);
        billboardTris[1] = new(0, 3, 1, 0);
    }

    public BillboardGizmo(string? name, string? texturePath, string? basePath, T? metadata, Renderer renderer) : base(renderer)
    {
        this.texturePath = texturePath;
        this.basePath = basePath;
        this.metadata = metadata;
        this.name = name ?? "Billboard Gizmo";
        this.transform.Scale = 0.05f;
        this.polygonOffset = -2;
        Update(false);
    }

    public BillboardGizmo(BillboardGizmo<T> other) : base(other)
    {
        this.texturePath = other.texturePath;
        this.basePath = other.basePath;
        this.metadata = other.metadata;
        transform.Pos = other.transform.Pos;
        transform.Scale = other.transform.Scale;
        this.polygonOffset = other.polygonOffset;
        Update(false);
    }

    public override SceneObject Clone()
    {
        return new BillboardGizmo<T>(this);
    }

    protected override SimpleMesh GenerateMesh(bool fast)
    {
        SimpleMesh mesh = new()
        {
            vertices = billboardVerts,
            triangles = billboardTris,
            materials = [new(new(1f, 1f, 1f, 1), O3DVec3.Zero, O3DVec3.Zero, 0, texturePath ?? string.Empty)]
        };
        matPath = basePath;
        return mesh;
    }

    internal override void OnRender(Camera camera, Renderer renderer, Program program)
    {
        base.OnRender(camera, renderer, program);

        // Face camera
        transform.Rot = Quaternion.Conjugate(Quaternion.CreateFromRotationMatrix(camera.View));
    }

    // Called by the inspector window
    public void OnModify(object? metadata, bool deleted)
    {
        OnModified?.Invoke(metadata as T, deleted);
    }
}
