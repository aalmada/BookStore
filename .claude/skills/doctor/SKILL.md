---
name: Doctor
description: specific tools and SDKs required for the BookStore project. Use this to diagnose environment issues.
---

Perform a health check on the development environment to ensure all prerequisites are met:

1. **.NET SDK**
   - Run `dotnet --version`
   - **Requirement**: Version 10.0.x or later.
   - *Action*: If too old, advise upgrading .NET.

2. **Docker**
   - Run `docker --version`
   - **Requirement**: Version 20.x or later.
   - *Action*: If missing, Docker Desktop or Podman is required for running the Aspire containers.

3. **Azure Developer CLI (azd)**
   - Run `azd version`
   - **Requirement**: Version 1.5.0 or later.
   - *Action*: This is required for deployment scripts.

4. **Kubernetes CLI (kubectl)**
   - Run `kubectl version --client`
   - **Requirement**: Valid client version installed.
   - *Action*: Required for Kubernetes deployment commands.

5. **Aspire Workload**
   - Run `dotnet workload list`
   - **Requirement**: The `aspire` workload must be present.
   - *Action*: If missing, run `dotnet workload install aspire`.

Report a summary: "Environment is healthy" or list specific missing tools.
