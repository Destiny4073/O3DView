using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace O3DView;

internal class SettingsManager
{
    public Settings settings;

    private string? settingsDir;
    private JsonSerializerOptions serializerOptions;

    public SettingsManager(bool loadSettings, string? settingsDir = null)
    {
        this.settingsDir = settingsDir;
        serializerOptions = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
            IncludeFields = false,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
        };
        if (loadSettings)
        {
            var loadedSettings = LoadSettings(settingsDir, serializerOptions);
            if (loadedSettings != null)
                settings = loadedSettings;
            else
            {
                settings = new();
                SaveSettings(settings);
            }
        } else
        {
            settings = new();
        }
    }

    public void SaveSettings()
    {
        SaveSettings(settings, settingsDir, serializerOptions);
    }

    public static Settings? LoadSettings(string? settingsDir = null, JsonSerializerOptions? serializerOptions = null)
    {
        try
        {
            string filename = "O3DViewSettings.json";
            string path = settingsDir != null ? Path.Combine(settingsDir, filename) : filename;
            string settingsJson = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Settings>(settingsJson, serializerOptions) ?? throw new JsonException();
        }
        catch (Exception ex)
        {
            Program.Log($"Couldn't load existing O3DView settings file! Error:\n{ex.Message}", "Load Settings", GUI.GUIController.Severity.Warning);
        }
        return null;
    }

    public static void SaveSettings(Settings settings, string? settingsDir = null, JsonSerializerOptions? serializerOptions = null)
    {
        try
        {
            string filename = "O3DViewSettings.json";
            string path = settingsDir != null ? Path.Combine(settingsDir, filename) : filename;
            string settingsJson = JsonSerializer.Serialize(settings, serializerOptions);
        }
        catch (Exception ex)
        {
            Program.Log($"Couldn't save O3DView settings file! Error:\n{ex.Message}", "Save Settings", GUI.GUIController.Severity.Warning);
        }
    }
}

//[JsonSerializable(typeof(Settings), GenerationMode = JsonSourceGenerationMode.Serialization)]
public class Settings : INotifyPropertyChanged
{
    private int maxLights = 16;
    private AntiAliasing antiAliasingLevel = AntiAliasing.MSAAx4;
    private bool shouldCacheMaterials = false;
    private bool showGizmos = true;
    private bool selectionHighlight = true;
    private float gizmoIconSize = 0.2f;
    private int maxTexturesLoadedPerFrame = 16;
    private float splineTessDist = 1f;
    private float splineCurveSag = 0.005f;
    private TimeSpan maxLoadTimePerBatch = TimeSpan.FromMilliseconds(20);

    public int MaxLights 
    { 
        get => maxLights; 
        set 
        {
            maxLights = value;
            OnPropertyChanged();
        }
    }
    public AntiAliasing AntiAliasingLevel 
    { 
        get => antiAliasingLevel; 
        set 
        { 
            antiAliasingLevel = value; 
            OnPropertyChanged(); 
        } 
    }
    public bool ShouldCacheMaterials
    {
        get => shouldCacheMaterials; 
        set
        {
            shouldCacheMaterials = value;
            OnPropertyChanged();
        }
    }
    public bool ShowGizmos
    {
        get => showGizmos; 
        set
        {
            showGizmos = value;
            OnPropertyChanged();
        }
    }
    public bool SelectionHighlight
    {
        get => selectionHighlight; 
        set
        {
            selectionHighlight = value;
            OnPropertyChanged();
        }
    }
    public float GizmoIconSize
    {
        get => gizmoIconSize; 
        set
        {
            gizmoIconSize = value;
            OnPropertyChanged();
        }
    }
    public int MaxTexturesLoadedPerFrame
    {
        get => maxTexturesLoadedPerFrame; 
        set
        {
            maxTexturesLoadedPerFrame = value;
            OnPropertyChanged();
        }
    }
    public TimeSpan MaxLoadTimePerBatch
    {
        get => maxLoadTimePerBatch;
        set
        {
            maxLoadTimePerBatch = value;
            OnPropertyChanged();
        }
    }
    public float SplineTessDist
    {
        get => splineTessDist; 
        set
        {
            splineTessDist = value;
            OnPropertyChanged();
        }
    }
    public float SplineCurveSag
    {
        get => splineCurveSag; 
        set
        {
            splineCurveSag = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public enum AntiAliasing
    {
        None,
        MSAAx2,
        MSAAx4,
        MSAAx8,
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
