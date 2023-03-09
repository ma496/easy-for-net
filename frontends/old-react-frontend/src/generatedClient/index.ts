/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export { AppClient } from './AppClient';

export { ApiError } from './core/ApiError';
export { BaseHttpRequest } from './core/BaseHttpRequest';
export { CancelablePromise, CancelError } from './core/CancelablePromise';
export { OpenAPI } from './core/OpenAPI';
export type { OpenAPIConfig } from './core/OpenAPI';

export type { DateOnly } from './models/DateOnly';
export { DayOfWeek } from './models/DayOfWeek';
export type { LoginUserInput } from './models/LoginUserInput';
export type { LoginUserOutput } from './models/LoginUserOutput';
export type { RegisterUserInput } from './models/RegisterUserInput';
export type { UserDto } from './models/UserDto';
export type { WeatherForecast } from './models/WeatherForecast';

export { AuthService } from './services/AuthService';
export { CSharpTemplateApiService } from './services/CSharpTemplateApiService';
export { UserService } from './services/UserService';
