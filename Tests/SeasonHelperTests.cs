using NUnit.Framework;
using SouthernHemisphereSeasons;
using SPTarkov.Server.Core.Models.Enums;

namespace SouthernHemisphereSeasons.Tests;

/// <summary>
/// Covers SeasonHelper only - the pure calendar logic that is the whole point of this mod.
/// SeasonLoader/SeasonUpdater are deliberately not tested: exercising them means mocking
/// RaidWeatherService (a concrete class with five ctor dependencies) to assert almost nothing.
/// </summary>
[TestFixture]
public class SeasonHelperTests
{
    private static DateTime InMonth(int month) => new(2026, month, 15);

    // --- The Southern Hemisphere calendar, one case per month ---------------------------------

    [TestCase(12, Season.SUMMER)]
    [TestCase(1, Season.SUMMER)]
    [TestCase(2, Season.SUMMER)]
    [TestCase(3, Season.AUTUMN)]
    [TestCase(4, Season.AUTUMN)]
    [TestCase(5, Season.AUTUMN_LATE)]
    [TestCase(6, Season.WINTER)]
    [TestCase(7, Season.WINTER)]
    [TestCase(8, Season.WINTER)]
    [TestCase(9, Season.SPRING_EARLY)]
    [TestCase(10, Season.SPRING)]
    [TestCase(11, Season.SPRING)]
    public void GetSeasonFromDate_MapsEachMonthToDocumentedSeason(int month, Season expected)
    {
        Assert.That(SeasonHelper.GetSeasonFromDate(InMonth(month)), Is.EqualTo(expected));
    }

    [Test]
    public void GetSeasonFromDate_CoversEveryMonth()
    {
        // Guards the switch's default arm: if a month ever stopped being handled it would silently
        // fall through to SUMMER. Every month must map to the season the README documents.
        var seasons = Enumerable.Range(1, 12).Select(m => SeasonHelper.GetSeasonFromDate(InMonth(m))).ToList();

        Assert.That(seasons, Has.Exactly(3).EqualTo(Season.SUMMER), "Dec, Jan, Feb");
        Assert.That(seasons, Has.Exactly(2).EqualTo(Season.AUTUMN), "Mar, Apr");
        Assert.That(seasons, Has.Exactly(1).EqualTo(Season.AUTUMN_LATE), "May");
        Assert.That(seasons, Has.Exactly(3).EqualTo(Season.WINTER), "Jun, Jul, Aug");
        Assert.That(seasons, Has.Exactly(1).EqualTo(Season.SPRING_EARLY), "Sep");
        Assert.That(seasons, Has.Exactly(2).EqualTo(Season.SPRING), "Oct, Nov");
    }

    // --- forceSeason ---------------------------------------------------------------------------

    [TestCase(0, Season.SUMMER)]
    [TestCase(1, Season.AUTUMN)]
    [TestCase(2, Season.WINTER)]
    [TestCase(3, Season.SPRING)]
    [TestCase(4, Season.AUTUMN_LATE)]
    [TestCase(5, Season.SPRING_EARLY)]
    public void GetSeason_ForceSeasonInRange_OverridesTheDate(int forced, Season expected)
    {
        var config = new ModConfig { ForceSeason = forced };

        // June would otherwise be WINTER, so anything but `expected` means the override was ignored.
        Assert.That(SeasonHelper.GetSeason(config, InMonth(6)), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase(-1)]
    [TestCase(6)]   // STORM exists in SPT's enum but this mod deliberately does not expose it
    [TestCase(99)]
    public void GetSeason_ForceSeasonNullOrOutOfRange_FallsBackToTheDate(int? forced)
    {
        var config = new ModConfig { ForceSeason = forced };

        Assert.That(SeasonHelper.GetSeason(config, InMonth(6)), Is.EqualTo(Season.WINTER));
    }

    // --- Display names -------------------------------------------------------------------------

    /// <summary>
    /// SeasonNames is a positional array indexed by (int)season, so it silently depends on the
    /// numeric values of SPT's Season enum. If a future SPT release renumbers or reorders that enum,
    /// every displayed season name would shift by one with no compile error. This test is the
    /// tripwire for that - it should fail loudly on the SPT package bump, not in a user's log.
    /// </summary>
    [TestCase(Season.SUMMER, "Summer")]
    [TestCase(Season.AUTUMN, "Autumn")]
    [TestCase(Season.WINTER, "Winter")]
    [TestCase(Season.SPRING, "Spring")]
    [TestCase(Season.AUTUMN_LATE, "Late Autumn")]
    [TestCase(Season.SPRING_EARLY, "Early Spring")]
    public void GetSeasonName_MatchesSptEnumOrdering(Season season, string expected)
    {
        Assert.That(SeasonHelper.GetSeasonName(season), Is.EqualTo(expected));
    }

    [Test]
    public void GetSeasonName_UnknownSeason_DoesNotThrow()
    {
        // STORM (6) sits past the end of SeasonNames; an out-of-range index must not blow up.
        Assert.That(SeasonHelper.GetSeasonName(Season.STORM), Is.EqualTo("Unknown"));
    }
}
