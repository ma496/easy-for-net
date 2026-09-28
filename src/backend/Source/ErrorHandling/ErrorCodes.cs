namespace Backend.ErrorHandling;

/// <summary>
/// Central catalog of stable, machine-readable error codes returned to API
/// clients as part of problem-details responses.
/// </summary>
public static class ErrorCodes
{
    public static readonly ErrorCode InternalServerError = new("internalServerError");
    public static readonly ErrorCode DuplicateValue = new("duplicateValue");
    public static readonly ErrorCode DuplicatePropertyValue = new("duplicatePropertyValue");
    public static readonly ErrorCode RequiredFieldMissing = new("requiredFieldMissing");
    public static readonly ErrorCode RequiredPropertyFieldMissing = new("requiredPropertyFieldMissing");
    public static readonly ErrorCode ReferencedRecordNotFound = new("referencedRecordNotFound");
    public static readonly ErrorCode InvalidValueProvided = new("invalidValueProvided");
    public static readonly ErrorCode DatabaseError = new("databaseError");
    public static readonly ErrorCode AuthenticationRequired = new("authenticationRequired");
    public static readonly ErrorCode PermissionDenied = new("permissionDenied");
    public static readonly ErrorCode InvalidCurrentPassword = new("invalidCurrentPassword");
    public static readonly ErrorCode InvalidToken = new("invalidToken");
    public static readonly ErrorCode TokenExpired = new("tokenExpired");
    public static readonly ErrorCode PayloadTooLarge = new("payloadTooLarge");
    public static readonly ErrorCode InvalidUsernamePassword = new("invalidUsernamePassword");
    public static readonly ErrorCode InvalidEmailPassword = new("invalidEmailPassword");
    public static readonly ErrorCode UserNotActive = new("userNotActive");
    public static readonly ErrorCode UserNotFound = new("userNotFound");
    public static readonly ErrorCode EmailNotVerified = new("emailNotVerified");
    public static readonly ErrorCode EmailAlreadyExists = new("emailAlreadyExists");
    public static readonly ErrorCode UsernameAlreadyExists = new("usernameAlreadyExists");
    public static readonly ErrorCode SystemCreatedRolePermissionsCannotBeChanged = new("systemCreatedRolePermissionsCannotBeChanged");
    public static readonly ErrorCode SystemCreatedRoleCannotBeDeleted = new("systemCreatedRoleCannotBeDeleted");
    public static readonly ErrorCode SystemCreatedRoleCannotBeUpdated = new("systemCreatedRoleCannotBeUpdated");
    public static readonly ErrorCode SystemCreatedUserCannotBeDeleted = new("systemCreatedUserCannotBeDeleted");
    public static readonly ErrorCode SystemCreatedUserCannotBeUpdated = new("systemCreatedUserCannotBeUpdated");
    public static readonly ErrorCode RoleNotFound = new("roleNotFound");
    public static readonly ErrorCode RoleNameAlreadyExists = new("roleNameAlreadyExists");
    public static readonly ErrorCode TenantNotFound = new("tenantNotFound");
    public static readonly ErrorCode TenantSuspended = new("tenantSuspended");
    public static readonly ErrorCode NotTenantMember = new("notTenantMember");
    public static readonly ErrorCode NoActiveTenant = new("noActiveTenant");
    public static readonly ErrorCode TenantRequired = new("tenantRequired");
    public static readonly ErrorCode TenantIdentifierAlreadyExists = new("tenantIdentifierAlreadyExists");
    public static readonly ErrorCode DuplicateTenantMembership = new("duplicateTenantMembership");
    public static readonly ErrorCode LastTenantAdministrator = new("lastTenantAdministrator");
    public static readonly ErrorCode SystemCreatedTenantCannotBeModified = new("systemCreatedTenantCannotBeModified");
    public static readonly ErrorCode CrossTenantFileAccess = new("crossTenantFileAccess");
    public static readonly ErrorCode PlatformPermissionNotGrantable = new("platformPermissionNotGrantable");
    public static readonly ErrorCode TenantPermissionNotGrantable = new("tenantPermissionNotGrantable");
    public static readonly ErrorCode ConcurrentModification = new("concurrentModification");
    public static readonly ErrorCode UserSharedAcrossTenants = new("userSharedAcrossTenants");
    public static readonly ErrorCode FeatureDisabled = new("featureDisabled");
    public static readonly ErrorCode FeatureLimitExceeded = new("featureLimitExceeded");
    public static readonly ErrorCode FeatureNotFound = new("featureNotFound");
    public static readonly ErrorCode InvalidFeatureValue = new("invalidFeatureValue");
    public static readonly ErrorCode FeatureProviderNotAllowed = new("featureProviderNotAllowed");
    public static readonly ErrorCode UnknownFeatureProvider = new("unknownFeatureProvider");
    public static readonly ErrorCode EditionNotFound = new("editionNotFound");
    public static readonly ErrorCode EditionNameAlreadyExists = new("editionNameAlreadyExists");
    public static readonly ErrorCode EditionInUse = new("editionInUse");
}
