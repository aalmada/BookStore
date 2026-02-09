namespace BookStore.Shared.Models;

public static class ErrorCodes
{
    // Book Codes
    public static class Books
    {
        public const string TitleRequired = "ERR_BOOK_TITLE_REQUIRED";
        public const string TitleTooLong = "ERR_BOOK_TITLE_TOO_LONG";
        public const string IsbnEmpty = "ERR_BOOK_ISBN_EMPTY";
        public const string IsbnInvalidFormat = "ERR_BOOK_ISBN_INVALID_FORMAT";
        public const string LanguageRequired = "ERR_BOOK_LANGUAGE_REQUIRED";
        public const string LanguageInvalid = "ERR_BOOK_LANGUAGE_INVALID";
        public const string TranslationsRequired = "ERR_BOOK_TRANSLATIONS_REQUIRED";
        public const string TranslationLanguageInvalid = "ERR_BOOK_TRANSLATION_LANGUAGE_INVALID";
        public const string TranslationValueRequired = "ERR_BOOK_TRANSLATION_VALUE_REQUIRED";
        public const string DescriptionRequired = "ERR_BOOK_DESCRIPTION_REQUIRED";
        public const string DescriptionTooLong = "ERR_BOOK_DESCRIPTION_TOO_LONG";
        public const string PricesRequired = "ERR_BOOK_PRICES_REQUIRED";
        public const string PriceCurrencyInvalid = "ERR_BOOK_PRICE_CURRENCY_INVALID";
        public const string PriceNegative = "ERR_BOOK_PRICE_NEGATIVE";
        public const string AlreadyDeleted = "ERR_BOOK_ALREADY_DELETED";
        public const string NotDeleted = "ERR_BOOK_NOT_DELETED";
        public const string CoverFormatNone = "ERR_BOOK_COVER_FORMAT_NONE";
        public const string DefaultTranslationRequired = "ERR_BOOK_DEFAULT_TRANSLATION_REQUIRED";
        public const string DefaultPriceRequired = "ERR_BOOK_DEFAULT_PRICE_REQUIRED";
        public const string SaleOverlap = "ERR_BOOK_SALE_OVERLAP";
        public const string SaleNotFound = "ERR_BOOK_SALE_NOT_FOUND";
        public const string BookNotFound = "ERR_BOOK_NOT_FOUND";
        public const string RatingInvalid = "ERR_BOOK_RATING_INVALID";
        public const string UserNotFound = "ERR_BOOK_USER_NOT_FOUND";
        public const string ConcurrencyConflict = "ERR_BOOK_CONCURRENCY_CONFLICT";
    }

    // Author Codes
    public static class Authors
    {
        public const string NameRequired = "ERR_AUTHOR_NAME_REQUIRED";
        public const string NameTooLong = "ERR_AUTHOR_NAME_TOO_LONG";
        public const string TranslationsRequired = "ERR_AUTHOR_TRANSLATIONS_REQUIRED";
        public const string TranslationLanguageInvalid = "ERR_AUTHOR_TRANSLATION_LANGUAGE_INVALID";
        public const string TranslationValueRequired = "ERR_AUTHOR_TRANSLATION_VALUE_REQUIRED";
        public const string BiographyRequired = "ERR_AUTHOR_BIOGRAPHY_REQUIRED";
        public const string BiographyTooLong = "ERR_AUTHOR_BIOGRAPHY_TOO_LONG";
        public const string AlreadyDeleted = "ERR_AUTHOR_ALREADY_DELETED";
        public const string NotDeleted = "ERR_AUTHOR_NOT_DELETED";
        public const string DefaultTranslationRequired = "ERR_AUTHOR_DEFAULT_TRANSLATION_REQUIRED";
        public const string ConcurrencyConflict = "ERR_AUTHOR_CONCURRENCY_CONFLICT"; // Already added, ensuring keeps check
    }

    // Category Codes
    public static class Categories
    {
        public const string TranslationsRequired = "ERR_CATEGORY_TRANSLATIONS_REQUIRED";
        public const string TranslationLanguageInvalid = "ERR_CATEGORY_TRANSLATION_LANGUAGE_INVALID";
        public const string TranslationValueRequired = "ERR_CATEGORY_TRANSLATION_VALUE_REQUIRED";
        public const string NameRequired = "ERR_CATEGORY_NAME_REQUIRED";
        public const string NameTooLong = "ERR_CATEGORY_NAME_TOO_LONG";
        public const string AlreadyDeleted = "ERR_CATEGORY_ALREADY_DELETED";
        public const string NotDeleted = "ERR_CATEGORY_NOT_DELETED";
        public const string DefaultTranslationRequired = "ERR_CATEGORY_DEFAULT_TRANSLATION_REQUIRED";
        public const string ConcurrencyConflict = "ERR_CATEGORY_CONCURRENCY_CONFLICT";
    }

