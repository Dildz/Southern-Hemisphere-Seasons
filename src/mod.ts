/*
 * Description: 
 * SPT server mod that adjusts the seasons based on the IRL season in the Southern Hemisphere.
 * 
 * Storms have a chance to occur during summer months.
 * Weather is only updated when a raid starts.
 */

import path from "node:path";
import { jsonc } from "jsonc";
import { readFileSync, existsSync } from "fs";
import type { DependencyContainer } from "tsyringe";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import type { ConfigServer } from "@spt/servers/ConfigServer";
import type { IWeatherConfig } from "@spt/models/spt/config/IWeatherConfig";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";
import type { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import type { IPostSptLoadMod } from "@spt/models/external/IPostSptLoadMod";
import { StaticRouterModService } from "@spt/services/mod/staticRouter/StaticRouterModService";
import { WeatherCallbacks } from "@spt/callbacks/WeatherCallbacks";

class Mod implements IPreSptLoadMod, IPostSptLoadMod {
    private weatherConfig: IWeatherConfig | null = null; // store weather config, nullable
    private isFikaInstalled: boolean = false;            // flag to detect FIKA (multiplayer mod)
    
    private modConfig: { enabled: boolean; stormChance: number; forceSeason: number | null } = {
        enabled: true,     // default to enabled
        stormChance: 0.1,  // default storm chance (10%)
        forceSeason: null, // default to no forced season
    };

    public preSptLoad(container: DependencyContainer): void {
        const logger = container.resolve<ILogger>("WinstonLogger");

        // Load mod configuration from config.jsonc
        this.loadModConfig(logger);

        // Check if the mod is enabled
        if (!this.modConfig.enabled) {
            logger.info(`${this.modName} Mod is disabled in config.jsonc`);
            return; // exit early if mod is disabled
        }

        const router = container.resolve<StaticRouterModService>("StaticRouterModService");
        const configServer = container.resolve<ConfigServer>("ConfigServer");

        // Null checks for critical dependencies
        if (!logger || !router || !configServer) {
            if (logger) {
                logger.error(`${this.modName} Failed to resolve critical dependencies`);
            }
            else {
                console.error(`${this.modName} Failed to resolve critical dependencies, including logger`);
            }
            return; // exit early if dependencies are missing
        }

        // Store the weather config with null check
        this.weatherConfig = configServer.getConfig<IWeatherConfig>(ConfigTypes.WEATHER);
        if (!this.weatherConfig) {
            logger.error(`${this.modName} Failed to retrieve weather config`);
            return; // exit early if weather config is unavailable
        }

        // Check if FIKA is installed by attempting to resolve FikaConfig
        try {
            container.resolve("FikaConfig");
            this.isFikaInstalled = true;
            logger.info(`${this.modName} Detected FIKA installation`);
        }
        catch (e) {
            this.isFikaInstalled = false;
            logger.info(`${this.modName} Running in single-player mode (FIKA not detected)`);
        }

        // Register a static router for handling raid creation (FIKA)
        if (this.isFikaInstalled) {
            router.registerStaticRouter(
                "[SHS] FIKA Raid Create",
                [
                    {
                        url: "/fika/raid/create",
                        action: (url: string, info: any, sessionID: string, output: string): string => {
                            if (this.weatherConfig) {
                                this.weatherConfig.overrideSeason = null;   // reset season
                                const season = this.getSeason();            // determine the season
                                this.weatherConfig.overrideSeason = season; // set the season in the weather config
                                logger.success(`${this.modName} Selected Season for FIKA Raid Creation: ${this.seasonsArray[season]}`);
                                console.log(`[SHS] Weather set to: ${this.seasonsArray[season]}`); // FIKA console log
                            }
                            else {
                                logger.error(`${this.modName} Weather config is null, cannot set season for FIKA raid`);
                            }
                            return output; // return unchanged output
                        },
                    },
                ],
                "fika-raid-create"
            );
        }
        else {
            // For single-player (SPT), hook into raid start event
            const weatherCallbacks = container.resolve<WeatherCallbacks>("WeatherCallbacks");
            if (!weatherCallbacks) {
                logger.error(`${this.modName} Failed to resolve WeatherCallbacks`);
                return;
            }

            // Override the getWeather method to set the season only once at raid start
            const originalGetWeather = weatherCallbacks.getWeather.bind(weatherCallbacks);
            weatherCallbacks.getWeather = (url: string, info: any, sessionID: string): any => {
                if (this.weatherConfig) {
                    this.weatherConfig.overrideSeason = null;   // reset season
                    const season = this.getSeason();            // determine the season
                    this.weatherConfig.overrideSeason = season; // set the season in the weather config
                    logger.success(`${this.modName} Selected Season for SPT Raid Start: ${this.seasonsArray[season]}`);
                }
                else {
                    logger.error(`${this.modName} Weather config is null, cannot set season for SPT raid`);
                }
                return originalGetWeather(url, info, sessionID); // call the original method
            };
        }
    }

    public postSptLoad(container: DependencyContainer): void {
        const logger = container.resolve<ILogger>("WinstonLogger");
        if (!logger) {
            console.error(`${this.modName} Logger unavailable in postSptLoad`);
        }
        else {
            logger.info(`${this.modName} Mod loaded successfully`);
        }
    }

    private modName = "[Southern-Hemisphere-Seasons]";

    // Array mapping season indices to their names
    private seasonsArray = [
        "Summer",       // 0
        "Autumn",       // 1
        "Winter",       // 2
        "Spring",       // 3
        "Late Autumn",  // 4
        "Early Spring", // 5
        "Storm",        // 6
    ];

    /**
     * Loads the mod configuration from config.jsonc.
     * If the file is missing or invalid, defaults are used.
     */
    private loadModConfig(logger: ILogger): void {
        const configPath = path.resolve(__dirname, "../config/config.jsonc");

        if (!existsSync(configPath)) {
            logger.warning(`${this.modName} Config file not found at ${configPath}, using defaults`);
            return;
        }

        try {
            const config = jsonc.parse(readFileSync(configPath, "utf8"));
            if (typeof config.enabled === "boolean") {
                this.modConfig.enabled = config.enabled;
            }

            if (typeof config.stormChance === "number" && config.stormChance >= 0 && config.stormChance <= 1) {
                this.modConfig.stormChance = config.stormChance;
            }
            else {
                logger.warning(`${this.modName} Invalid stormChance value in config, using default (0.1)`);
            }

            if (typeof config.forceSeason === "number" && config.forceSeason >= 0 && config.forceSeason <= 6) {
                this.modConfig.forceSeason = config.forceSeason;
            }
            else if (config.forceSeason !== null) {
                logger.warning(`${this.modName} Invalid forceSeason value in config, must be between 0 and 6 or null`);
            }
        }
        catch (e) {
            logger.error(`${this.modName} Error parsing config file: ${e instanceof Error ? e.message : String(e)}`);
        }
    }

    /**
     * Determines the current season.
     * If forceSeason is set in the config, it will override the date-based logic.
     * Otherwise, the season is determined based on the system date.
     */
    private getSeason(): number {
        if (this.modConfig.forceSeason !== null) {
            return this.modConfig.forceSeason; // return the forced season
        }
        return this.getSeasonBasedOnDate(); // otherwise, use the date-based logic
    }

    /**
     * Determines the current season based on the system date.
     * Seasons are defined according to the Southern Hemisphere:
     * - Summer:        December 1 - February 28
     * - Autumn:        March 1 - April 30
     * - Late Autumn:   May 1 - May 31
     * - Winter:        June 1 - August 31
     * - Early Spring:  September 1 - September 30
     * - Spring:        October 1 - November 30
     * - Storm:         Random chance during Summer months (configurable via stormChance)
     */
    private getSeasonBasedOnDate(): number {
        const now = new Date();
        const month = now.getMonth() + 1; // getMonth() returns 0-11, so add 1
        const day = now.getDate();

        // Summer: December 1 - February 28
        if ((month === 12 && day >= 1) || month === 1 || month === 2) {
            // Random chance for Storm during Summer
            if (Math.random() < this.modConfig.stormChance) {
                return 6; // Storm
            }
            return 0; // Summer
        }
        // Autumn: March 1 - April 30
        else if (month === 3 || month === 4) {
            return 1; // Autumn
        }
        // Late Autumn: May 1 - May 31
        else if (month === 5) {
            return 4; // Late Autumn
        }
        // Winter: June 1 - August 31
        else if (month === 6 || month === 7 || month === 8) {
            return 2; // Winter
        }
        // Early Spring: September 1 - September 30
        else if (month === 9 && day <= 30) {
            return 5; // Early Spring
        }
        // Spring: October 1 - November 30
        else if (month === 10 || month === 11) {
            return 3; // Spring
        }

        // Default to Summer if no season matches (should not happen)
        return 0;
    }
}

export const mod = new Mod();