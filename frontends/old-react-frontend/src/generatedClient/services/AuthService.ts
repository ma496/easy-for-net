/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { LoginUserInput } from '../models/LoginUserInput';
import type { LoginUserOutput } from '../models/LoginUserOutput';
import type { RegisterUserInput } from '../models/RegisterUserInput';

import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';

export class AuthService {

    constructor(public readonly httpRequest: BaseHttpRequest) {}

    /**
     * @param requestBody 
     * @returns any Success
     * @throws ApiError
     */
    public postAuthRegister(
requestBody: RegisterUserInput,
): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/auth/register',
            body: requestBody,
            mediaType: 'application/json',
        });
    }

    /**
     * @param requestBody 
     * @returns LoginUserOutput Success
     * @throws ApiError
     */
    public postAuthLogin(
requestBody: LoginUserInput,
): CancelablePromise<LoginUserOutput> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/auth/login',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
            },
        });
    }

}
