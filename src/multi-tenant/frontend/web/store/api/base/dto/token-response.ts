/** Response returned from the refresh-token endpoint, containing the new access/refresh tokens and the user id. */
export interface TokenResponse {
  userId: string
  refreshToken: string
  accessToken: string
}
