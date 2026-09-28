/** DTO that reports whether its row was created by the system itself rather than by a user, so a client can tell in advance that updating or deleting it will be refused. */
export interface SystemCreatedDto {
  systemCreated: boolean
}
