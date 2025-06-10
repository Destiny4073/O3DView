using O3DParse;
using O3DParse.Ini;
using O3DView.Gizmos;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace O3DView;

public class O3DMesh : Mesh
{
    public O3DFile O3D => o3d;
    public MeshCommand? CFGMesh => meshCommand;
    public string O3DPath => o3dPath;

    private readonly GL gl;
    private readonly O3DFile o3d;
    private readonly MeshCommand? meshCommand;
    private readonly string o3dPath;
    private readonly string? cfgPath;

    // WARNING: Not thread-safe. Because this class calls OpenGL methods, it's already not thread-safe, so this is fine...
    private static readonly Dictionary<string, int> matls = [];

    public O3DMesh(Renderer renderer, O3DFile o3d, string o3dPath, MeshCommand? cfgMesh = null, string? cfgPath = null)
    {
        this.gl = renderer.GL;
        this.o3d = o3d;
        this.o3dPath = o3dPath;
        this.cfgPath = cfgPath;
        this.meshCommand = cfgMesh;
        meshNo++;
        name = Path.GetFileNameWithoutExtension(o3dPath);
        if (renderer.MainShader == null)
            throw new Exception("Main shader was not initialised, object cannot be created!");
        CreateFromO3D(renderer.MainShader, renderer.scene);
        ComputeAABB();
    }

    public O3DMesh(O3DMesh other) : base(other)
    {
        gl = other.gl;
        o3d = other.o3d;
        o3dPath = other.o3dPath;
        cfgPath = other.cfgPath;
        meshCommand = other.meshCommand;

        meshNo++;

        //CreateFromO3D(this.shader, scene);
        //ComputeAABB();
    }

    public override SceneObject Clone()
    {
        return new O3DMesh(this);
    }

    private void CreateFromO3D(Shader shader, SceneGraph scene)
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
        BufferObject<float> vbo = new(gl, verts, BufferTargetARB.ArrayBuffer);

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

        bool hasMatlCommands = meshCommand != null && (meshCommand?.matls?.Length ?? 0) > 0;
        if (hasMatlCommands)
        {
            matls.Clear();
            matls.EnsureCapacity(o3d.materials.Length);
            /*matls.EnsureCapacity(meshCommand?.matlCommands.Length ?? 0);
            foreach(var mat in meshCommand?.matlCommands!)
            {
                matls.Add(mat.path, mat);
            }*/
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
            BufferObject<uint> ibo = new(gl, tris.AsSpan()[..dstInd], BufferTargetARB.ElementArrayBuffer);
            VertexArrayObject<float, uint> vao = new(gl, vbo, ibo);
            vao.Bind();
            vao.VertexAttributePointer(0, 3, VertexAttribType.Float, 8, 0);
            vao.VertexAttributePointer(1, 3, VertexAttribType.Float, 8, 3);
            vao.VertexAttributePointer(2, 2, VertexAttribType.Float, 8, 6);

            if (shader == null)
                throw new Exception("Couldn't create O3D material! Main shader is not initialised!");

            MatlCommand? matl = null;
            if (hasMatlCommands)
            {
                string tex = o3d.materials[m].textureName;
                if (matls.TryGetValue(tex, out int ind))
                {
                    ind++;
                    matls[tex] = ind;
                }
                else
                {
                    ind = 0;
                    matls.Add(tex, ind);
                }

                matl = meshCommand?.matls?.FirstOrDefault(x => ind == x.index && string.Equals(x.path, tex, StringComparison.OrdinalIgnoreCase));
            }

            var mat = Material.CreateFromO3D(o3d.materials[m], o3dPath, shader, gl, scene, matl, cfgPath, meshCommand?.isShadow != null);

            submeshes.Add(new()
            {
                vao = vao,
                mat = mat,
                vertCount = (uint)dstInd//o3d.vertices.Length
            });
            vao.Unbind();
            ArrayPool<O3DVert>.Shared.Return(vertsCpy);
        }
    }

    private void ComputeAABB()
    {
        // Compute the AABB
        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
        foreach (O3DVert v in o3d.vertices)
        {
            Vector3 pos = v.pos;
            pos.X = -pos.X;
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }
        meshBounds = Bounds.FromMinMax(min, max);
        ComputeBounds();
    }

    internal override void OnRender(Camera camera, Renderer renderer, Program program)
    {
        base.OnRender(camera, renderer, program);
        DrawInteriorLightsRefGizmos(program, meshCommand?.illuminationInterior);
    }
}
