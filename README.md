# Southern-Hemisphere-Seasons

Version: 1.3.0

Author: Dildz

SPT Version: ^3.10.x


## Overview:

- The Southern-Hemisphere-Seasons mod aligns the in-game seasons with the real-world seasons for players in the Southern Hemisphere.
- The mod automatically adjusts the in-game season based on the current date, ensuring that the season reflects the real-world time of year.


### Key Features:

- Real-World Season Alignment: Automatically sets the in-game season based on the real-world date, corresponding to the Southern Hemisphere's seasonal calendar.
- Additional "Late Autum", "Early Spring" & "Storm" seasons added.
- Persistence Across Reboots: The season is recalculated on each server startup, ensuring continuity regardless of server reboots.


## Installation:

1. Download and Extract: Download the mod package zip from releases and extract it into your SPT directory.

2. Configuration: 
   - The mod comes with a `config.json` file where you can enable or disable the mod, storms, or the mod console messages.


## How It Works:

1. Season Determination: The mod calculates the current season based on the server’s system date and time.
It follows the following seasonal calendar:
   - Summer: December 1 to February 28
   - Autumn: March 1 to May 14
   - Late Autum: May 15 to May 31
   - Winter: June 1 to August 14
   - Early Spring: August 15 to August 31
   - Spring: September 1 to November 30

2. Persistence: The mod recalculates the season on each server start, ensuring that the correct season is always in effect.


## Notes:

- The mod defaults to summer if there is any issue determining the current season.
- Ensure the server’s time zone is correctly configured, as the mod relies on the system date to determine the season.
- Seasons "Early Spring" & "Storm" cause raid loads to fail - both are disabled until a fix is found.
- To-Do: make storms a chance % per raid during Summer months, for now storms default to false (off).
- The `forceSeason` option in `config.jsonc` is used for testing but can it can be set to force a season.

  Valid `forceSeason` values:
   - Summer = 0
   - Autumn = 1
   - Winter = 2
   - Spring = 3
   - LateAutumn = 4
   - EarlySpring = 5 (disabled - will default to auto detect)
   - Storm = 6 (disabled - will default to auto detect)
   - Auto Detect = -1


## Support:

This mod is provided as-is, with no official support. If bugs are reported I will do my best to fix.

It is intended for tinkerers and advanced users who understand how to modify and troubleshoot their game environment.

Please make sure to back up your game and mod files before making any changes.


## License:

This mod is licensed under the MIT License. See the `LICENSE` file for more details.


## Credits:

- [DewardianDev](https://hub.sp-tarkov.com/user/27036-dewardiandev/) for the original [All The Seasons](https://hub.sp-tarkov.com/files/file/2052-all-the-seasons/#overview) mod which this is based on.
- [sheepy](https://hub.sp-tarkov.com/user/20801-sheepy/) for the [Simple Season Selector](https://hub.sp-tarkov.com/files/file/2118-simple-season-selector/) mod which I used as a reference for updating to SPT 3.10.X as well as adapting certain features.
