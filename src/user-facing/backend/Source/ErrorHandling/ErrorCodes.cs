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
    public static readonly ErrorCode SessionStoreUnavailable = new("sessionStoreUnavailable");
    public static readonly ErrorCode SettingNotFound = new("settingNotFound");
    public static readonly ErrorCode SettingPropertyUnknown = new("settingPropertyUnknown");
    public static readonly ErrorCode SettingPropertyInvalid = new("settingPropertyInvalid");
    public static readonly ErrorCode SettingValueInvalid = new("settingValueInvalid");
    public static readonly ErrorCode SignupDisabled = new("signupDisabled");
    public static readonly ErrorCode EmailSmtpServerRequired = new("emailSmtpServerRequired");
    public static readonly ErrorCode EmailSmtpPortInvalid = new("emailSmtpPortInvalid");
    public static readonly ErrorCode EmailSenderEmailInvalid = new("emailSenderEmailInvalid");
    public static readonly ErrorCode SeoPageNotFound = new("seoPageNotFound");
    public static readonly ErrorCode SeoPageAlreadyExists = new("seoPageAlreadyExists");
    public static readonly ErrorCode InvalidJsonLd = new("invalidJsonLd");
}
