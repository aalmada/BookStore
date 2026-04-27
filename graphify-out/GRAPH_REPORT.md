# Graph Report - BookStore  (2026-04-25)

## Corpus Check
- 415 files · ~229,292 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2738 nodes · 5304 edges · 99 communities detected
- Extraction: 55% EXTRACTED · 45% INFERRED · 0% AMBIGUOUS · INFERRED: 2386 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]
- [[_COMMUNITY_Community 49|Community 49]]
- [[_COMMUNITY_Community 50|Community 50]]
- [[_COMMUNITY_Community 51|Community 51]]
- [[_COMMUNITY_Community 52|Community 52]]
- [[_COMMUNITY_Community 53|Community 53]]
- [[_COMMUNITY_Community 54|Community 54]]
- [[_COMMUNITY_Community 55|Community 55]]
- [[_COMMUNITY_Community 56|Community 56]]
- [[_COMMUNITY_Community 57|Community 57]]
- [[_COMMUNITY_Community 58|Community 58]]
- [[_COMMUNITY_Community 59|Community 59]]
- [[_COMMUNITY_Community 60|Community 60]]
- [[_COMMUNITY_Community 61|Community 61]]
- [[_COMMUNITY_Community 62|Community 62]]
- [[_COMMUNITY_Community 63|Community 63]]
- [[_COMMUNITY_Community 65|Community 65]]
- [[_COMMUNITY_Community 66|Community 66]]
- [[_COMMUNITY_Community 67|Community 67]]
- [[_COMMUNITY_Community 68|Community 68]]
- [[_COMMUNITY_Community 69|Community 69]]
- [[_COMMUNITY_Community 70|Community 70]]
- [[_COMMUNITY_Community 71|Community 71]]
- [[_COMMUNITY_Community 72|Community 72]]
- [[_COMMUNITY_Community 73|Community 73]]
- [[_COMMUNITY_Community 74|Community 74]]
- [[_COMMUNITY_Community 75|Community 75]]
- [[_COMMUNITY_Community 76|Community 76]]
- [[_COMMUNITY_Community 77|Community 77]]
- [[_COMMUNITY_Community 78|Community 78]]
- [[_COMMUNITY_Community 79|Community 79]]
- [[_COMMUNITY_Community 80|Community 80]]
- [[_COMMUNITY_Community 81|Community 81]]
- [[_COMMUNITY_Community 83|Community 83]]
- [[_COMMUNITY_Community 84|Community 84]]
- [[_COMMUNITY_Community 85|Community 85]]
- [[_COMMUNITY_Community 88|Community 88]]
- [[_COMMUNITY_Community 89|Community 89]]
- [[_COMMUNITY_Community 91|Community 91]]
- [[_COMMUNITY_Community 92|Community 92]]
- [[_COMMUNITY_Community 93|Community 93]]
- [[_COMMUNITY_Community 94|Community 94]]
- [[_COMMUNITY_Community 95|Community 95]]
- [[_COMMUNITY_Community 96|Community 96]]
- [[_COMMUNITY_Community 97|Community 97]]
- [[_COMMUNITY_Community 98|Community 98]]
- [[_COMMUNITY_Community 99|Community 99]]
- [[_COMMUNITY_Community 158|Community 158]]
- [[_COMMUNITY_Community 159|Community 159]]
- [[_COMMUNITY_Community 160|Community 160]]
- [[_COMMUNITY_Community 161|Community 161]]
- [[_COMMUNITY_Community 162|Community 162]]

## God Nodes (most connected - your core abstractions)
1. `ToString()` - 78 edges
2. `MartenUserStore` - 55 edges
3. `ok()` - 53 edges
4. `Validation()` - 53 edges
5. `Users` - 41 edges
6. `Log` - 36 edges
7. `Infrastructure` - 33 edges
8. `UserCommandHandlerTests` - 31 edges
9. `NotFound()` - 30 edges
10. `ProjectionCommitListener` - 29 edges

## Surprising Connections (you probably didn't know these)
- `Passkey/WebAuthn Authentication` --semantically_similar_to--> `Keycloak Identity Provider`  [INFERRED] [semantically similar]
  docs/guides/passkey-guide.md → README.md
- `Passkey Authentication Guide` --references--> `Keycloak Identity Provider`  [INFERRED]
  docs/guides/passkey-guide.md → README.md
- `Event Sourcing Pattern` --conceptually_related_to--> `Modular Monolith Architecture`  [INFERRED]
  docs/architecture.md → README.md
