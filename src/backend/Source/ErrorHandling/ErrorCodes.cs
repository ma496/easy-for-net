namespace Backend.ErrorHandling;

/// <summary>
/// Central catalog of stable, machine-readable error codes returned to API
/// clients as part of problem-details responses.
/// </summary>
public static class ErrorCodes
{
    public const string InternalServerError = "internalServerError";
    public const string DuplicateValue = "duplicateValue";
    public const string DuplicatePropertyValue = "duplicatePropertyValue";
    public const string RequiredFieldMissing = "requiredFieldMissing";
    public const string RequiredPropertyFieldMissing = "requiredPropertyFieldMissing";
    public const string ReferencedRecordNotFound = "referencedRecordNotFound";
    public const string InvalidValueProvided = "invalidValueProvided";
    public const string DatabaseError = "databaseError";
    public const string AuthenticationRequired = "authenticationRequired";
    public const string PermissionDenied = "permissionDenied";
    public const string InvalidCurrentPassword = "invalidCurrentPassword";
    public const string InvalidToken = "invalidToken";
    public const string TokenExpired = "tokenExpired";
    public const string PayloadTooLarge = "payloadTooLarge";
    public const string InvalidUsernamePassword = "invalidUsernamePassword";
    public const string InvalidEmailPassword = "invalidEmailPassword";
    public const string UserNotActive = "userNotActive";
    public const string UserNotFound = "userNotFound";
    public const string EmailNotVerified = "emailNotVerified";
    public const string EmailAlreadyExists = "emailAlreadyExists";
    public const string UsernameAlreadyExists = "usernameAlreadyExists";
    public const string SystemCreatedRolePermissionsCannotBeChanged = "systemCreatedRolePermissionsCannotBeChanged";
    public const string SystemCreatedRoleCannotBeDeleted = "systemCreatedRoleCannotBeDeleted";
    public const string SystemCreatedRoleCannotBeUpdated = "systemCreatedRoleCannotBeUpdated";
    public const string SystemCreatedUserCannotBeDeleted = "systemCreatedUserCannotBeDeleted";
    public const string SystemCreatedUserCannotBeUpdated = "systemCreatedUserCannotBeUpdated";
    public const string RoleNotFound = "roleNotFound";
    public const string RoleNameAlreadyExists = "roleNameAlreadyExists";
    public const string TenantNotFound = "tenantNotFound";
    public const string TenantSuspended = "tenantSuspended";
    public const string NotTenantMember = "notTenantMember";
    public const string NoActiveTenant = "noActiveTenant";
    public const string TenantRequired = "tenantRequired";
    public const string TenantIdentifierAlreadyExists = "tenantIdentifierAlreadyExists";
    public const string DuplicateTenantMembership = "duplicateTenantMembership";
    public const string LastTenantAdministrator = "lastTenantAdministrator";
    public const string SystemCreatedTenantCannotBeModified = "systemCreatedTenantCannotBeModified";
    public const string CrossTenantFileAccess = "crossTenantFileAccess";
    public const string PlatformPermissionNotGrantable = "platformPermissionNotGrantable";
    public const string ConcurrentModification = "concurrentModification";
    public const string UserSharedAcrossTenants = "userSharedAcrossTenants";
}
