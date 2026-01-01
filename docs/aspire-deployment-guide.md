# Aspire Deployment Guide

This guide covers deploying the BookStore application using **Aspire** to production environments, including Azure Container Apps and Kubernetes.

## Overview

**Aspire** separates the act of producing deployment assets from executing a deployment:

- **`aspire publish`** - Generates intermediate, parameterized deployment artifacts (Docker Compose, Kubernetes manifests, Azure specifications)
- **`aspire deploy`** - Executes deployment by resolving parameters and applying changes to the target environment

The BookStore application uses Aspire to orchestrate:
- **API Service** - Event-sourced backend with Marten and PostgreSQL
- **Web Frontend** - Blazor application
- **PostgreSQL** - Database with event store and projections
- **Azure Blob Storage** - File storage (Azurite emulator locally)

## Prerequisites

### General Requirements

- **.NET 10 SDK** or later (with Aspire workload)
- **Docker Desktop** or Podman (OCI-compliant container runtime)
- **Aspire CLI** (for publishing and deployment)

```bash
# Install Aspire workload
dotnet workload install aspire

# Verify installation
dotnet workload list
```

### Azure Deployment Requirements

- **Azure Developer CLI (azd)** - [Install azd](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- **Azure CLI** - Configured and signed in
- **Active Azure subscription** with permissions to create resources

```bash
# Install Azure Developer CLI (macOS)
brew install azure-developer-cli

# Login to Azure
azd auth login
az login
```

### Kubernetes Deployment Requirements

- **kubectl** - Kubernetes command-line tool
- **Kubernetes cluster** - AKS, EKS, GKE, or local (kind, minikube)
- **Container registry** - Azure Container Registry, Docker Hub, or private registry
- **Aspire.Hosting.Kubernetes** NuGet package

```bash
# Install kubectl (macOS)
brew install kubectl

# Verify cluster access
kubectl cluster-info
```

---

## Deployment to Azure Container Apps

Azure Container Apps (ACA) is the recommended hosting environment for Aspire applications, providing a fully managed, serverless platform for containerized microservices.

### Step 1: Initialize Azure Deployment

From your solution root directory:

```bash
# Navigate to solution root
cd /path/to/BookStore

# Initialize azd
azd init
```

When prompted:
1. Select **"Use code in the current directory"**
2. Confirm the detected Aspire AppHost project (`BookStore.AppHost`)
3. Enter an **environment name** (e.g., `dev`, `staging`, `prod`)

This generates:
- `azure.yaml` - Defines services and Azure resource mappings
- `.azure/config.json` - Active environment configuration
- `.azure/<environment>/.env` - Environment-specific settings

### Step 2: Configure Azure Resources

Review and customize `azure.yaml` if needed. The default configuration creates:
- **Azure Container Apps Environment** - Hosts all containers
- **Azure Container Registry** - Stores container images
- **Log Analytics Workspace** - Centralized logging
- **Managed PostgreSQL** - Production database
- **Azure Storage Account** - Blob storage

> [!IMPORTANT]
> The BookStore application requires PostgreSQL with `pg_trgm` and `unaccent` extensions. Ensure your Azure PostgreSQL Flexible Server has these extensions enabled.

### Step 3: Deploy to Azure

Execute the provisioning and deployment:

```bash
azd up
```

This command:
1. **Packages services** - Builds container images using .NET's built-in container publishing
2. **Provisions Azure resources** - Creates resource group, container registry, container apps environment, etc.
3. **Pushes images** - Uploads containers to Azure Container Registry
4. **Deploys services** - Deploys containers to Azure Container Apps

When prompted:
- Select your **Azure subscription**
- Choose an **Azure region** (e.g., `eastus2`, `westeurope`)
- Select which services to **expose to the Internet** (typically `webfrontend`)

### Step 4: Verify Deployment

After successful deployment, `azd` outputs:
- **Service endpoints** - URLs for your deployed services
- **Aspire Dashboard URL** - Monitoring and observability
- **Resource group link** - Azure Portal overview

```bash
# View deployment details
azd show

# Stream logs from a service
azd logs --service apiservice --follow

# Monitor all services
azd monitor
```

### Step 5: Configure Database Extensions

Connect to your Azure PostgreSQL Flexible Server and enable required extensions:

```sql
-- Connect to your database
\c bookstore

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;
```

### Step 6: Run Database Migrations

The BookStore application uses Marten for event sourcing. Initialize the database schema:

```bash
# Option 1: Use the API's built-in migration endpoint (if exposed)
curl -X POST https://<apiservice-url>/api/admin/projections/rebuild

# Option 2: Run migrations locally against Azure database
# Set connection string in user secrets or environment variable
dotnet user-secrets set "ConnectionStrings:bookstore" "<azure-connection-string>" \
  --project src/BookStore.ApiService

# Run the API service to apply migrations
dotnet run --project src/BookStore.ApiService
```

### Managing Azure Deployments

```bash
# Update existing deployment
azd deploy

# Provision infrastructure only
azd provision

# Clean up all resources
azd down

# View environment variables
azd env get-values
```

### Cost Optimization

Azure Container Apps uses consumption-based pricing. To minimize costs:

- **Scale to zero** - Configure min replicas to 0 for non-production environments
- **Use Azure PostgreSQL Basic tier** - For development/staging
- **Enable auto-pause** - For development databases
- **Use resource tags** - Track costs by environment

---

## Deployment to Kubernetes

Kubernetes deployment provides maximum flexibility and portability across cloud providers and on-premises infrastructure.

### Step 1: Add Kubernetes Hosting Integration

Add the Kubernetes hosting package to your AppHost project:

```bash
dotnet add src/BookStore.AppHost/BookStore.AppHost.csproj \
  package Aspire.Hosting.Kubernetes
```

### Step 2: Generate Kubernetes Manifests

Use the Aspire CLI to generate Kubernetes YAML manifests:

```bash
# Generate manifests to output directory
aspire publish -o ./k8s-artifacts

# Review generated files
ls -la ./k8s-artifacts
```

This generates:
- **Deployments** - For API service and web frontend
- **StatefulSets** - For PostgreSQL (if using in-cluster database)
- **Services** - Internal service discovery
- **ConfigMaps** - Application configuration
- **Secrets** - Sensitive data (connection strings, API keys)
- **PersistentVolumeClaims** - For PostgreSQL data
- **Ingress** - External access (if configured)

### Step 3: Configure Container Registry

Build and push container images to your registry:

```bash
# Set your container registry
export CONTAINER_REGISTRY="myregistry.azurecr.io"

# Login to registry
# Azure Container Registry
az acr login --name myregistry

# Docker Hub
docker login

# Build and push API service
docker build -t $CONTAINER_REGISTRY/bookstore-api:latest \
  -f src/BookStore.ApiService/Dockerfile .
docker push $CONTAINER_REGISTRY/bookstore-api:latest

# Build and push web frontend
docker build -t $CONTAINER_REGISTRY/bookstore-web:latest \
  -f src/BookStore.Web/Dockerfile .
docker push $CONTAINER_REGISTRY/bookstore-web:latest
```

> [!TIP]
> For Azure Container Registry, grant your AKS cluster pull permissions:
> ```bash
> az aks update -n <cluster-name> -g <resource-group> \
>   --attach-acr <registry-name>
> ```

### Step 4: Configure Secrets and ConfigMaps

Create Kubernetes secrets for sensitive data:

```bash
# Create namespace
kubectl create namespace bookstore

# PostgreSQL connection string
kubectl create secret generic bookstore-db \
  --from-literal=connectionString="Host=postgres;Database=bookstore;Username=postgres;Password=<password>" \
  -n bookstore

# Azure Storage connection string (for production)
kubectl create secret generic bookstore-storage \
  --from-literal=connectionString="<azure-storage-connection-string>" \
  -n bookstore
```

### Step 5: Deploy PostgreSQL

For production, use a managed database service (Azure Database for PostgreSQL, AWS RDS, Google Cloud SQL). For development/testing, deploy PostgreSQL in-cluster:

```yaml
# postgres-deployment.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: bookstore
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:16
        env:
        - name: POSTGRES_DB
          value: bookstore
        - name: POSTGRES_USER
          value: postgres
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: bookstore-db
              key: password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-data
          mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
  - metadata:
      name: postgres-data
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 10Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: bookstore
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
```

Apply the PostgreSQL deployment:

```bash
kubectl apply -f postgres-deployment.yaml
```

### Step 6: Deploy Application Services

Update the generated manifests with your container registry and apply:

```bash
# Update image references in manifests
sed -i '' "s|image: .*bookstore-api.*|image: $CONTAINER_REGISTRY/bookstore-api:latest|g" \
  k8s-artifacts/*.yaml
sed -i '' "s|image: .*bookstore-web.*|image: $CONTAINER_REGISTRY/bookstore-web:latest|g" \
  k8s-artifacts/*.yaml

# Apply all manifests
kubectl apply -f k8s-artifacts/ -n bookstore

# Verify deployments
kubectl get pods -n bookstore
kubectl get services -n bookstore
```

### Step 7: Configure Ingress

Expose the web frontend using an Ingress controller:

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: bookstore-ingress
  namespace: bookstore
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - bookstore.example.com
    secretName: bookstore-tls
  rules:
  - host: bookstore.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: webfrontend
            port:
              number: 80
```

Apply the ingress:

```bash
kubectl apply -f ingress.yaml
```

### Step 8: Verify Deployment

```bash
# Check pod status
kubectl get pods -n bookstore -w

# View logs
kubectl logs -f deployment/apiservice -n bookstore
kubectl logs -f deployment/webfrontend -n bookstore

# Port-forward for local testing
kubectl port-forward svc/webfrontend 8080:80 -n bookstore

# Check ingress
kubectl get ingress -n bookstore
```

### Managing Kubernetes Deployments

```bash
# Update deployment with new image
kubectl set image deployment/apiservice \
  apiservice=$CONTAINER_REGISTRY/bookstore-api:v2 -n bookstore

# Scale deployment
kubectl scale deployment/apiservice --replicas=3 -n bookstore

# Rollback deployment
kubectl rollout undo deployment/apiservice -n bookstore

# View rollout history
kubectl rollout history deployment/apiservice -n bookstore

# Delete all resources
kubectl delete namespace bookstore
```

---

## Azure Kubernetes Service (AKS) Deployment

For deploying to Azure Kubernetes Service, combine both approaches:

### Step 1: Create AKS Cluster

```bash
# Set variables
RESOURCE_GROUP="bookstore-rg"
CLUSTER_NAME="bookstore-aks"
LOCATION="eastus2"
ACR_NAME="bookstoreacr"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create Azure Container Registry
az acr create --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME --sku Basic

# Create AKS cluster
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $CLUSTER_NAME \
  --node-count 2 \
  --enable-managed-identity \
  --attach-acr $ACR_NAME \
  --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME
```

### Step 2: Create Azure Database for PostgreSQL

```bash
# Create PostgreSQL Flexible Server
az postgres flexible-server create \
  --resource-group $RESOURCE_GROUP \
  --name bookstore-db \
  --location $LOCATION \
  --admin-user dbadmin \
  --admin-password '<secure-password>' \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 16 \
  --storage-size 32

# Create database
az postgres flexible-server db create \
  --resource-group $RESOURCE_GROUP \
  --server-name bookstore-db \
  --database-name bookstore

# Configure firewall (allow Azure services)
az postgres flexible-server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --name bookstore-db \
  --rule-name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### Step 3: Deploy Application

Follow the Kubernetes deployment steps above, using Azure Container Registry and Azure Database for PostgreSQL connection strings.

---

## Environment Configuration

### Development Environment

Use Aspire's local development experience:

```bash
aspire run
```

For a detailed look at the local orchestration configuration, see the [Aspire Orchestration Guide](aspire-guide.md).

This starts:
- Aspire Dashboard
- All services with hot reload
- Azurite (Azure Storage emulator)
- PostgreSQL container
- PgAdmin

### Staging/Production Environments

Use environment-specific configuration:

```bash
# Azure
azd env new staging
azd env select staging
azd up

# Kubernetes
kubectl create namespace bookstore-staging
kubectl apply -f k8s-artifacts/ -n bookstore-staging
```

### Configuration Management

**Azure Container Apps:**
- Use Azure App Configuration for centralized settings
- Use Azure Key Vault for secrets
- Configure via `azd` environment variables

**Kubernetes:**
- Use ConfigMaps for non-sensitive configuration
- Use Secrets for sensitive data
- Consider external secret management (Azure Key Vault, HashiCorp Vault)

---

## Monitoring and Observability

### Aspire Dashboard

Deploy the Aspire Dashboard to production for monitoring:

```bash
# Azure Container Apps (included by default with azd)
# Access via the URL provided by azd up

# Kubernetes
kubectl apply -f https://raw.githubusercontent.com/dotnet/aspire/main/src/Aspire.Dashboard/kubernetes/aspire-dashboard.yaml
kubectl port-forward svc/aspire-dashboard 18888:18888 -n aspire-system
```

### Application Insights (Azure)

Configure Application Insights for production telemetry:

```bash
# Create Application Insights
az monitor app-insights component create \
  --app bookstore-insights \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP

# Get instrumentation key
az monitor app-insights component show \
  --app bookstore-insights \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey
```

Add to your services:

```csharp
// In Program.cs
builder.Services.AddApplicationInsightsTelemetry(
    builder.Configuration["ApplicationInsights:InstrumentationKey"]);
```

### Health Checks

Both services expose health check endpoints:

```bash
# API Service
curl https://<api-url>/health

# Web Frontend
curl https://<web-url>/health
```

Configure Kubernetes liveness and readiness probes:

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5
```

---

## Troubleshooting

### Azure Container Apps

```bash
# View logs
azd logs --service apiservice --follow

# Check container status
az containerapp show \
  --name apiservice \
  --resource-group <resource-group>

# View revisions
az containerapp revision list \
  --name apiservice \
  --resource-group <resource-group>
```

### Kubernetes

```bash
# Check pod status
kubectl describe pod <pod-name> -n bookstore

# View logs
kubectl logs <pod-name> -n bookstore --tail=100 -f

# Execute commands in pod
kubectl exec -it <pod-name> -n bookstore -- /bin/bash

# Check events
kubectl get events -n bookstore --sort-by='.lastTimestamp'
```

### Common Issues

**Database Connection Failures:**
- Verify connection strings in secrets
- Check firewall rules (Azure)
- Ensure PostgreSQL extensions are enabled

**Image Pull Errors:**
- Verify container registry authentication
- Check image names and tags
- Ensure AKS has ACR pull permissions (Azure)

**Service Discovery Issues:**
- Verify service names match configuration
- Check DNS resolution in pods
- Review Aspire service references

---

## CI/CD Integration

### GitHub Actions (Azure)

```yaml
# .github/workflows/deploy-azure.yml
name: Deploy to Azure

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install azd
        uses: Azure/setup-azd@v1
      
      - name: Login to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      - name: Deploy to Azure
        run: azd up --no-prompt
        env:
          AZURE_ENV_NAME: production
```

### GitHub Actions (Kubernetes)

```yaml
# .github/workflows/deploy-k8s.yml
name: Deploy to Kubernetes

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Login to ACR
        uses: azure/docker-login@v2
        with:
          login-server: ${{ secrets.ACR_LOGIN_SERVER }}
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}
      
      - name: Build and push images
        run: |
          docker build -t ${{ secrets.ACR_LOGIN_SERVER }}/bookstore-api:${{ github.sha }} \
            -f src/BookStore.ApiService/Dockerfile .
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/bookstore-api:${{ github.sha }}
          
          docker build -t ${{ secrets.ACR_LOGIN_SERVER }}/bookstore-web:${{ github.sha }} \
            -f src/BookStore.Web/Dockerfile .
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/bookstore-web:${{ github.sha }}
      
      - name: Set up kubectl
        uses: azure/setup-kubectl@v4
      
      - name: Deploy to AKS
        uses: azure/k8s-deploy@v5
        with:
          manifests: |
            k8s-artifacts/
          images: |
            ${{ secrets.ACR_LOGIN_SERVER }}/bookstore-api:${{ github.sha }}
            ${{ secrets.ACR_LOGIN_SERVER }}/bookstore-web:${{ github.sha }}
          namespace: bookstore
```

---

## Security Best Practices

1. **Use Managed Identities** - Avoid storing credentials (Azure)
2. **Enable HTTPS** - Use TLS certificates (Let's Encrypt, Azure-managed)
3. **Network Policies** - Restrict pod-to-pod communication (Kubernetes)
4. **Secret Management** - Use Azure Key Vault or external secret stores
5. **RBAC** - Configure role-based access control
6. **Container Scanning** - Scan images for vulnerabilities
7. **Regular Updates** - Keep dependencies and base images updated

---

## Additional Resources

- [Aspire Deployment Overview](https://learn.microsoft.com/dotnet/aspire/deployment/overview)
- [Deploy to Azure Container Apps](https://learn.microsoft.com/dotnet/aspire/deployment/azure/aca-deployment)
- [Azure Developer CLI Reference](https://learn.microsoft.com/azure/developer/azure-developer-cli/reference)
- [Aspire.Hosting.Kubernetes Package](https://www.nuget.org/packages/Aspire.Hosting.Kubernetes)
- [Azure Kubernetes Service Documentation](https://learn.microsoft.com/azure/aks/)
- [PostgreSQL on Azure](https://learn.microsoft.com/azure/postgresql/)
