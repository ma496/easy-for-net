/** Request payload for the refresh-token endpoint, identifying the user and supplying the current refresh token. */
export interface TokenRequest {
  userId: string
  refreshToken: string
}
