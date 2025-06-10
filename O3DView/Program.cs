using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Diagnostics.CodeAnalysis;
using O3DParse;
using O3DParse.Ini;
using System.IO;
using System.Reflection;
using Silk.NET.Core;
using StbImageSharp;
using static O3DView.GUI.GUIController;

#if ALLOW_DECRYPTION
using O3DParse.Encryption;
#endif
using O3DView.GUI;

namespace O3DView;

public class Program
{
    [NotNullIfNotNull(nameof(renderer))]
    public SceneGraph? SceneGraph => renderer?.scene;
    public GizmoManager? GizmoManager => gizmoManager;
    //public SDFManager? SDFManager => sdfManager;
    public FileLoader? FileLoader => fileLoader;
    public static Settings Settings => settingsManager.settings;
    public static GUIController? GUIController => guiController;
    public SceneObject? SelectedObject => selectedObject;
    public SceneObject? HoveredObject => selectionManager?.Hovered;
    public Ray MouseRay => selectionManager?.CameraRay ?? default;
    public float FPS => fps;
    public int RenderedVerts => renderer?.RenderedVerts ?? 0;
    public int RenderedTris => renderer?.RenderedTris ?? 0;
    public int DrawCalls => renderer?.DrawCalls ?? 0;
    public VariableManager VariableManager => variableManager;

    public bool fullbright = false;
    public bool wireframe = false;
    public bool showLightReferences = false;
    public float nightmapStrength = 0;
    public float lightmapStrength = 0;
    public OmsiCFGFile? lastLoadedCFG = null;
    public string? lastLoadedCFGPath = null;

    public string debugString = "";

    public static string logFilePath = string.Empty;
    private static readonly string logFileName = "O3DView-log.txt";
    public readonly string appdataPath;
    public static string resourcesPath = "Resources";
#if DEBUG
    public bool logToFile = true;
#else
    public bool logToFile = false;
#endif

    private static SettingsManager settingsManager = new(false);
    private SceneObject? selectedObject;
    private FileLoader? fileLoader;
    private Renderer? renderer;
    private GizmoManager? gizmoManager;
    //private SDFManager? sdfManager;
    private IInputContext? inputContext;
    private IKeyboard? mainKeyboard;
    private IMouse? mainMouse;
    private static GUIController? guiController;
    private ClickSelectionManager? selectionManager;
    private VariableManager variableManager;
    private float fps;
    private string[]? args;

    [STAThread]
    static void Main(string[] args)
    { 
        string executablePath = Path.GetDirectoryName(Application.StartupPath) ?? string.Empty;
        logFilePath = Path.Combine(executablePath, logFileName);
        resourcesPath = Path.Combine(executablePath, resourcesPath);
#if DEBUG
        File.AppendAllText(logFilePath, $"[{DateTime.Now}] [Info] [.ctor] Starting O3DView...\n");
#endif
        Program program = new();
        program.args = args;
        program.StartApp();
    }

    public Program()
    {
        appdataPath = string.Empty;
        try
        {
            appdataPath = Environment.ExpandEnvironmentVariables(@"%APPDATA%\O3DView");
            if (!Directory.Exists(appdataPath))
                Directory.CreateDirectory(appdataPath);
        }
#pragma warning disable CS0168
        catch (Exception ex)
#pragma warning restore CS0168
        {
#if DEBUG
            File.AppendAllText(logFilePath, $"[{DateTime.Now}] [{Severity.Error}] [.ctor] Couldn't get appdata directory: {ex.Message}\n");
#endif
        }
        settingsManager = new(true, appdataPath);
        variableManager = new();
    }