    // Publisher Codes
    public static class Publishers
    {
        public const string NameRequired = "ERR_PUBLISHER_NAME_REQUIRED";
        public const string NameTooLong = "ERR_PUBLISHER_NAME_TOO_LONG";
        public const string AlreadyDeleted = "ERR_PUBLISHER_ALREADY_DELETED";
        public const string NotDeleted = "ERR_PUBLISHER_NOT_DELETED";
    }

    // Authentication Codes
    public static class Auth
    {
        public const string InvalidCredentials = "ERR_AUTH_INVALID_CREDENTIALS";
        public const string LockedOut = "ERR_AUTH_LOCKED_OUT";
        public const string NotAllowed = "ERR_AUTH_NOT_ALLOWED";
        public const string EmailUnconfirmed = "ERR_AUTH_EMAIL_UNCONFIRMED";
        public const string DuplicateEmail = "ERR_AUTH_DUPLICATE_EMAIL";
        public const string InvalidToken = "ERR_AUTH_INVALID_TOKEN";
        public const string TokenExpired = "ERR_AUTH_TOKEN_EXPIRED";
        public const string CrossTenantIdentity = "ERR_AUTH_CROSS_TENANT_IDENTITY";
        public const string PasswordMismatch = "ERR_AUTH_PASSWORD_MISMATCH";
        public const string PasswordReuse = "ERR_AUTH_PASSWORD_REUSE";
        public const string RegistrationDisabled = "ERR_AUTH_REGISTRATION_DISABLED";
        public const string VerificationDisabled = "ERR_AUTH_VERIFICATION_DISABLED";
        public const string RateLimitExceeded = "ERR_AUTH_RATE_LIMIT_EXCEEDED";
        public const string InvalidRequest = "ERR_AUTH_INVALID_REQUEST";
        public const string RequestFailed = "ERR_AUTH_REQUEST_FAILED";
    }

    // Shopping Cart Codes
    public static class Cart
    {
        public const string ItemNotFound = "ERR_CART_ITEM_NOT_FOUND";
        public const string InvalidQuantity = "ERR_CART_INVALID_QUANTITY";
        public const string QuantityExceeded = "ERR_CART_QUANTITY_EXCEEDED";
        public const string BookNotFound = "ERR_CART_BOOK_NOT_FOUND";
        public const string UserNotFound = "ERR_CART_USER_NOT_FOUND";
    }

    // Admin Codes
    public static class Admin
    {
        public const string CannotPromoteSelf = "ERR_ADMIN_CANNOT_PROMOTE_SELF";
        public const string CannotDemoteSelf = "ERR_ADMIN_CANNOT_DEMOTE_SELF";
        public const string UserNotFound = "ERR_ADMIN_USER_NOT_FOUND";
        public const string AlreadyAdmin = "ERR_ADMIN_ALREADY_ADMIN";
        public const string NotAdmin = "ERR_ADMIN_NOT_ADMIN";
        public const string FileEmpty = "ERR_ADMIN_FILE_EMPTY";
        public const string FileTooLarge = "ERR_ADMIN_FILE_TOO_LARGE";
        public const string InvalidFileType = "ERR_ADMIN_INVALID_FILE_TYPE";
    }

    // Tenancy Codes
    public static class Tenancy
    {
        public const string TenantIdRequired = "ERR_TENANT_ID_REQUIRED";
        public const string InvalidTenantId = "ERR_TENANT_ID_INVALID";
        public const string InvalidAdminEmail = "ERR_TENANT_ADMIN_EMAIL_INVALID";
        public const string InvalidAdminPassword = "ERR_TENANT_ADMIN_PASSWORD_INVALID";
        public const string TenantAlreadyExists = "ERR_TENANT_ALREADY_EXISTS";
        public const string TenantNotFound = "ERR_TENANT_NOT_FOUND";
        public const string AccessDenied = "ERR_TENANT_ACCESS_DENIED";
    }

    // Passkey Codes
    public static class Passkey
    {
        public const string EmailRequired = "ERR_PASSKEY_EMAIL_REQUIRED";
        public const string AttestationFailed = "ERR_PASSKEY_ATTESTATION_FAILED";
        public const string AssertionFailed = "ERR_PASSKEY_ASSERTION_FAILED";
        public const string IdAlreadyExists = "ERR_PASSKEY_ID_ALREADY_EXISTS";
        public const string UserNotFound = "ERR_PASSKEY_USER_NOT_FOUND";
        public const string LastPasskey = "ERR_PASSKEY_LAST_ONE";
        public const string StoreNotAvailable = "ERR_PASSKEY_STORE_NOT_AVAILABLE";
        public const string InvalidFormat = "ERR_PASSKEY_INVALID_FORMAT";
        public const string InvalidCredential = "ERR_PASSKEY_INVALID_CREDENTIAL";
    }
}