- `CQRS Pattern` --conceptually_related_to--> `Modular Monolith Architecture`  [INFERRED]
  docs/architecture.md → README.md
- `ETag Optimistic Concurrency` --conceptually_related_to--> `Multi-Tenancy Data Isolation`  [INFERRED]
  docs/guides/etag-guide.md → README.md

## Hyperedges (group relationships)
- **Event-Sourced CQRS Stack: Event Sourcing + CQRS + Wolverine + Marten** — arch_event_sourcing, arch_cqrs, tech_wolverine, tech_marten [EXTRACTED 0.95]
- **AI Agent Governance: AGENTS.md + Copilot Skills + Roslyn Analyzers** — system_agents_md, system_copilot_skills, tech_roslyn_analyzers [EXTRACTED 0.90]
- **Integration Test Stack: TUnit + Aspire.Hosting.Testing + Bogus** — tech_tunit, tech_aspire_testing, tech_bogus [EXTRACTED 0.90]
- **Real-Time Cache Invalidation and SSE Fan-Out Flow** — concept_marten_commit_listener, concept_cache_tags, concept_sse_notifications, concept_async_daemon [EXTRACTED 0.95]
- **Tenant-Aware Localized Caching Pattern** — concept_hybridcache, concept_cache_tags, concept_get_or_create_localized, concept_conjoined_tenancy [INFERRED 0.85]
- **Event Sourcing Core Triad (Events, Streams, Aggregates)** — concept_event_sourcing_pattern, concept_streams, concept_aggregates, concept_marten_event_store [EXTRACTED 0.92]

## Communities

### Community 0 - "Community 0"
Cohesion: 0.03
Nodes (25): AccountIsolationTests, AccountLockoutTests, AdminTenantTests, AdminUserTests, AuthenticationHelpers, AuthTests, ConfigurationEndpointsTests, CrossTenantAuthenticationTests (+17 more)

### Community 1 - "Community 1"
Cohesion: 0.03
Nodes (25): BookConcurrencyTests, BookCrudTests, BookFilterRegressionTests, BookHelpers, BookRatingTests, BookSoftDeleteTests, BookValidationTests, CatalogService (+17 more)

### Community 2 - "Community 2"
Cohesion: 0.02
Nodes (40): AdminAuthorEndpoints, BookStore.ApiService.Commands, BookStore.ApiService.Endpoints.Admin, AdminBookEndpoints, BookStore.ApiService.Commands, BookStore.ApiService.Endpoints.Admin, AdminCategoryEndpoints, BookStore.ApiService.Commands (+32 more)

### Community 3 - "Community 3"
Cohesion: 0.03
Nodes (32): AuthenticationService, AuthorAggregate, BookAggregate, CategoryAggregate, CheckoutSessionEndpoints, CheckoutSessionHandlers, EmailValidator, Conflict() (+24 more)

### Community 4 - "Community 4"
Cohesion: 0.02
Nodes (27): addItem(), clear(), getCart(), getCount(), getItems(), normalizeQuantity(), removeItem(), setCart() (+19 more)

### Community 5 - "Community 5"
Cohesion: 0.02
Nodes (26): AggregateApplyMethodAnalyzer, AggregateRulesAnalyzer, ApplicationServicesExtensions, ApplicationServicesExtensionsTests, TestWebHostEnvironment, CacheTags, CodeFixProvider, CommandMustBeRecordAnalyzer (+18 more)

### Community 6 - "Community 6"
Cohesion: 0.03
Nodes (17): AuthorCrudTests, AuthorHelpers, CategoryConcurrencyTests, CategoryCrudTests, CategoryHelpers, CategoryOrderingTests, ConcurrencyTests, DebugRefit (+9 more)

### Community 7 - "Community 7"
Cohesion: 0.02
Nodes (20): AuthorHandlers, BookHandlers, BookPriceHandlers, CategoryHandlers, CultureCache, CultureCacheTests, CultureValidator, Authors (+12 more)

### Community 8 - "Community 8"
Cohesion: 0.03
Nodes (19): AuthBroadcast, AuthorHandlerTests, BookHandlerTests, CategoryHandlerTests, EmailHandlers, EmailHandlersTests, EmailTemplateService, HandlerTestBase (+11 more)

### Community 9 - "Community 9"
Cohesion: 0.03
Nodes (13): CoverGenerator, CoverGeneratorTests, DatabaseExtensions, DatabaseSeeder, GlobalExceptionHandler, IChangeListener, IDocumentSessionListener, IExceptionHandler (+5 more)

