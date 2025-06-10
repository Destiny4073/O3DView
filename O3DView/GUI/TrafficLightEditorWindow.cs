using ImGuiNET;
using O3DParse.Ini;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.GUI;

public class TrafficLightEditorWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;
    private readonly string[] trafficPhases;

    private bool isOpen;
    private WindowSnapEdge windowSnap;
    private int selectedStop = -1;
    private int selectedJump = -1;
    private int selectedTrafficLight;
    private int selectedTrafficPhase;
    private string[]? trafficLightsNames;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new TrafficLightEditorWindow(guiController, program);

        guiController.RegisterWindow(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.Tools,
            name = "Traffic Light Editor",
            shortcut = "",
            action = (open) => ret.isOpen = open,
        });

        return ret;
    }

    private TrafficLightEditorWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;

        trafficPhases = Enum.GetNames(typeof(OmsiTrafficLightPhase));
    }

    public bool DrawWindow()
    {
        if (!isOpen)
            return false;

        if (ImGui.Begin("Traffic Light Editor", ref isOpen))
        {
            ImGui.SetWindowSize(new(700, 600), ImGuiCond.Once);
            controller.SnapWindowToEdge(ref windowSnap);

            ImGui.TextWrapped("This window allows you to edit traffic light groups on SCO files. Click on one of the coloured phases in the chart to edit it.");
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1, 1), $"Selected: {program.SelectedObject?.name ?? "NONE"}");
            ImGui.Spacing();
            //ImGui.Text("Traffic Lights:");
            ImGui.CollapsingHeader("Traffic Lights", ImGuiTreeNodeFlags.DefaultOpen);
            //ImGui.Spacing();

            if (program.SelectedObject is CFGObject cfgObj
                && cfgObj.cfg is OmsiComplMapObj cfg)
            {
                if (cfg.trafficLightsGroup != null)
                {
                    TrafficLightDefCommand[] trafficLights = cfg.trafficLightsGroup.lights ?? [];
                    TrafficLightJumpCommand[] jumps = cfg.trafficLightsGroup.jumps ?? [];
                    TrafficLightStopCommand[] stops = cfg.trafficLightsGroup.stops ?? [];

                    if (trafficLightsNames == null
                        || trafficLightsNames.Length != trafficLights.Length
                        || !trafficLightsNames.SequenceEqual(trafficLights.Select(x=>x.name)))
                        trafficLightsNames = trafficLights.Select(x => x.name).ToArray();

                    // Draw the traffic light phase table
                    if (ImGui.BeginTable("Phase Table", 3, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                        //int maxCols = 0;
                        //foreach (var light in lights.lights ?? [])
                        //    maxCols = Math.Max(maxCols, light.phases?.Length ?? 0);
                        ImGui.TableSetupColumn("Phases", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableHeadersRow();
                        var tableStartPos = ImGui.GetCursorScreenPos();

                        DrawTrafficLightsTable(cfg.trafficLightsGroup, trafficLights);
                        DrawStopsJumpsTable(cfg.trafficLightsGroup, jumps, stops, tableStartPos);

                        ImGui.EndTable();
                    }

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Create: ");
                    ImGui.SameLine();
                    if (ImGui.Button("Traffic Light"))
                        cfg.trafficLightsGroup.lights = [.. trafficLights, new() { name = "New Light", phases = [new()] }];
                    ImGui.SameLine();
                    if (ImGui.Button("Phase Stop"))
                        cfg.trafficLightsGroup.stops = [.. stops, new() { time = 5, trafficLight = 0 }];
                    ImGui.SameLine();
                    if (ImGui.Button("Phase Jump"))
                        cfg.trafficLightsGroup.jumps = [.. jumps, new() { time = 5, jumpTime = 10, trafficLight = 0 }];

                    ImGui.SetNextItemWidth(100);
                    ImGui.DragFloat("Cycle Time (s)", ref cfg.trafficLightsGroup.cycleDuration, 0.2f, 0.1f, 3600);

                    ImGui.Spacing();
                    ImGui.Spacing();

                    ImGui.Indent();
                    DrawSelectedTrafficLight(cfg.trafficLightsGroup);
                    DrawSelectedStopJump(cfg.trafficLightsGroup);
                    ImGui.Unindent();
                }
                else
                {
                    if (ImGui.Button("Create"))
                    {
                        cfg.trafficLightsGroup = new TrafficLightsGroupCommand()
                        {
                            cycleDuration = 60,
                            lights = [new () { 
                                name = "New Traffic Light", 
                                phases = [ 
                                    new() { time = 15, phase = OmsiTrafficLightPhase.Red },
                                    new() { time = 5, phase = OmsiTrafficLightPhase.Yellow_RG },
                                    new() { time = 20, phase = OmsiTrafficLightPhase.Green },
                                    new() { time = 5, phase = OmsiTrafficLightPhase.Yellow_GR },
                                    new() { time = 15, phase = OmsiTrafficLightPhase.Red },
                                ] 
                            }]
                        };
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("Please select an object with traffic lights."); ;
            }
        }
        ImGui.End();
        return isOpen;
    }

    private void DrawTrafficLightsTable(TrafficLightsGroupCommand lightsGroup, TrafficLightDefCommand[] trafficLights)
    {
        for (int i = 0; i < trafficLights.Length; i++)
        {
            ImGui.PushID(i);
            ref TrafficLightDefCommand light = ref trafficLights[i];
            ImGui.TableNextColumn();
            if (ImGui.Button(" X "))
            {
                var lst = trafficLights.ToList();
                lst.RemoveAt(i);
                lightsGroup.lights = [.. lst];
                break;
            }
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (ImGui.Selectable(light.name, i == selectedTrafficLight))
            {
                selectedTrafficLight = i;
                selectedTrafficPhase = 0;
                selectedJump = -1;
                selectedStop = -1;
            }

            ImGui.TableNextColumn();
            var phases = light.phases;
            if (phases != null && phases.Length > 0)
            {
                var drawList = ImGui.GetWindowDrawList();
                var rowStart = ImGui.GetCursorScreenPos();// + ImGui.GetCursorScreenPos();
                var rowSize = new Vector2(ImGui.GetColumnWidth(), ImGui.GetFrameHeight());
                float currX = 0;
                float currTime = 0;
                for (int j = 0; j < phases.Length; j++)
                {
                    ImGui.PushID(j);
                    PhaseCommand phase = phases[j];

                    float time = phase.time;
                    float startTime = currTime;
                    currTime += time;
                    float width = time / lightsGroup.cycleDuration * rowSize.X;
                    if (j == phases.Length - 1)
                        width = rowSize.X - currX;
                    var start = rowStart + new Vector2(currX, 0);
                    var end = rowStart + new Vector2(currX + width, rowSize.Y);
                    currX += width;
                    bool hover = ImGui.IsMouseHoveringRect(start, end);
                    bool sel = (i == selectedTrafficLight && j == selectedTrafficPhase);
                    uint colour = ImGui.GetColorU32(PhaseToColour(phase.phase, hover || sel ? 1 : 0.7f));

                    drawList.AddRectFilled(start, end, colour, 0, ImDrawFlags.RoundCornersNone);

                    if (hover)
                    {
                        if(ImGui.BeginTooltip())
                        {
                            ImGui.Text(light.name);
                            ImGui.Text($"Start: {startTime:F2} s End: {currTime:F2} s Duration: {time:F2} s");
                            ImGui.Text($"Phase: {phase.phase}");
                            ImGui.EndTooltip();
                        }
                    }

                    if (ImGui.InvisibleButton("PhaseButton", new Vector2(width, rowSize.Y)))
                    {
                        selectedTrafficLight = i;
                        selectedTrafficPhase = j;
                        selectedJump = -1;
                        selectedStop = -1;
                    }
                    ImGui.SameLine(0, 0);
                    ImGui.PopID();
                }
            }
            ImGui.PopID();
        }
    }

    private void DrawStopsJumpsTable(TrafficLightsGroupCommand lightsGroup, TrafficLightJumpCommand[] jumps, TrafficLightStopCommand[] stops, Vector2 tableStartPos)
    {
        var drawList = ImGui.GetWindowDrawList();
        // Stops
        for (int i = 0; i < stops.Length; i++)
        {
            ImGui.PushID(i);
            ref TrafficLightStopCommand stop = ref stops[i];
            ImGui.TableNextColumn();
            if (ImGui.Button(" X "))
            {
                var lst = stops.ToList();
                lst.RemoveAt(i);
                lightsGroup.stops = [.. lst];
                ImGui.PopID();
                break;
            }
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            bool sel = ImGui.Selectable($"Stop {i}", i == selectedStop);

            ImGui.TableNextColumn();
            var startPos = ImGui.GetCursorScreenPos();// + ImGui.GetCursorScreenPos();
            var size = new Vector2(ImGui.GetColumnWidth()-1, ImGui.GetFrameHeight());
            uint colour = 0xff4050ff;
            float x = (stop.time / lightsGroup.cycleDuration) * size.X + startPos.X;
            float y0 = startPos.Y + size.Y / 2;
            float y1 = tableStartPos.Y + 1;
            var startPnt = new Vector2(x, y0);
            drawList.AddLine(startPnt, new Vector2(x, y1), colour, 1);
            //drawList.AddNgon(new Vector2(x, y0), 10, colour, 8);
            drawList.AddRectFilled(startPnt - new Vector2(4, 4), startPnt + new Vector2(4, 4), colour, 0, ImDrawFlags.RoundCornersNone);

            sel |= ImGui.InvisibleButton("StopSelButton", size);

            if (sel)
            {
                selectedStop = i;
                selectedTrafficLight = -1;
                selectedJump = -1;
            }

            ImGui.PopID();
        }
        ImGui.PushID(999);
        // Jumps
        for (int i = 0; i < jumps.Length; i++)
        {
            ImGui.PushID(i);
            ref TrafficLightJumpCommand jump = ref jumps[i];
            ImGui.TableNextColumn();
            if (ImGui.Button(" X "))
            {
                var lst = jumps.ToList();
                lst.RemoveAt(i);
                lightsGroup.jumps = [.. lst];
                ImGui.PopID();
                break;
            }
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            bool sel = ImGui.Selectable($"Jump {i}", i == selectedJump);

            ImGui.TableNextColumn();
            var startPos = ImGui.GetCursorScreenPos();// + ImGui.GetCursorScreenPos();
            var size = new Vector2(ImGui.GetColumnWidth()-1, ImGui.GetFrameHeight());
            uint colour = 0xffff4050;
            float x0 = (jump.time / lightsGroup.cycleDuration) * size.X + startPos.X;
            float x1 = (jump.jumpTime / lightsGroup.cycleDuration) * size.X + startPos.X;
            float y0 = startPos.Y + size.Y;
            float y1 = tableStartPos.Y + 1;
            float y2 = startPos.Y + size.Y / 2;
            drawList.AddLine(new Vector2(x0, y0), new Vector2(x0, y1), colour, 1);
            drawList.AddLine(new Vector2(x1, y0), new Vector2(x1, y1), colour, 1);
            // Draw arrow
            drawList.AddLine(new Vector2(x0, y2), new Vector2(x1, y2), colour, 1);
            drawList.AddLine(new Vector2(x1, y2), new Vector2(x1, y2) + new Vector2(jump.time > jump.jumpTime ? 5 : -5, 5), colour, 1);
            drawList.AddLine(new Vector2(x1, y2), new Vector2(x1, y2) + new Vector2(jump.time > jump.jumpTime ? 5 : -5, -5), colour, 1);

            sel |= ImGui.InvisibleButton("StopSelButton", size);

            if (sel)
            {
                selectedJump = i;
                selectedTrafficLight = -1;
                selectedStop = -1;
            }

            ImGui.PopID();
        }
        ImGui.PopID();
    }

    private void DrawSelectedStopJump(TrafficLightsGroupCommand lightsGroup)
    {
        if (lightsGroup.stops != null && lightsGroup.stops.Length > selectedStop && selectedStop >= 0)
        {
            ImGui.PushID("StopEdit");
            ref var stop = ref lightsGroup.stops[selectedStop];
            if (ImGui.CollapsingHeader($"Stop {selectedStop}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PushItemWidth(100);
                ImGui.Combo("Traffic Light", ref stop.trafficLight, trafficLightsNames, trafficLightsNames?.Length ?? 0);
                ImGui.DragFloat("Time", ref stop.time, 0.1f, 0, lightsGroup.cycleDuration);
                bool approach = !stop.ifNoApproach;
                if (ImGui.Checkbox("Trigger on Approach", ref approach))
                    stop.ifNoApproach = !approach;

                if (ImGui.Button("Convert to Jump"))
                {
                    lightsGroup.jumps = [.. lightsGroup.jumps ?? [], new() {
                        time = stop.time,
                        jumpTime = stop.time + 10,
                        trafficLight = stop.trafficLight,
                        ifNoApproach = stop.ifNoApproach,
                        comments = stop.comments
                    }];

                    var lst = lightsGroup.stops.ToList();
                    lst.RemoveAt(selectedStop);
                    lightsGroup.stops = [.. lst];

                    selectedJump = lightsGroup.jumps.Length - 1;
                    selectedStop = -1;
                }

                ImGui.PopItemWidth();
            }
        } else if (lightsGroup.jumps != null && lightsGroup.jumps.Length > selectedJump && selectedJump >= 0)
        {
            ImGui.PushID("JumpEdit");
            ref var jump = ref lightsGroup.jumps[selectedJump];
            if (ImGui.CollapsingHeader($"Jump {selectedJump}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PushItemWidth(100);
                ImGui.Combo("Traffic Light", ref jump.trafficLight, trafficLightsNames, trafficLightsNames?.Length ?? 0);
                ImGui.DragFloat("Time", ref jump.time, 0.1f, 0, lightsGroup.cycleDuration);
                ImGui.DragFloat("Jump Time", ref jump.jumpTime, 0.1f, 0, lightsGroup.cycleDuration);
                bool approach = !jump.ifNoApproach;
                if (ImGui.Checkbox("Trigger on Approach", ref approach))
                    jump.ifNoApproach = !approach;

                if (ImGui.Button("Convert to Stop"))
                {
                    lightsGroup.stops = [.. lightsGroup.stops ?? [], new() {
                        time = jump.time,
                        trafficLight = jump.trafficLight,
                        ifNoApproach = jump.ifNoApproach,
                        comments = jump.comments
                    }];

                    var lst = lightsGroup.jumps.ToList();
                    lst.RemoveAt(selectedJump);
                    lightsGroup.jumps = [.. lst];

                    selectedJump = -1;
                    selectedStop = lightsGroup.stops.Length - 1;
                }
                ImGui.PopItemWidth();
            }
            ImGui.PopID();
        }
    }

    private void DrawSelectedTrafficLight(TrafficLightsGroupCommand lightsGroup)
    {
        if (lightsGroup.lights != null && lightsGroup.lights.Length > selectedTrafficLight && selectedTrafficLight >= 0)
        {
            ref var light = ref lightsGroup.lights[selectedTrafficLight];
            if (ImGui.CollapsingHeader($"Traffic Light '{light.name}'", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PushItemWidth(100);
                ImGui.InputText("Name", ref light.name, 255);

                bool approachExists = light.approachDist != null;
                if (ImGui.Checkbox("", ref approachExists))
                {
                    if (approachExists)
                        light.approachDist = new ApproachDistCommand();
                    else
                        light.approachDist = null;
                }
                ImGui.SameLine();
                if (!approachExists)
                    ImGui.BeginDisabled();
                float dist = light.approachDist?.approachDist ?? 0;
                if (ImGui.DragFloat("Approach Distance", ref dist, 0.1f, 0, 1000))
                    light.approachDist = new ApproachDistCommand() { approachDist = dist };

                if (!approachExists)
                    ImGui.EndDisabled();
                ImGui.PopItemWidth();

                ImGui.Spacing();
                if (light.phases != null && light.phases.Length > selectedTrafficPhase)
                {
                    ref PhaseCommand phase = ref light.phases[selectedTrafficPhase];
                    ImGui.Indent();

                    bool phaseHeader = ImGui.CollapsingHeader($"Phase {selectedTrafficPhase + 1}/{light.phases.Length}", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
                    //ImGui.AlignTextToFramePadding();
                    //ImGui.Text($"Phase {selectedTrafficPhase+1}/{light.phases.Length}");
                    ImGui.SameLine();
                    if (ImGui.Button(" < "))
                        selectedTrafficPhase = Math.Max(0, selectedTrafficPhase - 1);
                    ImGui.SameLine();
                    if (ImGui.Button(" > "))
                        selectedTrafficPhase = Math.Min(light.phases.Length - 1, selectedTrafficPhase + 1);
                    ImGui.SameLine();
                    if (ImGui.Button("Insert Left"))
                    {
                        var list = light.phases.ToList();
                        list.Insert(selectedTrafficPhase, new PhaseCommand() { phase = phase.phase, time = phase.time });
                        light.phases = [.. list];
                        phase = ref light.phases[selectedTrafficPhase];
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Delete") && light.phases.Length > 1)
                    {
                        var list = light.phases.ToList();
                        list.RemoveAt(selectedTrafficPhase);
                        light.phases = [.. list];
                        phase = ref light.phases[Math.Max(selectedTrafficPhase - 1, 0)];
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Insert Right"))
                    {
                        var list = light.phases.ToList();
                        list.Insert(selectedTrafficPhase + 1, new PhaseCommand() { phase = phase.phase, time = phase.time });
                        light.phases = [.. list];
                        phase = ref light.phases[selectedTrafficPhase];
                    }

                    if (phaseHeader)
                    {
                        ImGui.PushItemWidth(100);

                        float cumCycleTime = 0;
                        for (int i = 0; i < selectedTrafficPhase; i++)
                            cumCycleTime += light.phases[i].time;

                        float maxTime;
                        if (selectedTrafficPhase < light.phases.Length - 2)
                            maxTime = phase.time + light.phases[selectedTrafficPhase + 1].time;
                        else
                            maxTime = lightsGroup.cycleDuration - cumCycleTime;

                        ImGui.LabelText("Start Time (s)", $"{cumCycleTime:F2}");
                        if (ImGui.DragFloat("Duration (s)", ref phase.time, 0.1f, 0.001f, maxTime))
                        {
                            if (selectedTrafficPhase < light.phases.Length - 2)
                            {
                                light.phases[selectedTrafficPhase + 1].time = maxTime - phase.time;
                            }
                        }
                        var phaseType = (int)phase.phase;
                        if (ImGui.Combo("Phase", ref phaseType, trafficPhases, trafficPhases.Length))
                            phase.phase = (OmsiTrafficLightPhase)phaseType;

                        ImGui.PopItemWidth();
                    }
                    ImGui.Unindent();
                }
            }
        }
    }

    private static Vector4 PhaseToColour(OmsiTrafficLightPhase phase, float alpha = 1)
    {
        switch (phase)
        {
            case OmsiTrafficLightPhase.Red:
            case OmsiTrafficLightPhase.Red_1:
            case OmsiTrafficLightPhase.Red_2:
                return new Vector4(0.7f, 0, 0, alpha);
            case OmsiTrafficLightPhase.Yellow_RG:
            case OmsiTrafficLightPhase.Yellow_RG_1:
            case OmsiTrafficLightPhase.Yellow_RG_2:
                return new Vector4(1, 0.5f, 0, alpha);
            case OmsiTrafficLightPhase.Green:
            case OmsiTrafficLightPhase.Green_1:
            case OmsiTrafficLightPhase.Green_2:
                return new Vector4(0, 0.9f, 0.1f, alpha);
            case OmsiTrafficLightPhase.Yellow_GR:
            case OmsiTrafficLightPhase.Yellow_GR_1:
            case OmsiTrafficLightPhase.Yellow_GR_2:
                return new Vector4(1, 1, 0, alpha);
            case OmsiTrafficLightPhase.Off:
            default:
                return new Vector4(0, 0, 0, alpha);
        }
    }
}
