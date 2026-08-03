using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.InRaid;
using System.Reflection;
using System.Text.Json;

namespace SouthernHemisphereSeasons;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.dildz.southern-hemisphere-seasons";
    public string Name { get; init; } = "Southern-Hemisphere-Seasons";
    public string Author { get; init; } = "Dildz";
    public List<string>? Contributors { get; init; } = ["bushtail"];
    public SemanticVersioning.Version Version { get; init; } = new("3.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
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

// PostLoad is the last stage, so we override SPT's own weather setup and regenerate the forecast after
// everything else has had its say. (4.0 used PostSptModLoader, which no longer exists.)
[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class SeasonLoader(
    ISptLogger<SeasonLoader> logger,
    WeatherConfig weatherConfig,
    RaidWeatherService raidWeatherService,
    ModHelper modHelper) : IOnLoad
{
    private const string ModName = "[Southern-Hemisphere-Seasons]";

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        await LoadConfigAsync(cancellationToken);

        if (!SeasonState.Config.Enabled)
        {
            logger.Info($"{ModName} Mod is disabled in config.jsonc");
            return;
        }

        // If a seasonal event already forced a season and we're configured to respect that, back off
        if (SeasonState.Config.AllowEventSeason && weatherConfig.OverrideSeason.HasValue)
        {
            SeasonState.EventSeasonActive = true;
            SeasonState.LastSeason = weatherConfig.OverrideSeason.Value;
            logger.Info($"{ModName} Seasonal event detected - respecting event season: {SeasonHelper.GetSeasonName(weatherConfig.OverrideSeason.Value)}");
            return;
        }

        var season = SeasonHelper.GetSeason(SeasonState.Config);
        weatherConfig.OverrideSeason = season;
        SeasonState.LastSeason = season;

        // Regenerate the weather forecast with our season (SPT already generated one, we need to replace it)
        raidWeatherService.GenerateFutureWeatherAndCache(season);

        logger.Success($"{ModName} Loaded - Season set to: {SeasonHelper.GetSeasonName(season)}");
    }

    private async Task LoadConfigAsync(CancellationToken cancellationToken)
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

            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
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
public class SeasonUpdater(
    ISptLogger<SeasonUpdater> logger,
    WeatherConfig weatherConfig,
    RaidWeatherService raidWeatherService) : IOnUpdate
{
    private const string ModName = "[Southern-Hemisphere-Seasons]";
    private const long CheckIntervalSeconds = 3600; // 1 hour

    public Task<bool> OnUpdateAsync(long secondsSinceLastRun, CancellationToken cancellationToken)
    {
        if (!SeasonState.Config.Enabled || SeasonState.EventSeasonActive)
            return Task.FromResult(true);

        // Returning false leaves our last-run timestamp untouched, so secondsSinceLastRun keeps
        // accumulating across the server's 5s tick until an hour has passed. No manual counter needed.
        if (secondsSinceLastRun < CheckIntervalSeconds)
            return Task.FromResult(false);

        var season = SeasonHelper.GetSeason(SeasonState.Config);
        if (season != SeasonState.LastSeason)
        {
            weatherConfig.OverrideSeason = season;
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

    public static Season GetSeason(ModConfig config) => GetSeason(config, DateTime.Now);

    /// <summary>
    /// Overload taking the date explicitly, so the calendar logic can be tested without the system clock.
    /// </summary>
    public static Season GetSeason(ModConfig config, DateTime now)
    {
        if (config.ForceSeason is { } forced && forced >= 0 && forced <= 5)
            return (Season)forced;

        return GetSeasonFromDate(now);
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
    public static Season GetSeasonFromDate(DateTime date)
    {
        var month = date.Month;

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