### Community 10 - "Community 10"
Cohesion: 0.03
Nodes (23): AnonymousCartService, BlobStorageService, BlobStorageTests, BookSearchProjection, CachedTenantStore, EventMetadata, EventMetadataService, ITenantStore (+15 more)

### Community 11 - "Community 11"
Cohesion: 0.04
Nodes (12): DeviceNameParser, DeviceNameParserTests, JsonSerializationTests, TestDto, PartialDateTests, ProblemDetailsExtensions, ProblemDetailsExtensionsTests, RateLimitingExtensions (+4 more)

### Community 12 - "Community 12"
Cohesion: 0.03
Nodes (22): AuthenticationStateProvider, BookStoreEventsService, BunitTestContext, HttpClientDataClass, IAsyncDisposable, IAsyncInitializer, IDisposable, JsonConverter (+14 more)

### Community 13 - "Community 13"
Cohesion: 0.03
Nodes (17): AddBookDialogTests, AllLanguageSelectorTests, BookStoreClientExtensions, Iso8601DateTimeOffsetFormatter, BunitTestContext, CurrencySelectorTests, CurrencyService, CurrencyServiceTests (+9 more)

### Community 14 - "Community 14"
Cohesion: 0.06
Nodes (66): Analyzer Rules Guide, ApiService Copilot Instructions, Aspire Deployment Guide, Aspire Orchestration Guide, Authentication Guide, Caching Guide, Client Copilot Instructions, BookStore.Client README (+58 more)

### Community 15 - "Community 15"
Cohesion: 0.05
Nodes (65): Agent Development Guide, API Client Generation Guide, API Conventions Guide, CQRS Pattern, Event Sourcing Pattern, Modular Monolith Architecture, Architecture Overview, CI/CD Pipelines (GitHub Actions) (+57 more)

### Community 16 - "Community 16"
Cohesion: 0.06
Nodes (9): AuthorProjectionTests, AuthorStatisticsProjectionTests, BookSearchProjectionTests, CategoryProjectionTests, CategoryStatisticsProjectionTests, IPagedList, PagedListWrapper, PublisherProjection (+1 more)

### Community 17 - "Community 17"
Cohesion: 0.07
Nodes (8): INotificationService, Log, Notifications, NotificationEndpoints, NotificationEndpointsRateLimitingTests, INotificationService, NotificationService, RedisNotificationService

### Community 18 - "Community 18"
Cohesion: 0.07
Nodes (9): BookDetailsMenuTests, FailingSseHandler, BookStoreEventsServiceTests, GlobalHooks, HttpMessageHandler, IHostedService, JwtAlgorithmResolver, JwtAlgorithmWarningService (+1 more)

### Community 19 - "Community 19"
Cohesion: 0.06
Nodes (9): AuthorizationMessageHandler, BookStoreErrorHandler, BookStoreHeaderHandler, ClientContextService, CorsTests, DefaultTenantAuthHandler, DelegatingHandler, MultiTenantAuthenticationTests (+1 more)

### Community 20 - "Community 20"
Cohesion: 0.08
Nodes (12): AuthorStatistics, AuthorStatisticsGrouper, AuthorStatisticsProjectionBuilder, BookStatisticsProjection, CategoryStatistics, CategoryStatisticsGrouper, CategoryStatisticsProjectionBuilder, IAggregateGrouper (+4 more)

### Community 21 - "Community 21"
Cohesion: 0.12
Nodes (7): CSharpCodeFixTest, CSharpCodeFixVerifier, Test, EventMustBeRecordAnalyzerTests, UseCreateVersion7AnalyzerTests, UseDateTimeOffsetUtcNowAnalyzerTests, UseGenericMathAnalyzerTests

### Community 22 - "Community 22"
Cohesion: 0.1
Nodes (8): AccountCleanupHandlers, BackgroundService, Log, Maintenance, PasskeyAccountRecoveryProblemDetailsTests, StubUserManager, UnverifiedAccountCleanupService, UserManager

### Community 23 - "Community 23"
Cohesion: 0.16
Nodes (3): AggregateFactory, QueryInvalidationService, QueryInvalidationServiceTests

### Community 24 - "Community 24"
Cohesion: 0.12
Nodes (16): BookStore.Client, CreateAuthorRequest, CreateBookRequest, CreateCategoryRequest, CreatePublisherRequest, PasskeyCreationOptionsResponse, PasskeyCreationRequest, PasskeyInfo (+8 more)

