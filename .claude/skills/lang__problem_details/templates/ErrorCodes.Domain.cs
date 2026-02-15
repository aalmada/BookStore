namespace BookStore.Shared.Models;

public static class ErrorCodes
{
    // {Domain} Codes
    public static class {Domain}
    {
        // Creation/Existence
        public const string IdRequired = "ERR_{DOMAIN}_ID_REQUIRED";
        public const string NotFound = "ERR_{DOMAIN}_NOT_FOUND";
        public const string AlreadyExists = "ERR_{DOMAIN}_ALREADY_EXISTS";
        public const string AlreadyDeleted = "ERR_{DOMAIN}_ALREADY_DELETED";
        public const string NotDeleted = "ERR_{DOMAIN}_NOT_DELETED";

        // Validation - Basic Fields
        public const string NameRequired = "ERR_{DOMAIN}_NAME_REQUIRED";
        public const string NameTooLong = "ERR_{DOMAIN}_NAME_TOO_LONG";
        public const string DescriptionRequired = "ERR_{DOMAIN}_DESCRIPTION_REQUIRED";
        public const string DescriptionTooLong = "ERR_{DOMAIN}_DESCRIPTION_TOO_LONG";

        // Validation - Locale/Translation
        public const string LanguageInvalid = "ERR_{DOMAIN}_LANGUAGE_INVALID";
        public const string TranslationsRequired = "ERR_{DOMAIN}_TRANSLATIONS_REQUIRED";
        public const string TranslationLanguageInvalid = "ERR_{DOMAIN}_TRANSLATION_LANGUAGE_INVALID";
        public const string DefaultTranslationRequired = "ERR_{DOMAIN}_DEFAULT_TRANSLATION_REQUIRED";

        // Concurrency
        public const string ConcurrencyConflict = "ERR_{DOMAIN}_CONCURRENCY_CONFLICT";

        // TODO: Add domain-specific error codes here
    }
}
