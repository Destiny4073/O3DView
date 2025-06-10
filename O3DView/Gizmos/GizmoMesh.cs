using O3DParse;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace O3DView.Gizmos;

internal abstract class GizmoMesh : Mesh
{
    private readonly GL gl;
    private readonly Shader shader;
    private readonly SceneGraph scene;
    private BufferObject<float>? vbo;

    protected string? matPath;
    internal float polygonOffset = -2;

    public GizmoMesh(Renderer renderer)
    {
        this.gl = renderer.GL;
        if (renderer.MainShader == null)
            throw new Exception("Main shader was not initialised, object cannot be created!");
        this.shader = renderer.MainShader;
        this.scene = renderer.scene;
        matPath = null;
        name = $"Gizmo_{meshNo++}";
    }

    public GizmoMesh(GizmoMesh other) : base(other)
    {
        gl = other.gl;
        shader = other.shader;
        scene = other.scene;
        matPath = other.matPath;
    }

    /*public override SceneObject Clone()
    {
        return new GizmoMesh(this);
    }*/

    public void Update(bool fast = false)
    {
        UpdateMesh(GenerateMesh(fast), shader, scene, fast);
        ComputeAABB();
    }

    protected abstract O3DParse.SimpleMesh GenerateMesh(bool fast);

    private void UpdateMesh(in O3DParse.SimpleMesh o3d, Shader shader, SceneGraph scene, bool fast = false)
    {
        VertexArrayObject<float, uint>.UnbindAny(gl);

        // Create a new vertex buffer
        // Axis conversion
        var vertsCpy = ArrayPool<O3DVert>.Shared.Rent(o3d.vertices.Length);
        unsafe
        {
            fixed (O3DVert* src = o3d.vertices)
            fixed (O3DVert* dst = vertsCpy)
            {
                long size = o3d.vertices.Length * Unsafe.SizeOf<O3DVert>();
                System.Buffer.MemoryCopy(src, dst, size, size);
            }
        }
        for (int v = 0; v < o3d.vertices.Length; v++)
        {
            vertsCpy[v].pos.x = -vertsCpy[v].pos.x;
            vertsCpy[v].normal.x = -vertsCpy[v].normal.x;
        }

        var verts = MemoryMarshal.Cast<O3DVert, float>(vertsCpy.AsSpan(0, o3d.vertices.Length));
        if (vbo == null)
            vbo = new(gl, verts, BufferTargetARB.ArrayBuffer);
        else
            vbo.Update(verts, 0, true);

        nverts = o3d.vertices.Length;
        ntris = o3d.triangles.Length;

        vertices = new Vector3[o3d.vertices.Length];
        triangles = new Vector3D<int>[o3d.triangles.Length];
        for (int i = 0; i < o3d.vertices.Length; i++)
        {
            var vert = o3d.vertices[i].pos;
            vertices[i] = new(-vert.x, vert.y, vert.z);
        }
        for (int i = 0; i < o3d.triangles.Length; i++)
        {
            var tri = o3d.triangles[i];
            triangles[i] = unchecked(new((int)tri.a, (int)tri.b, (int)tri.c));
        }

        for (int m = 0; m < o3d.materials.Length; m++)
        {
            uint[] tris = ArrayPool<uint>.Shared.Rent(o3d.triangles.Length * 3);
            int dstInd = 0;
            for (int i = 0; i < o3d.triangles.Length; i++)
            {
                var tri = o3d.triangles[i];
                if (tri.mat == m)
                {
                    tris[dstInd++] = tri.c;
                    tris[dstInd++] = tri.b;
                    tris[dstInd++] = tri.a;
                }
            }

            if (submeshes.Count > m)
            {
                var sm = submeshes[m];
                sm.vertCount = (uint)dstInd;
                sm.vao.IBO?.Update(tris.AsSpan()[..dstInd], 0, true);

                submeshes[m] = sm;
            }
            else
            {
                BufferObject<uint> ibo = new(gl, tris.AsSpan()[..dstInd], BufferTargetARB.ElementArrayBuffer);
                VertexArrayObject<float, uint> vao = new(gl, vbo, ibo);
                vao.Bind();
                vao.VertexAttributePointer(0, 3, VertexAttribType.Float, 8, 0);
                vao.VertexAttributePointer(1, 3, VertexAttribType.Float, 8, 3);
                vao.VertexAttributePointer(2, 2, VertexAttribType.Float, 8, 6);

                if (shader == null)
                    throw new Exception("Couldn't create O3D material! Main shader is not initialised!");

                var mat = Material.CreateFromO3D(o3d.materials[m], matPath ?? "", shader, gl, scene);

                submeshes.Add(new()
                {
                    vao = vao,
                    mat = mat,
                    vertCount = (uint)dstInd//o3d.vertices.Length
                });
                vao.Unbind();
            }
            ArrayPool<O3DVert>.Shared.Return(vertsCpy);
        }
    }

    private void ComputeAABB()
    {
        // Compute the AABB
        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        meshBounds = Bounds.FromMinMax(min, max);
        ComputeBounds();
    }
}