### Community 25 - "Community 25"
Cohesion: 0.25
Nodes (1): ConfigurationValidationTests

### Community 26 - "Community 26"
Cohesion: 0.23
Nodes (2): ErrorLocalizationService, ErrorLocalizationServiceTests

### Community 27 - "Community 27"
Cohesion: 0.15
Nodes (12): Admin, Auth, Authors, Books, Cart, Categories, Checkout, ErrorCodes (+4 more)

### Community 28 - "Community 28"
Cohesion: 0.17
Nodes (1): IIdentityClient

### Community 29 - "Community 29"
Cohesion: 0.31
Nodes (8): ApiRequest, ApiResult, fetch(), main(), orchestrate(), print_results(), Parallel API Orchestrator Calls multiple APIs concurrently using asyncio + aioht, Fire all requests in parallel and return results in the same order.

### Community 30 - "Community 30"
Cohesion: 0.22
Nodes (3): CalculateDiscountedPrice(), IsActive(), BookSaleTests

### Community 31 - "Community 31"
Cohesion: 0.31
Nodes (8): ApiRequest, ApiResult, fetch(), main(), orchestrate(), print_results(), Parallel API orchestrator using asyncio + aiohttp.  Usage:     python parallel_a, Fetch all requests in parallel, bounded by a semaphore.

### Community 32 - "Community 32"
Cohesion: 0.22
Nodes (1): UcpProfileTests

### Community 33 - "Community 33"
Cohesion: 0.39
Nodes (1): MartenConfigurationExtensions

### Community 34 - "Community 34"
Cohesion: 0.33
Nodes (3): CSharpAnalyzerTest, CSharpAnalyzerVerifier, Test

### Community 35 - "Community 35"
Cohesion: 0.33
Nodes (2): ForwardedHeadersExtensions, ForwardedHeadersExtensionsTests

### Community 36 - "Community 36"
Cohesion: 0.33
Nodes (2): AuthorizationPolicyExtensions, AuthorizationPolicyExtensionsTests

### Community 37 - "Community 37"
Cohesion: 0.4
Nodes (1): SecurityHeadersTests

### Community 38 - "Community 38"
Cohesion: 0.4
Nodes (2): ETagValidationMiddleware, ETagValidationMiddlewareExtensions

### Community 39 - "Community 39"
Cohesion: 0.53
Nodes (1): Extensions

### Community 40 - "Community 40"
Cohesion: 0.4
Nodes (1): ITenantStore

### Community 41 - "Community 41"
Cohesion: 0.5
Nodes (1): ApiDocumentationTests

### Community 42 - "Community 42"
Cohesion: 0.5
Nodes (1): FrontendTests

### Community 43 - "Community 43"
Cohesion: 0.5
Nodes (1): ISystemClient

### Community 44 - "Community 44"
Cohesion: 0.5
Nodes (1): IOrdersClient

### Community 46 - "Community 46"
Cohesion: 0.5
Nodes (2): IHubFilter, LoggingHubFilter

### Community 47 - "Community 47"
Cohesion: 0.5
Nodes (2): CheckoutSessionAggregate, CheckoutSessionStatus

### Community 48 - "Community 48"
Cohesion: 0.5
Nodes (1): OrderSummaryProjection

### Community 49 - "Community 49"
Cohesion: 0.5
Nodes (1): AuthorProjection

### Community 50 - "Community 50"
Cohesion: 0.5
Nodes (1): CategoryProjection

### Community 51 - "Community 51"
Cohesion: 0.67
Nodes (1): SecurityHeadersMiddleware

### Community 52 - "Community 52"
Cohesion: 0.5
Nodes (2): ITenantContext, TenantContext

### Community 53 - "Community 53"
Cohesion: 0.67
Nodes (1): WolverineConfigurationExtensions

### Community 54 - "Community 54"
Cohesion: 0.5
Nodes (4): Bogus Test Data, NSubstitute Mocking, TUnit Test Framework, Testing Guide

### Community 55 - "Community 55"
Cohesion: 0.67
Nodes (1): InvalidDateTimeNow

### Community 56 - "Community 56"
Cohesion: 0.67
Nodes (1): InvalidDateTimeUtcNow

### Community 57 - "Community 57"
Cohesion: 0.67
Nodes (1): InvalidGuidInMethod

### Community 58 - "Community 58"
Cohesion: 0.67
Nodes (2): AuthorUpdated, BookAdded

