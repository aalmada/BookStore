---
name: deploy__azure_container_apps
description: Deploy the BookStore application to Azure using Aspire and azd. Use this to ship the application to production.
aliases:
  - /deploy-to-azure
---

Deploy the application stack to Azure Container Apps using the Azure Developer CLI (`azd`).

## Deployment Steps

1. **Environment Check**
   - Verify the `.azure` directory exists.
   - If missing, run `azd init` (with `--no-prompt` if possible, or ask user) to initialize the environment.
   - Run `/ops__doctor_check` to verify prerequisites.

2. **Authentication**
   - Run `azd auth login` to ensure the session is active.
   - *Tip*: If the user says they are already logged in, you can verify with `azd auth show-status` (if available) or proceed.

// turbo
3. **Deploy**
   - Run `azd up` to provision resources and deploy the code.
   - This command may take several minutes.
   - Be ready to handle prompts if `SafeToAutoRun` is not enabled.

4. **Verify Deployment**
   - Upon success, run `azd show` to retrieve the public endpoints.
   - Display the `webfrontend` URL to the user so they can access their deployed app.

## Related Skills

**Prerequisites**:
- `/ops__doctor_check` - Verify your environment has .NET SDK, Docker, and azd installed
- `/test__verify_feature` - Ensure build and tests pass before deployment

**Alternatives**:
- `/deploy__kubernetes_cluster` - For Kubernetes deployment (AKS, EKS, GKE)

**Recovery**:
- `/deploy__rollback` - If deployment fails or causes issues

**See Also**:
- [aspire-deployment-guide](../../../docs/guides/aspire-deployment-guide.md) - Azure Container Apps deployment
- [aspire-guide](../../../docs/guides/aspire-guide.md) - Aspire orchestration overview
- AppHost AGENTS.md - Aspire orchestration configuration
