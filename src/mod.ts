/*
 * Description: 
 * SPT-Fika server mod that adjusts the weather settings based on the IRL season in the southern hemisphere.
 *
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
    private config: { enable: boolean; enableStorms: boolean; consoleMessages: boolean };

    constructor() {
        this.config = { enable: true, enableStorms: false, consoleMessages: true };  // default config values
    }

    // Load the mod configuration from `config.jsonc`.
    private loadConfig(vfs: VFS): void {
        try {
            const configPath = path.resolve(__dirname, "../config/config.jsonc");
            this.config = jsonc.parse(vfs.readFile(configPath));
        } catch (error) {
            console.error(`${this.modName} Failed to load config.jsonc:`, error);
            this.config = { enable: true, enableStorms: false, consoleMessages: true };  // default values if an error occurs
        }
    }

    // Determine the real-life season based on the Southern Hemisphere calendar.
    private getRealLifeSeason(): number {
        const now = new Date();
        const month = now.getMonth(); // 0 = January, 11 = December
        const day = now.getDate();    // Day of the month
    
        // Check if storms are enabled
        if (this.config.enableStorms) {
            // Southern Hemisphere season mapping with Storm
            if ((month === 4 && day >= 15) || (month === 5 && day < 15)) return 6;  // Storm
        }
    
        // Southern Hemisphere season mapping without Storm
        if (month === 11 || month === 0 || month === 1) return 0;               // Summer
        if ((month === 2 && day < 15) || (month === 4 && day >= 15)) return 4;  // Late Autumn
        if (month === 2 || month === 3 || (month === 4 && day < 15)) return 1;  // Autumn
        if ((month === 5 && day < 15) || (month === 7 && day >= 15)) return 5;  // Early Spring
        if (month === 5 || month === 6 || (month === 7 && day < 15)) return 2;  // Winter
        if (month === 8 || month === 9 || month === 10) return 3;               // Spring
    
        // Default to Summer in case of an error
        return 0;
    }
    
    // Set the in-game season based on the detected real-life season.
    public postDBLoad(container: DependencyContainer): void {
        const vfs = container.resolve<VFS>("VFS");
        this.loadConfig(vfs);

        if (!this.config.enable) {
            console.log(`${this.modName} Mod is disabled in config.jsonc. Skipping season adjustment.`);
            return;
        }

        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const logger = container.resolve<ILogger>("WinstonLogger");
        const weatherConfig: IWeatherConfig = configServer.getConfig(ConfigTypes.WEATHER);

        weatherConfig.overrideSeason = null;    // reset initial value to null

        // Detect the real-life season and update the weather configuration
        const detectedSeason = this.getRealLifeSeason();
        weatherConfig.overrideSeason = detectedSeason;

        if (this.config.consoleMessages) {
            logger.success(
                `${this.modName} Automatically selected season: ${this.seasonsArray[detectedSeason]}`
            );
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