### Community 59 - "Community 59"
Cohesion: 0.67
Nodes (1): SecurityHeadersMiddlewareTests

### Community 60 - "Community 60"
Cohesion: 0.67
Nodes (1): HandlerTestBase

### Community 61 - "Community 61"
Cohesion: 0.67
Nodes (1): InfrastructureTests

### Community 62 - "Community 62"
Cohesion: 0.67
Nodes (1): DatabaseTests

### Community 63 - "Community 63"
Cohesion: 0.67
Nodes (1): ISalesClient

### Community 65 - "Community 65"
Cohesion: 0.67
Nodes (2): Icons, UIConstants

### Community 66 - "Community 66"
Cohesion: 0.67
Nodes (1): OrderAggregate

### Community 67 - "Community 67"
Cohesion: 0.67
Nodes (1): WolverineETagMiddleware

### Community 68 - "Community 68"
Cohesion: 0.67
Nodes (1): ITenantContext

### Community 69 - "Community 69"
Cohesion: 0.67
Nodes (1): PagedListExtensions

### Community 70 - "Community 70"
Cohesion: 0.67
Nodes (1): JsonConfigurationExtensions

### Community 71 - "Community 71"
Cohesion: 0.67
Nodes (1): IEmailService

### Community 72 - "Community 72"
Cohesion: 0.67
Nodes (2): AllowAnonymousTenantAttribute, Attribute

### Community 73 - "Community 73"
Cohesion: 0.67
Nodes (2): DiagnosticCategories, DiagnosticIds

### Community 74 - "Community 74"
Cohesion: 0.67
Nodes (3): Server GC Configuration, Tiered Compilation / Dynamic PGO, Performance Guide

### Community 75 - "Community 75"
Cohesion: 1.0
Nodes (1): InvalidDateTimeInField

### Community 76 - "Community 76"
Cohesion: 1.0
Nodes (1): InvalidGuidInField

### Community 77 - "Community 77"
Cohesion: 1.0
Nodes (1): InvalidGuidInProperty

### Community 78 - "Community 78"
Cohesion: 1.0
Nodes (1): AuthorUpdated

### Community 79 - "Community 79"
Cohesion: 1.0
Nodes (1): BookAdded

### Community 80 - "Community 80"
Cohesion: 1.0
Nodes (1): TestConstants

### Community 81 - "Community 81"
Cohesion: 1.0
Nodes (1): UpdateBookRequest

### Community 83 - "Community 83"
Cohesion: 1.0
Nodes (1): Tenant

### Community 84 - "Community 84"
Cohesion: 1.0
Nodes (1): ApplicationUser

### Community 85 - "Community 85"
Cohesion: 1.0
Nodes (1): BookStatistics

### Community 88 - "Community 88"
Cohesion: 1.0
Nodes (1): Instrumentation

### Community 89 - "Community 89"
Cohesion: 1.0
Nodes (1): RateLimitOptions

### Community 91 - "Community 91"
Cohesion: 1.0
Nodes (1): TenantConstants

### Community 92 - "Community 92"
Cohesion: 1.0
Nodes (1): AccountCleanupOptions

### Community 93 - "Community 93"
Cohesion: 1.0
Nodes (1): UcpProfileOptions

### Community 94 - "Community 94"
Cohesion: 1.0
Nodes (1): EmailOptions

### Community 95 - "Community 95"
Cohesion: 1.0
Nodes (1): ResourceNames

### Community 96 - "Community 96"
Cohesion: 1.0
Nodes (1): MultiTenancyConstants

### Community 97 - "Community 97"
Cohesion: 1.0
Nodes (1): IHaveETag

### Community 98 - "Community 98"
Cohesion: 1.0
Nodes (1): IDomainEventNotification

### Community 99 - "Community 99"
Cohesion: 1.0
Nodes (2): PartialDate Value Object, BookStore.Shared README

### Community 158 - "Community 158"
Cohesion: 1.0
Nodes (1): Contributing Guide

### Community 159 - "Community 159"
Cohesion: 1.0
Nodes (1): Getting Started Guide

### Community 160 - "Community 160"
Cohesion: 1.0
Nodes (1): Possible Improvements

### Community 161 - "Community 161"
Cohesion: 1.0
Nodes (1): BookStore Favicon

### Community 162 - "Community 162"
Cohesion: 1.0
Nodes (1): BookStore Hero Banner