    public void StartApp()
    {
        var wndOptions = new WindowOptions(ViewOptions.Default);
        switch(Settings.AntiAliasingLevel)
        {
            case Settings.AntiAliasing.None:
                wndOptions.Samples = 0;
                break;
            case Settings.AntiAliasing.MSAAx2:
                wndOptions.Samples = 2;
                break;
            case Settings.AntiAliasing.MSAAx4:
                wndOptions.Samples = 4;
                break;
            case Settings.AntiAliasing.MSAAx8:
                wndOptions.Samples = 8;
                break;
        }
        wndOptions.Title = "O3DView - Fast O3D File Viewer";
        wndOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new(4, 3));
        using var window = Window.Create(wndOptions);
        GL? gl = null;

        window.Load += () =>
        {
            gl = window.CreateOpenGL();
#if DEBUG
            gl.DebugMessageControl(DebugSource.DontCare, DebugType.DontCare, DebugSeverity.DebugSeverityLow, null, true);
            nint tmp = 0;
            gl.DebugMessageCallback<nint>((GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam) =>
            {
                if ((uint)severity == (uint)DebugSeverity.DebugSeverityNotification)
                    return;
                string msg = Marshal.PtrToStringUTF8(message, length);
                Debug.WriteLine($"[GL_Debug] [{(DebugSeverity)severity}] [{(DebugType)type}] {msg}");
            }, ref tmp);
            gl.Enable(EnableCap.DebugOutput);
#endif

            SetIcon(window);

            inputContext = window.CreateInput();
            guiController = new(window, inputContext, gl, this);
            Log("Initialised OpenGL context!");
            Log($"OpenGL Version: '{gl.GetStringS(StringName.Version)}' " +
                $"Vendor: '{gl.GetStringS(StringName.Vendor)}' " +
                $"Renderer: '{gl.GetStringS(StringName.Renderer)}' " +
                $"GLSL Version: '{gl.GetStringS(StringName.ShadingLanguageVersion)}'");
            SetupInput();
            renderer = new(gl, window, (string caption, string message) => guiController.ShowErrorPopup(caption, message), this);
            renderer.scene.camera.Mouse = mainMouse;
            renderer.scene.camera.Keyboard = mainKeyboard;

            selectionManager = new(mainMouse, mainKeyboard, window, this, renderer.scene.camera);
            gizmoManager = new(this, gl);
            //sdfManager = new(this, gl);
            fileLoader = new(this, renderer);

            Focus(null);
            if (args?.Length > 0)
            {
                fileLoader.LoadFiles(args);
            }
        };

        // Handle resizes
        window.FramebufferResize += size =>
        {
            if (gl == null)
                return;

            gl.Viewport(size);
        };

        // The render function
        window.Render += delta =>
        {
            if (guiController == null || renderer == null || selectionManager == null || fileLoader == null)
                return;

            fps = ((float)(1 / delta) * .1f + fps * .9f);

            renderer.Render(delta);
            guiController.Render(delta);
            selectionManager.Update();
            fileLoader.OnFrame();
            O3DTexture.LoadTextureResults();
        };

        window.FileDrop += (files) =>
        {
            fileLoader?.LoadFiles(files);
        };

        // The closing function
        window.Closing += () =>
        {
            settingsManager.SaveSettings();
            guiController?.Dispose();
            gl?.Dispose();
            inputContext?.Dispose();
        };

