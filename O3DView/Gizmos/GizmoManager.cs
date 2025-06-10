using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace O3DView;

public class GizmoManager
{
    private GizmoVert[] gizmoVertices;
    private int vertIndex;
    private readonly BufferObject<float> vbo;
    private readonly VertexArrayObject<float, int> vao;
    private readonly Shader shader;
    private readonly Program program;
    private readonly object vertLock = new();

    private int framesSinceErrorMsg = 0;

    public Vector4 Colour { get; set; } = new(1, 0, .5f, 1);
    public bool ShowGizmos { get; set; } = true;

    public GizmoManager(Program program, GL gl) 
    {
        this.program = program;

        vbo = new(gl, [], BufferTargetARB.ArrayBuffer);
        vao = new(gl, vbo, null);
        vao.Bind();
        vao.VertexAttributePointer(0, 3, VertexAttribType.Float, 7, 0);
        vao.VertexAttributePointer(1, 4, VertexAttribType.Float, 7, 3);
        vao.Unbind();

        shader = new(gl, "unlit_vert.glsl", "unlit_frag.glsl");

        gizmoVertices = new GizmoVert[2048];
    }

    internal void Render(GL gl, in Matrix4x4 viewMat, in Matrix4x4 projMat)
    {
        if (ShowGizmos == false)
        {
            vertIndex = 0;
            return;
        }

        gl.DepthFunc(DepthFunction.Lequal);
        gl.DepthMask(true);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
        gl.Disable(EnableCap.CullFace);
        gl.PolygonOffset(0, 2);

        shader.Use();
        shader.SetUniform("uModel", Matrix4x4.Identity);
        shader.SetUniform("uView", viewMat);
        shader.SetUniform("uProj", projMat);

        lock (vertLock)
        {
            vbo.Update(MemoryMarshal.Cast<GizmoVert, float>(gizmoVertices.AsSpan()[..vertIndex]), 0, true);
            vao.Bind();
            gl.DrawArrays(PrimitiveType.Lines, 0, (uint)vertIndex);
            vao.Unbind();

            vertIndex = 0;
            framesSinceErrorMsg++;
        }
        gl.PolygonOffset(0, 0);
    }

    private void OutOfGizmoVerts()
    {
        if (framesSinceErrorMsg > 30)
        {
            Program.Log($"Couldn't draw all gizmos! Only {gizmoVertices.Length} gizmo vertices are reserved per frame!", 
                nameof(GizmoManager), GUI.GUIController.Severity.Error);
            framesSinceErrorMsg = 0;
        }
    }

    public void DrawLine(Vector3 start, Vector3 end, Vector4? colour = null)
    {
        if (vertIndex + 2 > gizmoVertices.Length)
        {
            OutOfGizmoVerts();
            return;
        }

        lock (vertLock)
        {
            colour ??= Colour;
            gizmoVertices[vertIndex++] = new() { pos = start, colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = end, colour = colour.Value };
        }
    }

    public void DrawRay(Ray ray, float length = 10, Vector4? colour = null)
    {
        if (vertIndex + 2 > gizmoVertices.Length)
        {
            OutOfGizmoVerts();
            return;
        }

        lock (vertLock)
        {
            colour ??= Colour;
            gizmoVertices[vertIndex++] = new() { pos = ray.start, colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = ray.start + ray.dir * length, colour = colour.Value };
        }
    }

    public void DrawBounds(Bounds bounds, Vector4? colour = null)
    {
        if (vertIndex + 24 > gizmoVertices.Length)
        {
            OutOfGizmoVerts();
            return;
        }

        lock (vertLock)
        {
            var (min, max) = bounds.ToMinMax();

            colour ??= Colour;
            // Base
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, min.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, min.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, min.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, min.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, min.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, min.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, min.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, min.Y, min.Z), colour = colour.Value };
            // Verticals
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, min.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, max.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, min.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, max.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, min.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, max.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, min.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, max.Y, max.Z), colour = colour.Value };
            // Top
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, max.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, max.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, max.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, max.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, max.Y, max.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, max.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(max.X, max.Y, min.Z), colour = colour.Value };
            gizmoVertices[vertIndex++] = new() { pos = new Vector3(min.X, max.Y, min.Z), colour = colour.Value };
        }
    }
}

[StructLayout(LayoutKind.Explicit)]
internal struct GizmoVert
{
    [FieldOffset(0x0)] public Vector3 pos;
    [FieldOffset(0xc)] public Vector4 colour;
}