## Knowledge Gaps
- **134 isolated node(s):** `DebugRefit`, `Parallel API Orchestrator Calls multiple APIs concurrently using asyncio + aioht`, `Fire all requests in parallel and return results in the same order.`, `ApiRequest`, `Parallel API orchestrator using asyncio + aiohttp.  Usage:     python parallel_a` (+129 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 25`** (15 nodes): `ConfigurationValidationTests`, `.LocalizationOptions_DefaultCultureEmpty_FailsValidation()`, `.LocalizationOptions_DefaultCultureInSupportedCulturesCaseInsensitive_PassesValidation()`, `.LocalizationOptions_DefaultCultureNotInSupportedCultures_FailsValidation()`, `.LocalizationOptions_SupportedCulturesEmpty_FailsValidation()`, `.LocalizationOptions_ValidConfiguration_PassesValidation()`, `.PaginationOptions_DefaultPageSizeGreaterThanMaxPageSize_FailsValidation()`, `.PaginationOptions_DefaultPageSizeNegative_FailsValidation()`, `.PaginationOptions_DefaultPageSizeTooLarge_FailsValidation()`, `.PaginationOptions_DefaultPageSizeZero_FailsValidation()`, `.PaginationOptions_MaxPageSizeNegative_FailsValidation()`, `.PaginationOptions_MaxPageSizeZero_FailsValidation()`, `.PaginationOptions_ValidConfiguration_PassesValidation()`, `.ValidateModel()`, `ConfigurationValidationTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 26`** (14 nodes): `ErrorLocalizationService`, `.GetLocalizedMessage()`, `.IsTechnicalMessage()`, `ErrorLocalizationServiceTests`, `.GetLocalizedMessage_ReturnsConnectionError_WhenFetchOrNetwork()`, `.GetLocalizedMessage_ReturnsDefault_WhenTypeNotFound()`, `.GetLocalizedMessage_ReturnsDefaultError_WhenTechnicalMessageDetected()`, `.GetLocalizedMessage_ReturnsLocalizedCode_WhenCodeExists()`, `.GetLocalizedMessage_ReturnsMessage_WhenCodeAndTypeNotFoundButMessageIsSafe()`, `.GetLocalizedMessage_ReturnsTypeDefault_ForVariousTypes()`, `.GetLocalizedMessage_ReturnsTypeDefault_WhenCodeNotFoundButTypeExists()`, `.GetLocalizedMessage_StringOverload_ReturnsDefault_WhenNullOrEmpty()`, `ErrorLocalizationService.cs`, `ErrorLocalizationServiceTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 28`** (12 nodes): `IIdentityClient`, `.AddPasswordAsync()`, `.ChangePasswordAsync()`, `.ConfirmEmailAsync()`, `.GetPasswordStatusAsync()`, `.LoginAsync()`, `.LogoutAsync()`, `.RefreshTokenAsync()`, `.RegisterAsync()`, `.RemovePasswordAsync()`, `.ResendVerificationAsync()`, `IIdentityClient.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 32`** (9 nodes): `UcpProfileTests.cs`, `UcpProfileTests`, `.GetUcpProfile_ShouldContainCatalogCapability()`, `.GetUcpProfile_ShouldContainCheckoutCapability()`, `.GetUcpProfile_ShouldContainCheckoutRestService()`, `.GetUcpProfile_ShouldContainSimulatedPaymentHandler()`, `.GetUcpProfile_ShouldContainUcpVersion()`, `.GetUcpProfile_ShouldReturnJsonContentType()`, `.GetUcpProfile_ShouldReturnOk()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 33`** (9 nodes): `MartenConfigurationExtensions`, `.AddMartenEventStore()`, `.ConfigureEventMetadata()`, `.ConfigureIndexes()`, `.ConfigureJsonSerialization()`, `.RegisterChangeListeners()`, `.RegisterEventTypes()`, `.RegisterProjections()`, `MartenConfigurationExtensions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 35`** (6 nodes): `ForwardedHeadersExtensions`, `.ConfigureSecureForwardedHeaders()`, `ForwardedHeadersExtensionsTests`, `.ConfigureSecureForwardedHeaders_ShouldApplySecureDefaults()`, `ForwardedHeadersExtensions.cs`, `ForwardedHeadersExtensionsTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 36`** (6 nodes): `AuthorizationPolicyExtensions`, `.AddSystemAdminPolicy()`, `AuthorizationPolicyExtensionsTests`, `.AddSystemAdminPolicy_ShouldRequireAdminRoleAndDefaultTenantClaim()`, `AuthorizationPolicyExtensions.cs`, `AuthorizationPolicyExtensionsTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 37`** (6 nodes): `SecurityHeadersTests`, `.ClassSetup()`, `.DevelopmentEnvironment_ShouldNotEmitHstsHeader()`, `.GetHeaderValue()`, `.GetRequest_ShouldIncludeBaselineSecurityHeaders()`, `SecurityHeadersTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 38`** (6 nodes): `ETagValidationMiddleware`, `.InvokeAsync()`, `.IsUpdateOrDeleteAction()`, `ETagValidationMiddlewareExtensions`, `.UseETagValidation()`, `ETagValidationMiddleware.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 39`** (6 nodes): `Extensions`, `.AddDefaultHealthChecks()`, `.AddOpenTelemetryExporters()`, `.AddServiceDefaults()`, `.ConfigureOpenTelemetry()`, `Extensions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 40`** (5 nodes): `ITenantStore`, `.GetAllTenantsAsync()`, `.InvalidateCacheAsync()`, `.IsValidTenantAsync()`, `ITenantStore.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 41`** (4 nodes): `ApiDocumentationTests`, `.GetOpenApiDocument_ShouldReturnOk()`, `.GetScalarUi_ShouldReturnOk()`, `ApiDocumentationTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 42`** (4 nodes): `FrontendTests`, `.GetWebFrontendHealthCallbackReturnsOk()`, `.GetWebFrontendRoot_ShouldReturnOk()`, `FrontendTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 43`** (4 nodes): `ISystemClient`, `.GetProjectionStatusAsync()`, `.RebuildProjectionsAsync()`, `ISystemClient.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 44`** (4 nodes): `IOrdersClient`, `.GetOrdersAsync()`, `.PlaceOrderAsync()`, `IOrdersClient.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 46`** (4 nodes): `IHubFilter`, `LoggingHubFilter`, `.InvokeMethodAsync()`, `LoggingHubFilter.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 47`** (4 nodes): `CheckoutSessionAggregate`, `.Apply()`, `CheckoutSessionStatus`, `CheckoutSessionAggregate.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 48`** (4 nodes): `OrderSummaryProjection`, `.Apply()`, `.Create()`, `OrderSummaryProjection.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 49`** (4 nodes): `AuthorProjection`, `.Apply()`, `.Create()`, `AuthorProjection.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 50`** (4 nodes): `CategoryProjection`, `.Apply()`, `.Create()`, `CategoryProjection.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 51`** (4 nodes): `SecurityHeadersMiddleware`, `.InvokeAsync()`, `.SetHeaderIfMissing()`, `SecurityHeadersMiddleware.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 52`** (4 nodes): `ITenantContext`, `TenantContext.cs`, `TenantContext`, `.Initialize()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 53`** (4 nodes): `WolverineConfigurationExtensions.cs`, `WolverineConfigurationExtensions`, `.AddWolverineMessaging()`, `.RegisterHandlers()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 55`** (3 nodes): `InvalidDateTimeNow`, `.CreateEvent()`, `InvalidDateTimeNow.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 56`** (3 nodes): `InvalidDateTimeUtcNow`, `.CreateEvent()`, `InvalidDateTimeUtcNow.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 57`** (3 nodes): `InvalidGuidInMethod`, `.CreateEntity()`, `InvalidGuidInMethod.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 58`** (3 nodes): `AuthorUpdated`, `BookAdded`, `ClassEvents.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 59`** (3 nodes): `SecurityHeadersMiddlewareTests`, `.HstsValue_ShouldIncludePreloadAndSubDomainsAndMaxAge()`, `SecurityHeadersMiddlewareTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 60`** (3 nodes): `HandlerTestBase`, `.GetLogger()`, `HandlerTestBase.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 61`** (3 nodes): `InfrastructureTests`, `.ResourceIsHealthy()`, `InfrastructureTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 62`** (3 nodes): `DatabaseTests`, `.CanConnectToDatabase()`, `DatabaseTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 63`** (3 nodes): `ISalesClient`, `.GetSalesAsync()`, `ISalesClient.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 65`** (3 nodes): `UIConstants.cs`, `Icons`, `UIConstants`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 66`** (3 nodes): `OrderAggregate`, `.Apply()`, `OrderAggregate.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 67`** (3 nodes): `WolverineETagMiddleware.cs`, `WolverineETagMiddleware`, `.Before()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 68`** (3 nodes): `ITenantContext`, `.Initialize()`, `ITenantContext.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 69`** (3 nodes): `PagedListExtensions`, `.ToPagedListDto()`, `PagedListExtensions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 70`** (3 nodes): `JsonConfigurationExtensions`, `.AddJsonConfiguration()`, `JsonConfigurationExtensions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 71`** (3 nodes): `IEmailService`, `.SendAccountVerificationEmailAsync()`, `IEmailService.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 72`** (3 nodes): `AllowAnonymousTenantAttribute`, `Attribute`, `AllowAnonymousTenantAttribute.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 73`** (3 nodes): `DiagnosticCategories`, `DiagnosticIds`, `DiagnosticIds.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 75`** (2 nodes): `InvalidDateTimeInField`, `InvalidDateTimeInField.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 76`** (2 nodes): `InvalidGuidInField`, `InvalidGuidInField.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 77`** (2 nodes): `InvalidGuidInProperty`, `InvalidGuidInProperty.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 78`** (2 nodes): `AuthorUpdated`, `AuthorUpdatedClass.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 79`** (2 nodes): `BookAdded`, `BookAddedClass.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 80`** (2 nodes): `TestConstants`, `TestConstants.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 81`** (2 nodes): `UpdateBookRequestExtensions.cs`, `UpdateBookRequest`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 83`** (2 nodes): `Tenant.cs`, `Tenant`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 84`** (2 nodes): `ApplicationUser`, `ApplicationUser.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 85`** (2 nodes): `BookStatistics`, `BookStatistics.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 88`** (2 nodes): `Instrumentation`, `Instrumentation.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 89`** (2 nodes): `RateLimitOptions`, `RateLimitOptions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 91`** (2 nodes): `TenantConstants.cs`, `TenantConstants`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 92`** (2 nodes): `AccountCleanupOptions`, `AccountCleanupOptions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 93`** (2 nodes): `UcpProfileOptions.cs`, `UcpProfileOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 94`** (2 nodes): `EmailOptions`, `EmailOptions.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 95`** (2 nodes): `ResourceNames`, `ResourceNames.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 96`** (2 nodes): `MultiTenancyConstants`, `MultiTenancyConstants.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 97`** (2 nodes): `IHaveETag`, `IHaveETag.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 98`** (2 nodes): `IDomainEventNotification`, `DomainEventNotifications.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 99`** (2 nodes): `PartialDate Value Object`, `BookStore.Shared README`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 158`** (1 nodes): `Contributing Guide`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 159`** (1 nodes): `Getting Started Guide`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 160`** (1 nodes): `Possible Improvements`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 161`** (1 nodes): `BookStore Favicon`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 162`** (1 nodes): `BookStore Hero Banner`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ToString()` connect `Community 10` to `Community 0`, `Community 1`, `Community 2`, `Community 3`, `Community 4`, `Community 5`, `Community 6`, `Community 8`, `Community 9`, `Community 11`, `Community 12`, `Community 13`, `Community 17`, `Community 18`, `Community 19`, `Community 22`?**
  _High betweenness centrality (0.257) - this node is a cross-community bridge._
- **Why does `ok()` connect `Community 2` to `Community 0`, `Community 3`, `Community 4`, `Community 17`, `Community 31`?**
  _High betweenness centrality (0.052) - this node is a cross-community bridge._
- **Why does `MartenUserStore` connect `Community 4` to `Community 0`, `Community 1`, `Community 3`?**
  _High betweenness centrality (0.030) - this node is a cross-community bridge._
- **Are the 76 inferred relationships involving `ToString()` (e.g. with `.InitializeAsync_ShouldLoadFromLocalStorage()` and `.InitializeAsync_WithoutStoredCurrency_ShouldKeepGbpDefault()`) actually correct?**
  _`ToString()` has 76 INFERRED edges - model-reasoned connections that need verification._
- **Are the 52 inferred relationships involving `ok()` (e.g. with `.GetTenants()` and `.GetTenantInfo()`) actually correct?**
  _`ok()` has 52 INFERRED edges - model-reasoned connections that need verification._
- **Are the 52 inferred relationships involving `Validation()` (e.g. with `.GetLocalizedMessage_ReturnsTypeDefault_WhenCodeNotFoundButTypeExists()` and `.ToError()`) actually correct?**
  _`Validation()` has 52 INFERRED edges - model-reasoned connections that need verification._
- **What connects `DebugRefit`, `Parallel API Orchestrator Calls multiple APIs concurrently using asyncio + aioht`, `Fire all requests in parallel and return results in the same order.` to the rest of the system?**
  _134 weakly-connected nodes found - possible documentation gaps or missing edges._