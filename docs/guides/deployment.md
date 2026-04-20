# Deployment Guide

This guide covers how the BookStore application is run locally, how the CI/CD pipelines work, and the current state of production deployment.

## Local Development

The application is orchestrated locally using **.NET Aspire**. A single command starts all required services via Docker containers.

### Prerequisites

- **.NET 10 SDK**
- **Aspire CLI** — `dotnet tool install -g aspire.cli`
- **Docker Desktop** (must be running before `aspire run`)

### Running the Application

```bash
aspire run
```

This starts:
- **PostgreSQL** + **pgAdmin** (Docker containers)
- **Azurite** — Azure Blob Storage emulator (Docker container)
- **Redis** — distributed cache for HybridCache (Docker container)
- **BookStore.ApiService** — event-sourced REST API (Marten + Wolverine)
- **BookStore.Web** — Blazor Server frontend

The Aspire dashboard opens automatically and shows live URLs, health status, and logs for each resource.

> See [Getting Started](../getting-started.md) for full setup instructions and service URLs.
> See [Aspire Orchestration Guide](aspire-guide.md) for details on how resources are wired together.

### Aspire Configuration

The AppHost is defined in `src/BookStore.AppHost/AppHost.cs`. The `aspire.config.json` at the repository root points to this project:

```json
{
  "appHost": {
    "path": "src/BookStore.AppHost/BookStore.AppHost.csproj"
  }
}
```

Optional environment overrides passed through AppHost configuration:

| Configuration key      | Environment variable        | Purpose                            |
|------------------------|-----------------------------|------------------------------------|
| `RateLimit:Disabled`   | `RateLimit__Disabled`       | Disable rate limiting (tests)      |
| `Seeding:Enabled`      | `Seeding__Enabled`          | Enable database seeding on startup |
| `Email:DeliveryMethod` | `Email__DeliveryMethod`     | Override email delivery method     |

---

## CI/CD Pipelines

All pipelines are defined under `.github/workflows/`.

### `ci.yml` — Continuous Integration

Triggers on pushes and pull requests to `main` and `develop`.

**Jobs:**

| Job             | What it does                                                                                          |
|-----------------|-------------------------------------------------------------------------------------------------------|
| `build-and-test`| Restores, builds (Release), runs unit tests for `ApiService`, `Analyzers`, and `Shared` projects. Generates and posts code coverage reports. |
| `code-quality`  | Verifies code formatting (`dotnet format --verify-no-changes`) and runs Roslyn analyzers with warnings-as-errors. |

> Integration tests (`BookStore.AppHost.Tests`) are currently **disabled** in CI (commented out) due to Docker availability constraints on hosted runners.

### `nightly-integration.yml` — Nightly Integration Tests

Triggers on a schedule (2 AM UTC daily) and can be triggered manually.

Runs Aspire-based integration tests against a fully started application stack (PostgreSQL, Azurite, Redis). Uploads TRX test results as artifacts.

### `docs.yml` — Documentation Site Deployment

Triggers on pushes to `main`.

Builds the DocFX documentation site and deploys it to **GitHub Pages**.

**To enable this deployment**, configure repository settings:

1. Go to **Settings → Pages**.
2. Under **Build and deployment → Source**, select **GitHub Actions**.

After configuration, each push to `main` rebuilds and redeploys the docs site. Deployment status is visible in the **Actions** tab.

---

## Production Deployment

Production deployment has **not yet been implemented**. No `azure.yaml`, Kubernetes manifests, or production deploy workflow exist in the repository.

The intended production target is **Azure Container Apps**, leveraging the Aspire `aspire publish` / `aspire deploy` workflow. See the [Aspire Deployment Guide](aspire-deployment-guide.md) for detailed instructions on how to provision and deploy to Azure when ready.
