# Southern-Hemisphere-Seasons

Version: 2.0.0

Author: Dildz

SPT Version: 4.0.x


## Overview:

- The Southern-Hemisphere-Seasons mod aligns the in-game seasons with the real-world seasons for players in the Southern Hemisphere.
- The mod automatically adjusts the in-game season based on the current date, ensuring that the season reflects the real-world time of year.


### Key Features:

- Automatically sets the in-game season based on the real-world date, corresponding to the Southern Hemisphere's seasonal calendar.
- The season is checked on server start, and uses a state file to track seasons over time with proper rotation - ensuring the correct season is enabled, as (FIKA) servers don't always have restart scripts.


## Installation:

1. Download and Extract: Download the mod package zip from releases and extract it into your SPT directory.

2. Configuration: 
   - The mod comes with a `config.json` file where you can enable/disable custom seasonal weather, or force a season.


## How It Works:

1. Season Determination: The mod calculates the current season based on the server’s system date and time.
It follows the following seasonal calendar:
   - Summer: December 1 to February 28
   - Autumn: March 1 - April 30
   - Late Autumn: May 1 - May 31
   - Winter: June 1 - August 31
   - Early Spring: September 1 - September 30
   - Spring: October 1 - November 30

2. Persistence: The mod uses a state file, ensuring that the correct season is always in effect.


## Notes:

- The mod defaults to summer if there is any issue determining the current season.
- Ensure the server’s time zone is correctly configured, as the mod relies on the system date to determine the season.
- The `forceSeason` option in `config.jsonc` is used for testing but can it can be set to force a season.

  Valid `forceSeason` values:
   - Summer = 0
   - Autumn = 1
   - Spring = 2
   - Storm = 3
   - LateAutumn = 4
   - EarlySpring = 5
   - Winter = 6   
   - Auto Detect = null


## Support:

This mod is provided as-is, with no official support. If bugs are reported I will do my best to fix.

It is intended for tinkerers and advanced users who understand how to modify and troubleshoot their game environment.

Please make sure to back up your game and mod files before making any changes.


## License:

This mod is licensed under the MIT License. See the `LICENSE` file for more details.


## Credits:

- [turbodestroyer1337](https://forge.sp-tarkov.com/user/71742/turbodestroyer) for the original [season-rotator](https://forge.sp-tarkov.com/mod/2314/season-rotator) mod which this SPT 4.0 update is based on.
