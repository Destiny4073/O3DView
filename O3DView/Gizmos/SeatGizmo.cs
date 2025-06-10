using O3DParse;
using O3DParse.Ini;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.Gizmos;

internal class SeatGizmo : BillboardGizmo<SeatCommand>
{
    private readonly Program program;
    private bool wasSelected = false;

    private static SimpleMesh playerStandingMesh;
    private static SimpleMesh playerSittingMesh;

    public SeatGizmo(string? texturePath, string? basePath, string? name, SeatCommand? metadata, Program program, Renderer renderer)
        : base(texturePath, basePath, name, metadata, renderer)
    {
        this.program = program;
    }

    static SeatGizmo()
    {
        var standingO3D = O3DParser.ReadO3D(Program.GetResourcePath("player_standing.o3d"));
        var sittingO3D = O3DParser.ReadO3D(Program.GetResourcePath("player_sitting.o3d"));

        playerStandingMesh = new SimpleMesh()
        {
            vertices = standingO3D.vertices,
            triangles = standingO3D.triangles,
            materials = standingO3D.materials
        };

        playerSittingMesh = new SimpleMesh()
        {
            vertices = sittingO3D.vertices,
            triangles = sittingO3D.triangles,
            materials = sittingO3D.materials
        };
    }

    protected override SimpleMesh GenerateMesh(bool fast)
    {
        if (program == null || program.SelectedObject != this)
            return base.GenerateMesh(fast);

        if ((metadata?.height ?? 0) == 0)
            return playerStandingMesh;
        else
            return playerSittingMesh;
    }

    internal override void OnRender(Camera camera, Renderer renderer, Program program)
    {
        bool selected = program.IsSelected(this);
        if (!selected)
            base.OnRender(camera, renderer, program);

        if (selected != wasSelected)
        {
            wasSelected = selected;
            Update(true);

            if (selected)
            {
                var rot = (metadata?.rotation ?? 0) / 180 * 3.141592f;
                transform.EulerRot = new(0, rot, 0);
                transform.Scale = 1;
            } else
            {
                transform.Scale = 0.05f;
            }
        }

        DrawInteriorLightsRefGizmos(program, metadata?.illuminationInterior);
    }
}
