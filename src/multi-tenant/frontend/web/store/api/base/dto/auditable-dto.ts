import { BaseDto } from './base-dto'

/** Common creation audit fields (timestamp and creator) shared by auditable DTOs. */
export interface CreatableDto {
  createdAt: string
  createdBy: string
}

/** Common update audit fields (timestamp and last updater) shared by auditable DTOs. */
export interface UpdatableDto {
  updatedAt: string
  updatedBy: string
}

/** DTO that captures both creation and last-update audit information. */
export interface AuditableDto extends CreatableDto, UpdatableDto { }

/** Identifier-typed DTO that also includes creation audit fields. */
export interface GenericCreatableDto<TId> extends CreatableDto, BaseDto<TId> { }

/** Identifier-typed DTO that also includes last-update audit fields. */
export interface GenericUpdatableDto<TId> extends UpdatableDto, BaseDto<TId> { }

/** Identifier-typed DTO that includes both creation and last-update audit fields. */
export interface GenericAuditableDto<TId> extends AuditableDto, BaseDto<TId> { }
