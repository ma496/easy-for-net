/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { UserDto } from '../models/UserDto';

import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';

export class UserService {

    constructor(public readonly httpRequest: BaseHttpRequest) {}

    /**
     * @param id 
     * @returns UserDto Success
     * @throws ApiError
     */
    public getUserGetById(
id: number,
): CancelablePromise<UserDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/user/get-by-id/{id}',
            path: {
                'id': id,
            },
        });
    }

    /**
     * @param email 
     * @returns UserDto Success
     * @throws ApiError
     */
    public getUserGetByEmail(
email: string,
): CancelablePromise<UserDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/user/get-by-email/{email}',
            path: {
                'email': email,
            },
        });
    }

}
