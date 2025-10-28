using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using Season = SPTarkov.Server.Core.Models.Enums.Season;

namespace SouthernHemisphereSeasons;

// Mod metadata - basic information about the mod
public record ModMetadata : AbstractModMetadata{
    public override string ModGuid { get; init; } = "com.dildz.southern-hemisphere-seasons";
    public override string Name { get; init; } = "SouthernHemisphereSeasons";
    public override string Author { get; init; } = "Dildz";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.2");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}

// Main class that handles season calculation - runs when the game loads
[Injectable(TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class ChangeSeason(ConfigServer configServer, ISptLogger<ChangeSeason> logger, ModHelper modHelper) : IOnLoad {
    // Get the game's weather configuration
    private readonly WeatherConfig _weatherConfig = configServer.GetConfig<WeatherConfig>();
    
    // Southern hemisphere season order and dates with fixed lengths
    private readonly SeasonDefinition[] _southernSeasons = [
        new SeasonDefinition(Season.SUMMER, 12, 1, 2, 28, 90),      // Summer: Dec 1 - Feb 28 (90 days)
        new SeasonDefinition(Season.AUTUMN, 3, 1, 4, 30, 61),       // Autumn: Mar 1 - Apr 30 (61 days)
        new SeasonDefinition(Season.AUTUMN_LATE, 5, 1, 5, 31, 31),  // Late Autumn: May 1 - May 31 (31 days)
        new SeasonDefinition(Season.WINTER, 6, 1, 8, 31, 92),       // Winter: Jun 1 - Aug 31 (92 days)
        new SeasonDefinition(Season.SPRING_EARLY, 9, 1, 9, 30, 30), // Early Spring: Sep 1 - Sep 30 (30 days)
        new SeasonDefinition(Season.SPRING, 10, 1, 11, 30, 61)      // Spring: Oct 1 - Nov 30 (61 days)
    ];
    
    private ModState _modState = new();

    // This method runs when the mod loads
    public Task OnLoad() {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json");
        
        // Get season, override season
        var season = GetCurrentSeason(pathToMod, config);
        var seasonEnum = (Season)season;
        _weatherConfig.OverrideSeason = seasonEnum;
        
        // Apply custom weather
        if (config.UseCustomWeather) {
            ApplyCustomWeather(pathToMod, seasonEnum);
        }
        
        logger.Success($"[Southern Hemisphere Seasons] Applied season: {seasonEnum} (ends on {_modState.EndDate})");
        return Task.CompletedTask;
    }

    // Returns the current season based on config, state file, or southern hemisphere calendar
    private int GetCurrentSeason(string modPath, ModConfig config) {
        // Check if forceSeason is configured
        if (config.ForceSeason.HasValue) {
            logger.Success($"[Southern Hemisphere Seasons] Using forced season: {(Season)config.ForceSeason.Value}");
            return config.ForceSeason.Value;
        }

        var stateFilePath = Path.Combine(modPath, "state.json");
        var today = DateTime.Now;
        
        // If no state file exists, create one based on southern hemisphere calendar
        if (!File.Exists(stateFilePath)) {
            return CreateNewSeasonState(modPath, today);
        }
        
        // Load existing state and check if season has expired
        var state = modHelper.GetJsonDataFromFile<ModState>(modPath, "state.json");
        var endDate = DateTime.Parse(state.EndDate);
        
        if (today > endDate) {
            // Season has expired, calculate new season based on southern hemisphere calendar
            return CalculateNewSeason(modPath, today, state);
        }
        
        _modState = state;
        return state.CurrentSeason;
    }

    // Creates a new season state based on southern hemisphere calendar
    private int CreateNewSeasonState(string modPath, DateTime currentDate) {
        var currentSeasonDef = GetSeasonDefinitionByDate(currentDate);
        var endDate = currentDate.AddDays(currentSeasonDef.Length);
        
        logger.Info($"[Southern Hemisphere Seasons] Creating new state file. Season: {currentSeasonDef.Season}");
        
        var newState = new ModState {
            StartDate = currentDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            CurrentSeason = (int)currentSeasonDef.Season
        };
        
        ModExtensions.SaveJsonDataToFile(modHelper, modPath, "state.json", newState);
        _modState = newState;
        return (int)currentSeasonDef.Season;
    }

    // Calculates new season when current one expires
    private int CalculateNewSeason(string modPath, DateTime currentDate, ModState oldState) {
        var currentSeasonDef = GetSeasonDefinitionByDate(currentDate);
        var endDate = currentDate.AddDays(currentSeasonDef.Length);
        
        var newState = new ModState {
            StartDate = currentDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            CurrentSeason = (int)currentSeasonDef.Season
        };
        
        ModExtensions.SaveJsonDataToFile(modHelper, modPath, "state.json", newState);
        _modState = newState;
        return (int)currentSeasonDef.Season;
    }

    // Gets the southern hemisphere season definition for a given date
    private SeasonDefinition GetSeasonDefinitionByDate(DateTime date) {
        var monthDay = date.Month * 100 + date.Day;
        
        foreach (var seasonDef in _southernSeasons) {
            var start = seasonDef.StartMonth * 100 + seasonDef.StartDay;
            var end = seasonDef.EndMonth * 100 + seasonDef.EndDay;
            
            // Handle seasons that don't wrap year boundaries
            if (start <= end) {
                if (monthDay >= start && monthDay <= end) {
                    return seasonDef;
                }
            } else {
                // Handle seasons that wrap year boundaries (like Summer: Dec-Feb)
                if (monthDay >= start || monthDay <= end) {
                    return seasonDef;
                }
            }
        }
        
        // Fallback - should never reach here
        logger.Warning("[Southern Hemisphere Seasons] Date outside expected ranges, defaulting to Summer");
        return _southernSeasons[0]; // Return Summer as fallback
    }

    // Apply custom weather settings from JSON file
    private void ApplyCustomWeather(string modPath, Season currentSeason) {
        try {
            var root = modHelper.GetJsonDataFromFile<JsonNode>(modPath, "customWeather.json");
            
            // Null check for root
            if (root == null) {
                logger.Warning("[Southern Hemisphere Seasons] customWeather.json not found or invalid");
                return;
            }

            var weatherPresetWeightNode = root["weatherPresetWeight"] as JsonObject;
            
            // Override weather probabilities for each season
            if (weatherPresetWeightNode is not null) {
                foreach (var kvp in weatherPresetWeightNode) {
                    var seasonKey = kvp.Key;
                    var valueObj = kvp.Value as JsonObject;
                    var dict = new Dictionary<SPTarkov.Server.Core.Models.Spt.Config.WeatherPreset, double>();
                    
                    if (valueObj != null) {
                        foreach (var inner in valueObj) {
                            if (Enum.TryParse(inner.Key, true, out SPTarkov.Server.Core.Models.Spt.Config.WeatherPreset preset) && inner.Value is JsonValue jv && jv.TryGetValue<double>(out var w)) {
                                dict[preset] = w;
                            }
                        }
                    }
                    _weatherConfig.Weather.WeatherPresetWeight[seasonKey] = dict;
                }
            }
            
            // Apply detailed weather presets for specific seasons
            if (currentSeason is Season.AUTUMN or Season.SPRING or Season.SPRING_EARLY or Season.AUTUMN_LATE or Season.WINTER) {
                var seasonWeatherKey = "DEMI"; // Default for spring/autumn
                if (currentSeason is Season.WINTER) { 
                    seasonWeatherKey = "WINTER"; 
                }
                
                // Safe null checks for JSON navigation
                var presetsNode = root["Presets"] as JsonObject;
                var seasonPresetNode = presetsNode?[seasonWeatherKey] as JsonObject;
                
                if (seasonPresetNode is not null) {
                    foreach (var kvp in seasonPresetNode) {
                        var key = kvp.Key;
                        if (kvp.Value is JsonNode node) {
                            var value = node.Deserialize<SPTarkov.Server.Core.Models.Spt.Config.PresetWeights>();
                            if (value != null) {
                                _weatherConfig.Weather.PresetWeights[key] = value;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) {
            logger.Error($"[Southern Hemisphere Seasons] Error applying custom weather: {ex.Message}");
        }
    }
}

// Represents a season with its date range and fixed length
public record SeasonDefinition(Season Season, int StartMonth, int StartDay, int EndMonth, int EndDay, int Length);

// Configuration class for the mod
public class ModConfig {
    public bool UseCustomWeather { get; set; } = true;
    public int? ForceSeason { get; set; } // null = auto, 0=Summer, 1=Autumn, 2=Spring, 3=Storm, 4=LateAutumn, 5=EarlySpring, 6=Winter
}

// State tracking class - saved to state.json
public class ModState {
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public int CurrentSeason { get; set; }
}

// Helper extension for saving JSON files
public static class ModExtensions
{
    public static void SaveJsonDataToFile<T>(this ModHelper modHelper, string pathToFile, string fileName, T data)
    {
        var fullPath = Path.Combine(pathToFile, fileName);
        var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fullPath, json);
    }
}