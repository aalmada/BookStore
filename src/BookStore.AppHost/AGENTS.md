# AppHost Instructions

**Scope**: `src/BookStore.AppHost/**`

## Guides
- `docs/guides/aspire-guide.md` - Aspire patterns
- `docs/guides/aspire-deployment-guide.md` - Azure deployment

## Skills
- `/deploy-to-azure` - Deploy to Azure Container Apps
- `/deploy-kubernetes` - Deploy to Kubernetes
- `/doctor` - Verify environment

## Rules
- Use Aspire for all resource orchestration
- Define resources in `AppHost.cs`: PostgreSQL, Redis, Azurite, API, Web
- Aspire handles connection strings and service discovery automatically
- Ensure Docker Desktop is running before `aspire run`
- Define resources before referencing them

## References
- See [Aspire Orchestration Guide](../../docs/aspire-guide.md) for detailed patterns
- See [Aspire Deployment Guide](../../docs/aspire-deployment-guide.md) for Azure and Kubernetes deployment

