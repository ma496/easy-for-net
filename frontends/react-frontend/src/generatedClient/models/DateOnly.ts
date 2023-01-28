/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { DayOfWeek } from './DayOfWeek';

export type DateOnly = {
    year?: number;
    month?: number;
    day?: number;
    dayOfWeek?: DayOfWeek;
    readonly dayOfYear?: number;
    readonly dayNumber?: number;
};
