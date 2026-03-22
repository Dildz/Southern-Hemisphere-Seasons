# Southern-Hemisphere-Seasons

Version: 2.0.0

Author: Dildz

SPT Version: 4.0.x


## Overview:

- The Southern-Hemisphere-Seasons mod aligns the in-game seasons with the real-world seasons for players in the Southern Hemisphere.
- The mod automatically adjusts the in-game season based on the current date, ensuring that the season reflects the real-world time of year.


### Key Features:

- Automatically sets the in-game season based on the real-world date, corresponding to the Southern Hemisphere's seasonal calendar.
- Rechecks the season every hour for long-running servers, so the season updates without needing a restart.
- Optional support for SPT seasonal events (e.g. Christmas) - can be configured to let events override the mod's season.


## Installation:

1. Download and Extract: Download the mod package zip from releases and extract it into your SPT directory.

2. Configuration:
   - The mod comes with a `config/config.jsonc` file where you can enable or disable the mod, force a season, or allow SPT seasonal events to override.


## How It Works:

Season Determination: The mod calculates the current season based on the server's system date and time.
It follows the following seasonal calendar:
   - Summer:       December 1 - February 28/29
   - Autumn:       March 1 - April 30
   - Late Autumn:  May 1 - May 31
   - Winter:       June 1 - August 31
   - Early Spring: September 1 - September 30
   - Spring:       October 1 - November 30

## Configuration:

The `config/config.jsonc` file supports the following options:

- `enabled`          - Enable or disable the mod (default: `true`)
- `forceSeason`      - Force a specific season, or `null` for auto-detect (default: `null`)
- `allowEventSeason` - If `true`, let SPT seasonal events (e.g. Christmas) override this mod's season (default: `false`)

Valid `forceSeason` values:
   - Summer = 0
   - Autumn = 1
   - Winter = 2
   - Spring = 3
   - Late Autumn = 4
   - Early Spring = 5
   - Auto Detect = null


## Notes:

- The mod defaults to Summer if there is any issue determining the current season.
- Ensure the server's time zone is correctly configured, as the mod relies on the system date/time to determine the season.


## Support:

This mod is provided as-is, with no official support. If bugs are reported I will do my best to fix.

Please make sure to back up your game and mod files before making any changes.


## License:

This mod is licensed under the MIT License. See the `LICENSE` file for more details.


## Credits:

- [DewardianDev](https://hub.sp-tarkov.com/user/27036-dewardiandev/) for the original [All The Seasons](https://hub.sp-tarkov.com/files/file/2052-all-the-seasons/) mod which was the inspiration for this mod.
