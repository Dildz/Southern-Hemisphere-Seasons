using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using System.Reflection;
using System.Text.Json;

namespace SouthernHemisphereSeasons;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.dildz.southern-hemisphere-seasons";
    public override string Name { get; init; } = "Southern-Hemisphere-Seasons";
    public override string Author { get; init; } = "Dildz";
    public override List<string>? Contributors { get; init; } = ["bushtail"];
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = false;
    public override string License { get; init; } = "MIT";
}

public record ModConfig
{
    public bool Enabled { get; set; } = true;
    public int? ForceSeason { get; set; }
    public bool AllowEventSeason { get; set; } = false;
}

// Shared state between the OnLoad and OnUpdate classes (SPT may create separate instances for each).
public static class SeasonState
{
    public static ModConfig Config { get; set; } = new();
    public static Season? LastSeason { get; set; }
    public static bool EventSeasonActive { get; set; }
}

// Run after PostSptModLoader so we override SPT's own weather setup and regenerate the forecast.
#pragma warning disable CS0618
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]
public class SeasonLoader(
    ISptLogger<SeasonLoader> logger,
    ConfigServer configServer,
    RaidWeatherService raidWeatherService,
    ModHelper modHelper) : IOnLoad
{
    private readonly WeatherConfig _weatherConfig = configServer.GetConfig<WeatherConfig>();
#pragma warning restore CS0618

    private const string ModName = "[Southern-Hemisphere-Seasons]";

    public Task OnLoad()
    {
        LoadConfig();

        if (!SeasonState.Config.Enabled)
        {
            logger.Info($"{ModName} Mod is disabled in config.jsonc");
            return Task.CompletedTask;
        }

        // If a seasonal event already forced a season and we're configured to respect that, back off
        if (SeasonState.Config.AllowEventSeason && _weatherConfig.OverrideSeason.HasValue)
        {
            SeasonState.EventSeasonActive = true;
            SeasonState.LastSeason = _weatherConfig.OverrideSeason.Value;
            logger.Info($"{ModName} Seasonal event detected - respecting event season: {SeasonHelper.GetSeasonName(_weatherConfig.OverrideSeason.Value)}");
            return Task.CompletedTask;
        }

        var season = SeasonHelper.GetSeason(SeasonState.Config);
        _weatherConfig.OverrideSeason = season;
        SeasonState.LastSeason = season;

        // Regenerate the weather forecast with our season (SPT already generated one, we need to replace it)
        raidWeatherService.GenerateFutureWeatherAndCache(season);

        logger.Success($"{ModName} Loaded - Season set to: {SeasonHelper.GetSeasonName(season)}");
        return Task.CompletedTask;
    }

    private void LoadConfig()
    {
        try
        {
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var configPath = Path.Combine(pathToMod, "config", "config.jsonc");

            if (!File.Exists(configPath))
            {
                logger.Warning($"{ModName} Config file not found, using defaults");
                return;
            }

            var json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            var config = JsonSerializer.Deserialize<ModConfig>(json, options);

            if (config != null)
                SeasonState.Config = config;
        }
        catch (Exception e)
        {
            logger.Error($"{ModName} Error reading config: {e.Message}");
        }
    }
}

// Recheck the season every hour for long-running servers.
[Injectable(TypePriority = OnUpdateOrder.InsuranceCallbacks)]
#pragma warning disable CS0618
public class SeasonUpdater(
    ISptLogger<SeasonUpdater> logger,
    ConfigServer configServer,
    RaidWeatherService raidWeatherService) : IOnUpdate
{
    private readonly WeatherConfig _weatherConfig = configServer.GetConfig<WeatherConfig>();
#pragma warning restore CS0618

    private const string ModName = "[Southern-Hemisphere-Seasons]";
    private const long CheckIntervalMs = 3_600_000; // 1 hour
    private long _timeSinceLastCheck;

    public Task<bool> OnUpdate(long timeSinceLastRun)
    {
        if (!SeasonState.Config.Enabled || SeasonState.EventSeasonActive)
            return Task.FromResult(true);

        _timeSinceLastCheck += timeSinceLastRun;
        if (_timeSinceLastCheck < CheckIntervalMs)
            return Task.FromResult(true);

        _timeSinceLastCheck = 0;

        var season = SeasonHelper.GetSeason(SeasonState.Config);
        if (season != SeasonState.LastSeason)
        {
            _weatherConfig.OverrideSeason = season;
            SeasonState.LastSeason = season;
            raidWeatherService.GenerateFutureWeatherAndCache(season);
            logger.Success($"{ModName} Season changed to: {SeasonHelper.GetSeasonName(season)}");
        }

        return Task.FromResult(true);
    }
}

public static class SeasonHelper
{
    private static readonly string[] SeasonNames =
    [
        "Summer",       // 0
        "Autumn",       // 1
        "Winter",       // 2
        "Spring",       // 3
        "Late Autumn",  // 4
        "Early Spring"  // 5
    ];

    public static Season GetSeason(ModConfig config)
    {
        if (config.ForceSeason is { } forced && forced >= 0 && forced <= 5)
            return (Season)forced;

        return GetSeasonFromDate();
    }

    /// <summary>
    /// Determines the current season based on the Southern Hemisphere calendar:
    /// - Summer:       December 1 - February 28/29
    /// - Autumn:       March 1 - April 30
    /// - Late Autumn:  May 1 - May 31
    /// - Winter:       June 1 - August 31
    /// - Early Spring: September 1 - September 30
    /// - Spring:       October 1 - November 30
    /// </summary>
    private static Season GetSeasonFromDate()
    {
        var month = DateTime.Now.Month;

        return month switch
        {
            12 or 1 or 2 => Season.SUMMER,
            3 or 4       => Season.AUTUMN,
            5            => Season.AUTUMN_LATE,
            6 or 7 or 8  => Season.WINTER,
            9            => Season.SPRING_EARLY,
            10 or 11     => Season.SPRING,
            _            => Season.SUMMER
        };
    }

    public static string GetSeasonName(Season season)
    {
        var index = (int)season;
        return index >= 0 && index < SeasonNames.Length ? SeasonNames[index] : "Unknown";
    }
}
