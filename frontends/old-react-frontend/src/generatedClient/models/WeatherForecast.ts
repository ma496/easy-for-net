/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { DateOnly } from './DateOnly';

export type WeatherForecast = {
    date?: DateOnly;
    temperatureC?: number;
    summary?: string | null;
    readonly temperatureF?: number;
};
