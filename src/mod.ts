/*
 * Description: 
 * SPT server mod that adjusts the seasons based on the IRL season in the Southern Hemisphere.
 * Cannot get Early Spring or Storm to pass raid load, so it's disabled for now (enabling Storms in the config will do nothing)
 */

// Required imports
import path from "node:path";
import { DependencyContainer } from "tsyringe";
import { IPostDBLoadMod } from "@spt/models/external/IPostDBLoadMod";
import { VFS } from "@spt/utils/VFS";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";
import { jsonc } from "jsonc";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { IWeatherConfig } from "@spt/models/spt/config/IWeatherConfig";
import { IPostSptLoadMod } from "@spt/models/external/IPostSptLoadMod";

// Valid season numbers
enum Season {
    Summer = 0,
    Autumn = 1,
    Winter = 2,
    Spring = 3,
    LateAutumn = 4,
    //EarlySpring = 5,
    //Storm = 6,
}

// Configuration interface
interface ModConfig {
    enableMod: boolean;
    //enableStorms: boolean;
    consoleMessages: boolean;
    forceSeason: Season | -1;      // -1 is no forced season
}

// Main mod class
class SouthernHemisphereSeasons implements IPostDBLoadMod, IPostSptLoadMod {
    private modName = "[Southern Hemisphere Seasons]";
    private seasonsArray: Season[] = [
        Season.Summer,
        Season.Autumn,
        Season.Winter,
        Season.Spring,
        Season.LateAutumn,
        //Season.EarlySpring,
        //Season.Storm",
    ];
    
    private finalSelectedSeason: number;
    private config: ModConfig = {
        enableMod: true,
        //enableStorms: false,
        consoleMessages: true,
        forceSeason: -1,        // default to not forced
    };

    // Load the mod configuration from `config.jsonc`.
    private loadConfig(vfs: VFS): void {
        const configPath = path.resolve(__dirname, "../config/config.jsonc");
        const loadedConfig = jsonc.parse(vfs.readFile(configPath)) as Partial<ModConfig>;

        // Merge loaded config with default values
        this.config = { ...this.config, ...loadedConfig };

        // Validate `forceSeason`
        if (typeof this.config.forceSeason !== "number" || this.config.forceSeason < -1 || this.config.forceSeason >= this.seasonsArray.length) {
            console.warn(`${this.modName} Invalid forceSeason value detected in config. Defaulting to -1.`);
            this.config.forceSeason = -1;
        }
    }

    // Determine the season based on the system date.
    private getRealLifeSeason(): number {
        const now = new Date();
        const month = now.getMonth();       // 0 = January, 11 = December
        /*
        // Get day of the month & check if storms are enabled
        const day = now.getDate();
        
        // To-Do: Impliment % chance of storms during raids in Summer
        if (this.config.enableStorms) {
            if ((month === 0 && day => 10) || (month === 0 && day <= 15)) return Season.Storm;      // Storm (January 10 - January 15)
        }
        */
    
        // Southern Hemisphere season mapping (re-adjusted dates to exclude early spring)
        if (month === 11 || month === 0 || month === 1) return Season.Summer;       // Summer (December 1 - February 28)
        if (month === 2 || month === 3) return Season.Autumn;                       // Autumn (March 1 - April 30)
        if (month === 4) return Season.LateAutumn;                                  // Late Autumn (May 1 - May 31)
        if (month === 5 || month === 6 || month === 7) return Season.Winter;        // Winter (June 1 - August 31)
        if (month === 8 || month === 9 || month === 10) return Season.Spring;       // Spring (September 1 - November 30)
        //if (month === 8) return Season.EarlySpring;                               // Early Spring (September 1 - September 30)
    
        return Season.Summer;       // default to Summer in case of an error
    }
    
    // Set the in-game season based on the detected or forced season.
    public postDBLoad(container: DependencyContainer): void {
        const vfs = container.resolve<VFS>("VFS");
        this.loadConfig(vfs);

        if (!this.config.enableMod) {
            console.log(`${this.modName} Mod is disabled in config. Skipping season adjustment.`);
            return;
        }

        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const logger = container.resolve<ILogger>("WinstonLogger");
        const seasonConfig: IWeatherConfig = configServer.getConfig(ConfigTypes.WEATHER);

        seasonConfig.overrideSeason = null;     // reset initial value to null

        // Check if a forced season is set in the configuration
        if (this.config.forceSeason !== -1) {
            // Apply the forced season
            seasonConfig.overrideSeason = this.config.forceSeason;
            if (this.config.consoleMessages) {
                logger.success(`${this.modName} Forced season selected: ${this.seasonsArray[this.config.forceSeason]}`);
            }
        } else {
            // Automatically detect the IRL season if not forced
            const detectedSeason = this.getRealLifeSeason();
            seasonConfig.overrideSeason = detectedSeason;

            if (this.config.consoleMessages) {
                logger.success(`${this.modName} Automatically selected season: ${Season[detectedSeason]}`);
            }
        }

        this.finalSelectedSeason = seasonConfig.overrideSeason;
    }

    // Check for conflicts if other mods override the selected season.
    public postSptLoad(container: DependencyContainer): void {
        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const logger = container.resolve<ILogger>("WinstonLogger");
        const seasonConfig: IWeatherConfig = configServer.getConfig(ConfigTypes.WEATHER);

        if (!this.config.enableMod) return;    // skip if the mod is disabled

        if (this.finalSelectedSeason !== seasonConfig.overrideSeason) {
            if (seasonConfig.overrideSeason === null) {
                logger.warning(
                    `${this.modName} Another mod has overridden the selected season. Current season: Auto. Check your load order.`
                );
            } else if (seasonConfig.overrideSeason < this.seasonsArray.length) {
                logger.warning(
                    `${this.modName} Another mod has overridden the selected season. Current season: ${this.seasonsArray[seasonConfig.overrideSeason]}. Check your load order.`
                );
            } else {
                logger.warning(
                    `${this.modName} Another mod has overridden the selected season to an invalid value: ${seasonConfig.overrideSeason}. Check your load order.`
                );
            }
        }
    }
}

export const mod = new SouthernHemisphereSeasons();
