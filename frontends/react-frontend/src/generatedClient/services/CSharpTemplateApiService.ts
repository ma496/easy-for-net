/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WeatherForecast } from '../models/WeatherForecast';

import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';

export class CSharpTemplateApiService {

    constructor(public readonly httpRequest: BaseHttpRequest) {}

    /**
     * @returns WeatherForecast Success
     * @throws ApiError
     */
    public getWeatherForecast(): CancelablePromise<Array<WeatherForecast>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/weatherforecast',
        });
    }

}
