/*
 * Description: 
 * SPT server mod that adjusts the season based on the IRL season in the Southern Hemisphere.
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

// Configuration interface
interface ModConfig {
    enable: boolean;
    enableStorms: boolean;
    consoleMessages: boolean;
    forceSeason: number;
}

// Main mod class
class SouthernHemisphereSeasons implements IPostDBLoadMod, IPostSptLoadMod {
    private modName = "[Southern Hemisphere Seasons]";
    private seasonsArray = [
        "Summer",
        "Autumn",
        "Winter",
        "Spring",
        "Late Autumn",
        "Early Spring",
        "Storm",
    ];
    
    private finalSelectedSeason: number;
    private config: ModConfig = {
        enable: true,
        enableStorms: false,
        consoleMessages: true,
        forceSeason: -1, // Default to off
    };

    // Load the mod configuration from `config.jsonc`.
    private loadConfig(vfs: VFS): void {
        const configPath = path.resolve(__dirname, "../config/config.jsonc");
        const loadedConfig = jsonc.parse(vfs.readFile(configPath)) as Partial<ModConfig>;

        // Merge loaded values with defaults
        this.config = {
            ...this.config,
            ...loadedConfig,
        };

        // Validate `forceSeason` value
        if (typeof this.config.forceSeason !== "number" || this.config.forceSeason < -1 || this.config.forceSeason >= this.seasonsArray.length) {
            console.warn(`${this.modName} Invalid forceSeason value detected in config. Defaulting to -1.`);
            this.config.forceSeason = -1;
        }
    }

    // Determine the real-life season based on the Southern Hemisphere calendar.
    private getRealLifeSeason(): number {
        const now = new Date();
        const month = now.getMonth(); // 0 = January, 11 = December
        const day = now.getDate();    // Day of the month
    
        // Check if storms are enabled
        if (this.config.enableStorms) {
            // Placeholder logic for storms
            if ((month === 4 && day >= 15) || (month === 5 && day < 15)) return 6;  // Storm
        }
    
        // Southern Hemisphere season mapping
        if (month === 11 || month === 0 || month === 1) return 0;               // Summer (December 1 - February 28)
        if ((month === 2 && day < 15) || (month === 4 && day >= 15)) return 4;  // Late Autumn (March 1 - May 14)
        if (month === 2 || month === 3 || (month === 4 && day < 15)) return 1;  // Autumn (March 15 - May 31)
        if ((month === 5 && day < 15) || (month === 7 && day >= 15)) return 5;  // Early Spring (June 1 - August 14)
        if (month === 5 || month === 6 || (month === 7 && day < 15)) return 2;  // Winter (June 15 - August 31)
        if (month === 8 || month === 9 || month === 10) return 3;               // Spring (September 1 - November 30)
    
        // Default to Summer in case of an error
        return 0;
    }
    
    // Set the in-game season based on the detected IRL or forced season.
    public postDBLoad(container: DependencyContainer): void {
        const vfs = container.resolve<VFS>("VFS");
        this.loadConfig(vfs);

        if (!this.config.enable) {
            console.log(`${this.modName} Mod is disabled in config. Skipping season adjustment.`);
            return;
        }

        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const logger = container.resolve<ILogger>("WinstonLogger");
        const weatherConfig: IWeatherConfig = configServer.getConfig(ConfigTypes.WEATHER);

        weatherConfig.overrideSeason = null; // reset initial value to null

        // Check if a forced season is set in the configuration
        if (this.config.forceSeason !== -1) {
            // Apply the forced season
            weatherConfig.overrideSeason = this.config.forceSeason;
            if (this.config.consoleMessages) {
                logger.success(`${this.modName} Forced season selected: ${this.seasonsArray[this.config.forceSeason]}`);
            }
        } else {
            // Automatically detect the IRL season if not forced
            const detectedSeason = this.getRealLifeSeason();
            weatherConfig.overrideSeason = detectedSeason;

            if (this.config.consoleMessages) {
                logger.success(`${this.modName} Automatically selected season: ${this.seasonsArray[detectedSeason]}`);
            }
        }

        this.finalSelectedSeason = weatherConfig.overrideSeason;
    }

    // Check for conflicts if other mods override the selected season.
    public postSptLoad(container: DependencyContainer): void {
        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const logger = container.resolve<ILogger>("WinstonLogger");
        const weatherConfig: IWeatherConfig = configServer.getConfig(ConfigTypes.WEATHER);

        if (!this.config.enable) return;    // skip if the mod is disabled

        if (this.finalSelectedSeason !== weatherConfig.overrideSeason) {
            if (weatherConfig.overrideSeason === null) {
                logger.warning(
                    `${this.modName} Another mod has overridden the selected season. Current season: Auto. Check your load order.`
                );
            } else if (weatherConfig.overrideSeason < this.seasonsArray.length) {
                logger.warning(
                    `${this.modName} Another mod has overridden the selected season. Current season: ${this.seasonsArray[weatherConfig.overrideSeason]}. Check your load order.`
                );
            } else {
                logger.warning(
                    `${this.modName} Another mod has overridden the selected season to an invalid value: ${weatherConfig.overrideSeason}. Check your load order.`
                );
            }
        }
    }
}

export const mod = new SouthernHemisphereSeasons();
