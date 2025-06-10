using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Microsoft.Win32;

namespace O3DView.GUI;

public partial class GUIController : IDisposable
{
    private readonly ImGuiController controller;
    private readonly GL gl;
    private readonly IInputContext inputContext;
    private readonly IWindow window;
    private readonly Program program;
    private readonly SynchronizationContext syncContext;
    private readonly GCHandle imguiIniPathHandle;

    private readonly HashSet<(IGUIWindow wnd, GUIMenuItem? menu)> windows = [];
    private readonly Dictionary<MenuItemCategory, List<GUIMenuItem>> menuItems;
    private readonly Dictionary<Key, (Action<IKeyboard> action, IGUIWindow window)> kbShortcuts = [];
    private readonly Dictionary<GUIMenuItem, bool> menuItemState = [];

    private readonly List<(Severity sev, string caption, string msg)> errorLog = [];

    private readonly float windowSnapRange = 10;

    private readonly object logFileLock = new();
    private ImFontPtr? fontPtr;
    private float loadingProgress = -1;
    private string loadingMessage = "";
    private bool errorMessageOpen = false;
    private bool errorLogOpen = false;
    private string? errorMessage = null;
    private string? errorCaption = null;
#if DEBUG
    private bool imGUIDemoOpen = false;
#endif
    private bool shouldRepositionWindows = true;
    private WindowSnapEdge logWindowSnap;

    public IInputContext InputContext => inputContext;
    public IWindow Window => window;

    public GUIController(IWindow window, IInputContext inputContext, GL gl, Program program)
    {
        this.window = window;
        this.gl = gl;
        this.inputContext = inputContext;
        ConfigureImGUI(window, inputContext, gl, program, out imguiIniPathHandle, out controller);

        this.program = program;
        syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        menuItems = new()
        {
            { MenuItemCategory.File, [] },
            { MenuItemCategory.Edit, [] },
            { MenuItemCategory.View, [] },
            { MenuItemCategory.Tools, [] },
            { MenuItemCategory.Help, [] },
        };

        var kb = inputContext.Keyboards.FirstOrDefault();
        if (kb != null)
        {
            kb.KeyDown += Kb_KeyDown;
        }

        window.FramebufferResize += Window_Resize;

        int windows = ConstructAllWindows(Assembly.GetExecutingAssembly());

        LogMessage("O3DView GUI Initialised!");
        LogMessage($"Loaded {windows} tool windows.");
    }

    private void ConfigureImGUI(IWindow window, IInputContext inputContext, GL gl, Program program,
        out GCHandle imguiIniPathHandle, out ImGuiController controller)
    {
        string fontPath = "guiFont.ttf";
        string fontPathFull = Program.GetResourcePath(fontPath);

        unsafe
        {
            string imguiIniPath = Path.Combine(program.appdataPath, "imgui.ini");
            var iniPathHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(imguiIniPath), GCHandleType.Pinned);
            imguiIniPathHandle = iniPathHandle;

            if (!File.Exists(fontPathFull))
                Program.Log($"Couldn't find font file '{fontPathFull}'!", "Font Failure", Severity.Warning);

            //var fontConfig = new ImGuiFontConfig(fontPathFull, 14);

            controller = new ImGuiController(
                gl,
                window,
                inputContext,
                //fontConfig,
                () =>
                {
                    var io = ImGui.GetIO();
                    io.NativePtr->IniFilename = (byte*)iniPathHandle.AddrOfPinnedObject();

                    try
                    {
                        ImFontPtr font;
                        var fontConfigPtr = ImGuiNative.ImFontConfig_ImFontConfig();
                        fontConfigPtr->RasterizerMultiply = 1;
                        fontConfigPtr->GlyphOffset = new Vector2(0);
                        fontConfigPtr->OversampleH = 3;
                        fontConfigPtr->OversampleV = 2;
                        //fontConfigPtr->FontDataOwnedByAtlas = 0;
                        fontConfigPtr->PixelSnapH = 1;

                        var bytes = File.ReadAllBytes(fontPathFull);
                        var ttfHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

                        font = io.Fonts.AddFontFromMemoryTTF(ttfHandle.AddrOfPinnedObject(), 15, 15, fontConfigPtr);
                        io.Fonts.Build();
                        ttfHandle.Free();

                        if (font.NativePtr == null || !font.IsLoaded())
                            Program.Log("Failed to load font file!", "Font Failure", Severity.Warning);

                        RecreateFontDeviceTexture();

                        io.FontGlobalScale = 1.0f;
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"Failed to load font file!\n{ex.Message}", "Font Failure", Severity.Warning);
                    }

                    io.Fonts.Build();

                    ImGui.GetStyle().FrameRounding = 2;
                    ImGui.GetStyle().WindowRounding = 2;
                }
            );