        window.Run();
    }

    private static void SetIcon(IWindow window)
    {
        Task.Run(() =>
        {
            try
            {
                using var iconStream = LoadResourceFile("O3DIcon.ico");
                var reader = new Ico.Reader.IcoReader();
                var icon = reader.Read(iconStream) ?? throw new IOException();
                List<RawImage> imgs = [];
                for (int i = 0; i < icon.ImageReferences.Count; i++)
                {
                    var img = icon.ImageReferences[i];
                    if (img.Format == Ico.Reader.Data.IcoImageFormat.BMP && img.BitCount != 32)
                        continue;
                    //iconStream.Position = img.Offset;
                    //var data = new byte[img.Size];
                    //iconStream.Read(data);
                    var data = icon.GetImage(i);
                    if (data == null)
                        continue;

                    try
                    {
                        var res = ImageResult.FromMemory(data, ColorComponents.RedGreenBlueAlpha);
                        imgs.Add(new(res.Width, res.Height, res.Data));
                    } catch { }

                    /*if (img.Format == Ico.Reader.Data.IcoImageFormat.PNG)
                    {
                        var res = ImageResult.FromMemory(data, ColorComponents.RedGreenBlueAlpha);
                        imgs.Add(new(res.Width, res.Height, res.Data));
                    } 
                    else
                    {
                        // ARGB to RGBA conversion
                        var dataInt = MemoryMarshal.Cast<byte, int>(data);
                        for (int d = 0; d < dataInt.Length; d++)
                        {
                            var argb = dataInt[d];
                            dataInt[d] = (argb << 8) | (argb & 0xff);
                        }
                        //imgs.Add(new(img.Width, img.Height, data));
                    }*/
                }

                window.SetWindowIcon(imgs.ToArray());
            }
            catch (Exception ex)
            {
                Log($"Failed to set window icon: {ex.Message}", "Window Icon", GUIController.Severity.Warning);
            }
        });
    }

    public static string GetResourcePath(string fileName)
    {
        return Path.Combine(resourcesPath, fileName);
    }

    /// <summary>
    /// Attempts to load an embedded resource file from the executing assembly.
    /// </summary>
    /// <param name="path">The path/filename to the file to load.</param>
    /// <returns>A stream of the file contents.</returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static Stream LoadResourceFile(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(str => str.EndsWith(Path.GetFileName(path)));

        if (resourceName == null || assembly == null)
            throw new FileNotFoundException(path);

        return assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException(path);
    }

    public static void Log(string message, string? caption = null, GUIController.Severity severity = GUIController.Severity.Info, bool popupOnError = true)
    {
        if (popupOnError && severity > GUIController.Severity.Error)
            guiController?.ShowErrorPopup(caption ?? "Error", message);
        else
            guiController?.LogMessage(message, caption, severity);
    }

    private void SetupInput()
    {
        if (inputContext == null)
            return;

        mainKeyboard = inputContext.Keyboards.FirstOrDefault();
        mainMouse = inputContext.Mice.FirstOrDefault();
        /*if (mainKeyboard != null)
        {
            mainKeyboard.KeyDown += MainKeyboard_KeyDown;
            mainKeyboard.KeyUp += MainKeyboard_KeyUp;
        }

        for (int i = 0; i < inputContext.Mice.Count; i++)
        {
            var mouse = inputContext.Mice[i];
            mouse.Cursor.CursorMode = CursorMode.Normal;
            mouse.MouseMove += HandleMouseMove;
            mouse.Scroll += HandleMouseWheel;
            mouse.MouseDown += HandleMouseDown;
            mouse.MouseUp += HandleMouseUp;
        }*/
    }

    public void Select(SceneObject? mesh)
    {
        selectedObject = mesh;
    }

    public bool IsSelected(SceneObject obj)
    {
        return SelectedObject == obj;
    }

    public void Focus(SceneObject? obj)
    {
        if (renderer == null)
            return;
        if (obj == null)
        {
            var bounds = renderer.scene.SceneObjects.Where(x => x is not Light && x is not Camera)
                .Select(x => x.Bounds);
            if (bounds.Any())
                renderer.scene.camera.Focus(bounds.Aggregate((last, next) => last.Union(next)));
            else
                renderer.scene.camera.Focus(new() { center = Vector3.Zero, size = Vector3.One * 5 });
        }
        else if (obj is SceneObject mesh)
            renderer.scene.camera.Focus(mesh.Bounds);
    }

    public void DuplicateObj(SceneObject? obj = null, SceneObject? parent = null, bool includeChildren = true)
    {
        var baseObj = obj ?? selectedObject;
        var dup = baseObj?.Clone();
        if (dup == null)
            return;

        SceneGraph?.Add(dup, parent ?? dup.Parent);
        if (includeChildren)
        {
            foreach(var c in baseObj?.children ?? [])
                DuplicateObj(c, dup, includeChildren);
        }
    }
}
