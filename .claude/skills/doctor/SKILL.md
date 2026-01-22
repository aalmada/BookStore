---
name: doctor
description: Check specific tools and SDKs required for the BookStore project. Use this to diagnose environment issues.
license: MIT
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

5. **Aspire CLI** (for orchestration)
   - Run: `aspire --version`
   - **Requirement**: Aspire CLI must be installed.
   - *Action*: If missing, see [Install instructions](https://aspire.dev/get-started/install-cli/)

6. **Report Summary**
   - If all checks pass: "✅ Environment is healthy"
   - If issues found: List specific missing tools with installation instructions

## Related Skills

**Run Before**:
- `/deploy-to-azure` - Check azd and Docker before deployment
- `/deploy-kubernetes` - Check kubectl and Docker before deployment
- `/verify-feature` - Ensure environment is correct before verification

**Next Steps**:
- If issues found, resolve them and re-run `/doctor`
- `/rebuild-clean` - Clean rebuild after environment changes

**See Also**:
- [getting-started](../../../docs/getting-started.md) - Installation instructions
- [aspire-guide](../../../docs/guides/aspire-guide.md) - Aspire orchestration
- README.md - Prerequisites section