            var fonts = ImGui.GetIO().Fonts;
            ImFontPtr? font = (fonts.Fonts.Size > 0) ? fonts.Fonts[fonts.Fonts.Size - 1] : null;
            if (!(font?.IsLoaded() ?? false))
                Program.Log("Failed to load font file!", "Font Failure", Severity.Warning);
            //ImGui.PushFont(font);
            fontPtr = font;
        }
    }

    private void RecreateFontDeviceTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr out_pixels, out int out_width, out int out_height, out int _);

        gl.GetInteger(GLEnum.TextureBinding2D, out var data);

        uint handle = gl.CreateTexture(TextureTarget.Texture2D);
        gl.BindTexture(TextureTarget.Texture2D, handle);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        unsafe
        {
            ref readonly byte bytes = ref Unsafe.AsRef<byte>((void*)out_pixels);
            gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, (uint)out_width, (uint)out_height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, in bytes);
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        io.Fonts.SetTexID((nint)handle);
        gl.BindTexture(GLEnum.Texture2D, (uint)data);
    }

    private void Kb_KeyDown(IKeyboard kb, Key key, int arg3)
    {
        if (ImGui.IsAnyItemActive())
            return;

        switch (key)
        {
            case Key.O:
                if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.Ctrl))
                    OpenFile();
                break;
            case Key.F:
                if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.None))
                    Focus();
                break;
            case Key.S:
                break;
            case Key.Delete:
                if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.None))
                    DeleteObj();
                break;
            case Key.Escape:
                program.FileLoader?.CancelLoading();
                break;
            case Key.D:
                if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.Ctrl))
                    program.DuplicateObj();
                break;
            case Key.F5:
                if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.Ctrl))
                    TextureManager.ReloadTextures();
                break;
            default:
                break;
        }

        if (kbShortcuts.TryGetValue(key, out var shortcut))
        {
            shortcut.action.Invoke(kb);
        }
    }

    public void Render(double delta)
    {
        controller.Update((float)delta);
        if (fontPtr != null)
            ImGui.PushFont(fontPtr.Value);

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Open", "CTRL+O"))
                    OpenFile();

                foreach (var item in menuItems[MenuItemCategory.File])
                    DrawMenuItem(item);

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Delete", "Delete"))
                    DeleteObj();
                if (ImGui.MenuItem("Delete All"))
                    DeleteObj(true);
                if (ImGui.MenuItem("Duplicate", "CTRL+D"))
                    program.DuplicateObj();
                if (ImGui.MenuItem("Create Light"))
                    CreateLight();
                bool cacheMats = Program.Settings.ShouldCacheMaterials;
                if (ImGui.MenuItem("Should Cache Materials", "", ref cacheMats))
                    Program.Settings.ShouldCacheMaterials = cacheMats;

                foreach (var item in menuItems[MenuItemCategory.Edit])
                    DrawMenuItem(item);

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("View"))
            {
                if (ImGui.MenuItem("Focus", "F"))
                    Focus();
                bool highlight = Program.Settings.SelectionHighlight;
                if (ImGui.MenuItem("Highlight Selection", "", ref highlight))
                    Program.Settings.SelectionHighlight = highlight;
                ImGui.MenuItem("Fullbright", "", ref program.fullbright);
                ImGui.MenuItem("Wireframe", "", ref program.wireframe);
                ImGui.MenuItem("Show Interior Light References", "", ref program.showLightReferences);
                bool gizmos = program.GizmoManager!.ShowGizmos;
                if (ImGui.MenuItem("Gizmos", "", ref gizmos))
                    program.GizmoManager!.ShowGizmos = gizmos;
                if (ImGui.MenuItem("Reload Textures", "CTRL+F5"))
                    TextureManager.ReloadTextures();
                if (ImGui.MenuItem("Log Window", "", ref errorLogOpen))
                    errorLogOpen = true;

                foreach (var item in menuItems[MenuItemCategory.View])
                    DrawMenuItem(item);

#if DEBUG
                if (ImGui.MenuItem("ImGUI Debug"))
                    imGUIDemoOpen = true;
#endif
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Tools"))
            {
                foreach (var item in menuItems[MenuItemCategory.Tools])
                    DrawMenuItem(item);

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Help"))
            {
                if (ImGui.MenuItem("Register File Association"))
                    RegisterFileAssociation(true);
                if (ImGui.MenuItem("Unregister File Association"))
                    RegisterFileAssociation(false);

                foreach (var item in menuItems[MenuItemCategory.Help])
                    DrawMenuItem(item);

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }

#if DEBUG
        if (imGUIDemoOpen)
            ImGui.ShowDemoWindow(ref imGUIDemoOpen);
#endif
        DrawLogWindow();

        foreach (var window in windows)
        {
            bool open = window.wnd.DrawWindow();
            if (window.menu.HasValue)
                menuItemState[window.menu.Value] = open;
        }

        if (loadingProgress >= 0)
            DrawLoadingBar();

        if (errorMessageOpen)
        {
            ImGui.OpenPopup("ErrorMessage");
            errorMessageOpen = false;
        }
        if (ImGui.BeginPopup("ErrorMessage"))
        {
            ImGui.Text(errorCaption);
            ImGui.TextColored(new System.Numerics.Vector4(1, .2f, .2f, 1), errorMessage);
            ImGui.EndPopup();
        }

        shouldRepositionWindows = false;

        if (fontPtr != null)
            ImGui.PopFont();

        controller.Render();

        void DrawMenuItem(GUIMenuItem item)
        {
            if (item.toggle)
            {
                bool state = menuItemState[item];
                if (ImGui.MenuItem(item.name, item.shortcut, ref state))
                {
                    menuItemState[item] = state;
                    item.action?.Invoke(state);
                }
            }
            else
            {
                if (ImGui.MenuItem(item.name, item.shortcut))
                    item.action?.Invoke(true);
            }
        }
    }

    #region Window Registration
    public enum MenuItemCategory
    {
        File,
        Edit,
        View,
        Tools,
        Help
    }

    public record struct GUIMenuItem
    {
        public IGUIWindow window;
        public Action<bool>? action;
        public MenuItemCategory category;
        public string name;
        public string shortcut;
        public bool toggle;
    }

    public void RegisterWindow(IGUIWindow window, GUIMenuItem? menuItem = null)
    {
        try
        {
            windows.Add((window, menuItem));
            if (menuItem != null)
                RegisterMenuItem(window, menuItem.Value);
        }
        catch (Exception ex)
        {
            LogMessage($"Couldn't register window:\n{ex}", "Window Registration", Severity.Error);
        }
    }

    public void RegisterMenuItem(IGUIWindow window, GUIMenuItem menuItem)
    {
        try
        {
            if (!menuItems.TryGetValue(menuItem.category, out var items))
            {
                items = [];
                menuItems.Add(menuItem.category, items);
            }
            menuItemState.Add(menuItem, false);
            items.Add(menuItem);
        }
        catch (Exception ex)
        {
            LogMessage($"Couldn't menu item:\n{ex}", "Window Registration", Severity.Error);
        }
    }

    public void SetMenuItemState(GUIMenuItem menuItem, bool state)
    {
        if (menuItemState.ContainsKey(menuItem))
            menuItemState[menuItem] = state;
    }

    public void RegisterKeyboardShortcut(Key key, IGUIWindow window, Action<IKeyboard> action)
    {
        try
        {
            kbShortcuts.Add(key, (action, window));
        }
        catch (Exception ex)
        {
            LogMessage($"Couldn't keyboard shortcut:\n{ex}", "Window Registration", Severity.Error);
        }
    }

    public int ConstructAllWindows(Assembly assembly)
    {
        int registered = 0;
        var winddowTypes = assembly.ExportedTypes.Where(x => x.IsClass && typeof(IGUIWindow).IsAssignableFrom(x));
        foreach (var windowType in winddowTypes)
        {
            var meth = windowType.GetMethod(nameof(IGUIWindow.Construct), BindingFlags.Static | BindingFlags.Public);
            if (meth == null)
                continue;

            meth.Invoke(null, [this, program]);
            registered++;
        }
        return registered;
    }
    #endregion

    public void Dispose()
    {
        controller.Dispose();
        imguiIniPathHandle.Free();
    }

    private void Window_Resize(Silk.NET.Maths.Vector2D<int> size)
    {
        shouldRepositionWindows = true;
    }

    public void SnapWindowToEdge(ref WindowSnapEdge snapEdge)
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var viewSize = ImGui.GetWindowViewport().WorkSize;
        var workPos = ImGui.GetWindowViewport().WorkPos;
        pos -= workPos;

        var topLeft = Vector2.Abs(pos);
        var bottomRight = Vector2.Abs(pos + size - viewSize);
        var bottomRightNew = viewSize - size;

        if ((shouldRepositionWindows && (snapEdge & WindowSnapEdge.Left) != 0)
            || (topLeft.X != 0 && topLeft.X <= windowSnapRange))
        {
            // Left snap
            pos.X = 0;
            ImGui.SetWindowPos(pos + workPos);
            snapEdge |= WindowSnapEdge.Left;
        }
        else if ((shouldRepositionWindows && (snapEdge & WindowSnapEdge.Right) != 0)
            || (bottomRight.X != 0 && bottomRight.X <= windowSnapRange))
        {
            // Right snap
            pos.X = bottomRightNew.X;
            ImGui.SetWindowPos(pos + workPos);
            snapEdge |= WindowSnapEdge.Right;
        }
        else if (topLeft.X != 0 && bottomRight.X != 0)
        {
            // Clear any old left/right snaps if the window isn't on the edge
            snapEdge &= ~(WindowSnapEdge.Left | WindowSnapEdge.Right);
        }

        if ((shouldRepositionWindows && (snapEdge & WindowSnapEdge.Top) != 0)
            || (topLeft.Y != 0 && topLeft.Y <= windowSnapRange))
        {
            // Top snap
            pos.Y = 0;
            ImGui.SetWindowPos(pos + workPos);
            snapEdge |= WindowSnapEdge.Top;
        }
        else if ((shouldRepositionWindows && (snapEdge & WindowSnapEdge.Bottom) != 0)
            || (bottomRight.Y != 0 && bottomRight.Y <= windowSnapRange))
        {
            // Bottom snap
            pos.Y = bottomRightNew.Y;
            ImGui.SetWindowPos(pos + workPos);
            snapEdge |= WindowSnapEdge.Bottom;
        }
        else if (topLeft.Y != 0 && bottomRight.Y != 0)
        {
            // Clear any old top/bottom snaps if the window isn't on the edge
            snapEdge &= ~(WindowSnapEdge.Top | WindowSnapEdge.Bottom);
        }
    }

    public void ShowErrorPopup(string caption, string message)
    {
        LogMessage(message, caption, Severity.Error);
    }

    public void LogMessage(string message, [CallerMemberName] string? caption = null, Severity severity = Severity.Info)
    {
        errorLog.Add((severity, caption ?? severity.ToString(), message));
        if (severity >= Severity.Warning)
        {
            errorCaption = caption ?? severity.ToString();
            errorMessage = message;
            errorMessageOpen = true;
        }
        if (program.logToFile)
        {
            lock (logFileLock)
            {
                File.AppendAllText(Program.logFilePath, $"[{DateTime.Now}] [{severity}] [{caption}] {message}\n");
            }
        }
    }

    public bool IsProgressBarVisible
    {
        get
        {
            lock (progressBarLock)
            {
                return loadingProgress != -1;
            }
        }
    }
    private readonly object progressBarLock = new();

    public void ShowProgressBar(string message)
    {
        lock (progressBarLock)
        {
            loadingMessage = message;
            if (loadingProgress < 0)
                loadingProgress = 0;
        }
    }

    public void HideProgressBar()
    {
        lock (progressBarLock)
        {
            loadingProgress = -1;
        }
    }

    public void UpdateProgress(float fraction)
    {
        lock (progressBarLock)
        {
            loadingProgress = fraction;
        }
    }

    private void DrawLoadingBar()
    {
        lock (progressBarLock)
        {
            var overlayFlags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
            ImGui.SetNextWindowBgAlpha(0.4f);
            var viewportSize = ImGui.GetMainViewport().Size;
            ImGui.SetNextWindowPos(new(viewportSize.X / 2, viewportSize.Y - 8), ImGuiCond.Always, new(0.5f, 1));
            if (ImGui.Begin("Loading Bar", overlayFlags))
            {
                ImGui.Text(new(loadingMessage));
                ImGui.ProgressBar(loadingProgress, new(240, 16));
                ImGui.End();
            }
        }
    }

    private void DrawLogWindow()
    {
        if (errorLogOpen)
        {
            if (ImGui.Begin("Log", ref errorLogOpen))
            {
                ImGui.SetWindowSize(new(500, 300), ImGuiCond.Once);
                SnapWindowToEdge(ref logWindowSnap);
                if (ImGui.Button("Clear"))
                    errorLog.Clear();
                ImGui.SameLine();
                bool scrollToEnd = ImGui.Button("Scroll To End");
                if (ImGui.BeginTable("LogTab", 4, ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX | ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("ID");
                    ImGui.TableSetupColumn("Severity");
                    ImGui.TableSetupColumn("Caption");
                    ImGui.TableSetupColumn("Message");
                    ImGui.TableHeadersRow();
                    for (int i = 0; i < errorLog.Count; i++)
                    {
                        ImGui.TableNextColumn();
                        ImGui.Text($"{i}");
                        var (sev, caption, msg) = errorLog[i];
                        ImGui.TableNextColumn();
                        ImGui.Text($"{sev}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{caption}");
                        ImGui.TableNextColumn();
                        ImGui.TextColored(sev >= Severity.Warning ? new(1, .5f, .5f, 1) : new(1, 1, 1, 1), $"{msg}");
                    }
                    if (scrollToEnd)
                        ImGui.SetScrollHereY();
                    ImGui.EndTable();
                }
                ImGui.End();
            }
        }
    }

    private void OpenFile()
    {
        using OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            DefaultExt = ".o3d",
            Multiselect = true,
            Title = "Open An O3D Model File",
            Filter = "Supported files (*.o3d;*.cfg;*.sco;*.bus;*.ovh;*.obj)|*.o3d;*.cfg;*.sco;*.bus;*.ovh;*.obj" +
            "|O3D files (*.o3d)|*.o3d" +
            "|Model files (*.cfg)|*.cfg" +
            "|SCO files (*.sco)|*.sco" +
            "|BUS files (*.bus;*.ovh)|*.bus;*.ovh" +
            "|OBJ files (*.obj)|*.obj" +
            "|All files (*.*)|*.*"
        };
        var res = new DialogResult[1] { DialogResult.None };
        syncContext.Send(x =>
        {
            if (x is DialogResult[] res)
                res[0] = dialog.ShowDialog();
        }, res);
        if (res[0] == DialogResult.OK)
        {
            program?.FileLoader?.LoadFiles(dialog.FileNames);
        }
    }

    private void DeleteObj(bool all = false)
    {
        if (program.SceneGraph == null)
            return;
        if (all)
        {
            LogMessage($"Deleting {program.SceneGraph.SceneObjects.Count} meshes...");
            program.SceneGraph.Clear();
            program.SceneGraph.CreateSun();
        }
        else
        {
            program.SceneGraph.Remove(program.SelectedObject);
        }
    }

    private void Focus()
    {
        program.Focus(program.SelectedObject);
    }

    private void CreateLight()
    {
        var l = new Light()
        {
            colour = new(.5f, .5f, .5f),
            ambient = new(0, 0, 0),
            mode = LightMode.Directional,
            intensity = 1
        };
        program.SceneGraph?.Add(l);
        program.Select(l);
    }

    private readonly string regFileAssocKey = @"Software\Classes\.o3d";
    private readonly string regFileActionClass = @"Software\Classes\O3DView";
    private void RegisterFileAssociation(bool register)
    {
        if (register)
        {
            // Register
            LogMessage("Attempting to register file association...");
            try
            {
                // Save the old value
                var curr = Registry.GetValue(@"HKEY_CURRENT_USER\" + regFileAssocKey, "", null);
                if (curr != null)
                    Registry.SetValue(@"HKEY_CURRENT_USER\" + regFileAssocKey, "_old", curr, RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\" + regFileAssocKey, "", "O3DView", RegistryValueKind.String);

                string path = Path.ChangeExtension(Assembly.GetEntryAssembly()?.Location, ".exe") ?? throw new Exception("Couldn't get executable path!");
                Registry.SetValue(@"HKEY_CURRENT_USER\" + regFileActionClass + @"\shell\open\command", "",
                    $@"""{path}"" ""%1"""
                    , RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\" + regFileActionClass + @"\DefaultIcon", "",
                    $@"""{path}"",0"
                    , RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                ShowErrorPopup("Failed to register file association:", ex.Message);
            }
        }
        else
        {
            // Unregister
            LogMessage("Attempting to unregister file association...");
            try
            {
                var old = Registry.GetValue(@"HKEY_CURRENT_USER\" + regFileAssocKey, "_old", null);
                if (old != null)
                    Registry.SetValue(@"HKEY_CURRENT_USER\" + regFileAssocKey, "", old, RegistryValueKind.String);
                else
                {
                    Registry.CurrentUser.DeleteSubKey(regFileAssocKey, false);
                }
                Registry.CurrentUser.DeleteSubKeyTree(regFileActionClass, false);
            }
            catch (Exception ex)
            {
                ShowErrorPopup("Failed to unregister file association:", ex.Message);
            }
        }
        try
        {
            var CurrentUser = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.o3d", true) ?? throw new Exception("Couldn't open FileExts key!");
            CurrentUser.DeleteSubKey("UserChoice", false);
            CurrentUser.CreateSubKey("UserChoice").SetValue("ProgId", @"O3DView");
            CurrentUser.Close();
            Registry.CurrentUser.Close();
        }
        catch (Exception ex)
        {
            ShowErrorPopup("Failed to register/unregister file association:", ex.Message);
        }

        // Tell explorer the file association has been changed
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    public enum Severity
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    [LibraryImport("shell32.dll", SetLastError = true)]
    public static partial void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}

[Flags]
public enum WindowSnapEdge
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Top = 1 << 2,
    Bottom = 1 << 3,
}
