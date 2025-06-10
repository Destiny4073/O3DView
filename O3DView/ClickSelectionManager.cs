using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;

namespace O3DView;

internal class ClickSelectionManager
{
    private readonly ImGuiIOPtr imGuiIO;
    private readonly IKeyboard? keyboard;
    private readonly IMouse? mouse;
    private readonly IWindow window;
    private readonly Program program;
    private readonly IEnumerable<SceneObject> sceneObjects;
    private readonly Camera camera;
    private readonly Stopwatch sw = new();

    private SceneObject? hovered;
    private Ray cameraRay;
    private Vector2 mouseStart;
    private bool lastMousePress = false;
    private Vector2 lastMousePos;
    private float lastClickDepth;

    public SceneObject? Hovered => hovered;
    public Ray CameraRay => cameraRay;

    public ClickSelectionManager(IMouse? mouse, IKeyboard? keyboard, IWindow window, Program program, Camera camera)
    {
        imGuiIO = ImGui.GetIO();
        this.mouse = mouse;
        this.keyboard = keyboard;
        this.window = window;
        this.program = program;
        sceneObjects = program?.SceneGraph?.SceneObjects ?? throw new ArgumentNullException(nameof(program.SceneGraph.SceneObjects));
        this.camera = camera;
    }

    public void Update()
    {
        if (imGuiIO.WantCaptureMouse || program.SceneGraph == null)
        {
            hovered = null;
            return;
        }

#if DEBUG
        //sw.Restart();
#endif
        var ray = CreateCameraRay();

        //program.GizmoManager?.DrawBounds(new Bounds(Vector3.Zero, Vector3.One));
        //for (int i = 0; i < 10; i++)
        //    program.GizmoManager?.DrawBounds(new Bounds(ray.start + ray.dir * i, Vector3.One*.1f));
        //program.GizmoManager?.DrawRay(ray);

        if (FindIntersectionRecursive(sceneObjects, ray, out var depth, lastClickDepth))
        {
            program.GizmoManager?.DrawBounds(((Mesh)hovered!).Bounds);
        } 
        else
        {
            hovered = null;
            lastClickDepth = 0;
            //program.debugString = "";
        }
#if DEBUG
        //program.debugString = $"Picking took {sw.Elapsed.TotalMilliseconds:F3} ms";
#endif

        if (mouse != null)
        {
            bool pressed = mouse.IsButtonPressed(MouseButton.Left);
            if (pressed && !lastMousePress)
            {
                mouseStart = mouse.Position;
            }
            else if (!pressed && lastMousePress)
            {
                var delta = mouse.Position - mouseStart;
                if (delta == Vector2.Zero)
                {
                    if (lastMousePos == mouse.Position)
                        lastClickDepth = depth;
                    program.Select(hovered);
                }
            }
            lastMousePress = pressed;

            if (lastMousePos != mouse.Position)
                lastClickDepth = 0;
            lastMousePos = mouse.Position;
        }
    }

    private bool FindIntersectionRecursive(IEnumerable<SceneObject> objects, in Ray ray, out float dist, float minDist)
    {
        dist = float.MaxValue;
        foreach (var obj in objects)
        {
            if (obj is Mesh mesh && mesh.visible)
            {
                if (mesh.Bounds.Intersects(ray))
                {
                    if (mesh.Intersects(ray, out float d))
                    {
                        if (d > minDist && d < dist)
                        {
                            dist = d;
                            hovered = obj;
                        }
                    }
                }
            }

            if (obj.children != null)
            {
                if (FindIntersectionRecursive(obj.children, ray, out float d1, minDist))
                {
                    dist = Math.Min(dist, d1);
                    //if (d1 > minDist && d1 < dist)
                    //    dist = d1;
                }
            }
        }
        if (dist != float.MaxValue)
            return true;

        return false;
    }

    const float DEG_2_RAD = MathF.PI / 180;

    private Ray CreateCameraRay()
    {
        var size = window.FramebufferSize;
        var mousePos = (mouse?.Position ?? Vector2.Zero) / new Vector2(size.X*.5f, size.Y*.5f) + new Vector2(-1, -1);
        float aspect = size.X / (float)size.Y;

        float verticalAngle = camera.fieldOfView * DEG_2_RAD * 0.25f * 1.075f;

        float worldHeight = MathF.Tan(verticalAngle) * 2;
        Vector3 direction = new (
            mousePos.X * worldHeight * aspect, 
            -mousePos.Y * worldHeight, 
            -1);
        //direction = new(0, 0, -1);

        // Rotate to match camera orientation.
        direction = Vector3.TransformNormal(direction, Matrix4x4.Transpose(camera.View));
        //direction = Vector4.Transform(new Vector4(direction, 0), Matrix4x4.Transpose(camera.View)).AsVector128().AsVector3();
        direction = Vector3.Normalize(direction);
        cameraRay = new(camera.transform.Pos, direction);

        return cameraRay;
    }
}
