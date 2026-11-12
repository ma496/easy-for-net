/** Minimal DTO contract that every persisted entity implements, exposing a strongly-typed identifier. */
export interface BaseDto<TId> {
  id: TId
}
