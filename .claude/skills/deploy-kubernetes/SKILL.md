---
name: deploy-kubernetes
description: Deploy the BookStore application to a Kubernetes cluster using Aspire-generated manifests. Use this for Kubernetes deployments (AKS, EKS, GKE, etc.).
license: MIT
---

Deploy the BookStore application stack to a Kubernetes cluster using Aspire's manifest generation and kubectl.

## Related Skills

**Prerequisites**:
- `/doctor` - Check kubectl, Docker, and Aspire CLI installation

**Alternatives**:
- `/deploy-to-azure` - For simpler Azure Container Apps deployment

**Recovery**:
- `/rollback-deployment` - Rollback failed Kubernetes deployment

**Verification**:
- `/verify-feature` - Test the deployed application

## Prerequisites

1. **kubectl** installed and configured
   - Run `kubectl version --client` to verify

2. **Aspire CLI** installed
   - Run `aspire --version` to verify

3. **Kubernetes Cluster** access
   - Azure AKS, AWS EKS, Google GKE, or local (minikube, kind)
   - Verify: `kubectl cluster-info`

4. **Docker Registry** access
   - For pushing container images
   - Azure Container Registry (ACR), Docker Hub, etc.

## Deployment Steps

### 1. Generate Aspire Manifests

Aspire can generate Kubernetes manifests from your AppHost configuration:

```bash
# Navigate to AppHost project
cd src/BookStore.AppHost

# Generate manifests
aspire generate k8s -o ../../deploy/k8s

# This creates YAML files in deploy/k8s/
#  - deployments.yaml
#  - services.yaml
#  - configmaps.yaml
#  - secrets.yaml (if any)
```

### 2. Review Generated Manifests

Check the generated files:

```bash
ls -la deploy/k8s/

# Review each file
cat deploy/k8s/deployments.yaml
cat deploy/k8s/services.yaml
```

**Key things to verify**:
- ✅ Image names and tags are correct
- ✅ Resource limits are appropriate
- ✅ Environment variables are set
- ✅ Secrets are properly referenced

### 3. Create Kubernetes Namespace

```bash
# Create dedicated namespace
kubectl create namespace bookstore

# Set as default for convenience
kubectl config set-context --current --namespace=bookstore
```

### 4. Configure Secrets

Create secrets for sensitive data:

```bash
# Database connection string
kubectl create secret generic postgres-secret \
  --from-literal=connectionString='Host=postgres;Database=bookstore;...'

# JWT signing key
kubectl create secret generic jwt-secret \
  --from-literal=key='your-secret-jwt-key'

# Redis connection
kubectl create secret generic redis-secret \
  --from-literal=connectionString='redis:6379'
```

### 5. Build and Push Container Images

```bash
# Login to container registry
docker login <your-registry>

# Build API Service image
docker build -t <registry>/bookstore-api:latest -f src/BookStore.ApiService/Dockerfile .
docker push <registry>/bookstore-api:latest

# Build Web Frontend image
docker build -t <registry>/bookstore-web:latest -f src/BookStore.Web/Dockerfile .
docker push <registry>/bookstore-web:latest

# Update manifests with correct image names
# Edit deploy/k8s/deployments.yaml to use your registry
```

### 6. Deploy PostgreSQL (if not using external DB)

```bash
# Deploy PostgreSQL
kubectl apply -f - <<EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
spec:
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
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: password
        - name: POSTGRES_DB
          value: bookstore
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
spec:
  ports:
  - port: 5432
    targetPort: 5432
  selector:
    app: postgres
EOF
```

### 7. Deploy Redis

```bash
# Deploy Redis
kubectl apply -f - <<EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:latest
        ports:
        - containerPort: 6379
---
apiVersion: v1
kind: Service
metadata:
  name: redis
spec:
  ports:
  - port: 6379
    targetPort: 6379
  selector:
    app: redis
EOF
```

### 8. Apply Aspire-Generated Manifests

```bash
# Apply all manifests
kubectl apply -f deploy/k8s/

# Or apply individually
kubectl apply -f deploy/k8s/configmaps.yaml
kubectl apply -f deploy/k8s/deployments.yaml
kubectl apply -f deploy/k8s/services.yaml
```

### 9. Verify Deployment

```bash
# Check deployments
kubectl get deployments

# Check pods
kubectl get pods

# Check services
kubectl get services

# View logs
kubectl logs -l app=bookstore-api --tail=50
kubectl logs -l app=bookstore-web --tail=50

# Check events
kubectl get events --sort-by=.metadata.creationTimestamp
```

### 10. Expose Services

For external access, create Ingress or LoadBalancer:

```bash
# Option 1: LoadBalancer (cloud providers)
kubectl expose deployment bookstore-web \
  --type=LoadBalancer \
  --name=bookstore-web-lb \
  --port=80 \
  --target-port=8080

# Option 2: Ingress (recommended for production)
kubectl apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: bookstore-ingress
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - bookstore.example.com
    secretName: bookstore-tls
  rules:
  - host: bookstore.example.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: bookstore-api
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: bookstore-web
            port:
              number: 80
EOF
```

### 11. Verify Application Health

```bash
# Get external IP/hostname
kubectl get services bookstore-web-lb

# Test health endpoint
curl http://<external-ip>/health

# Test API
curl http://<external-ip>/api/books

# Test Web frontend
open http://<external-ip>
```

## Scaling

Scale deployments as needed:

```bash
# Scale API service
kubectl scale deployment bookstore-api --replicas=3

# Scale Web frontend
kubectl scale deployment bookstore-web --replicas=2

# Verify
kubectl get pods
```

## Monitoring

```bash
# Watch pods
kubectl get pods -w

# Stream logs
kubectl logs -f deployment/bookstore-api

# Resource usage
kubectl top pods
kubectl top nodes
```

## Troubleshooting

### Pods Not Starting

```bash
# Describe pod
kubectl describe pod <pod-name>

# Check events
kubectl get events --sort-by=.metadata.creationTimestamp

# Check logs
kubectl logs <pod-name>

# Common issues:
# - ImagePullBackOff: Wrong image name or no pull access
# - CrashLoopBackOff: Application error on startup
# - Pending: Insufficient resources or PVC not bound
```

### Database Connection Issues

```bash
# Test connectivity from pod
kubectl run -it --rm debug --image=postgres:16 -- psql -h postgres -U postgres

# Check secret
kubectl get secret postgres-secret -o yaml

# Verify environment variables
kubectl exec deployment/bookstore-api -- env | grep CONNECTION
```

### Service Not Accessible

```bash
# Check service
kubectl get service bookstore-web

# Check endpoints
kubectl get endpoints bookstore-web

# Port forward for testing
kubectl port-forward service/bookstore-web 8080:80

# Test locally
curl http://localhost:8080/health
```

## Cleanup

```bash
# Delete everything in namespace
kubectl delete namespace bookstore

# Or delete individual resources
kubectl delete -f deploy/k8s/
kubectl delete deployment postgres redis
kubectl delete service postgres redis
```

## Azure AKS Specific

For Azure Kubernetes Service:

```bash
# Login to Azure
az login

# Get cluster credentials
az aks get-credentials --resource-group <rg-name> --name <aks-name>

# Attach ACR to AKS (for image pull)
az aks update -n <aks-name> -g <rg-name> --attach-acr <acr-name>

# Deploy
kubectl apply -f deploy/k8s/
```

## Best Practices

- ✅ Use namespaces for isolation
- ✅ Set resource limits (CPU/memory)
- ✅ Use health checks (liveness, readiness)
- ✅ Store secrets in Kubernetes Secrets (not ConfigMaps)
- ✅ Use Ingress for external traffic
- ✅ Implement horizontal pod autoscaling (HPA)
- ✅ Use persistent volumes for stateful services
- ✅ Enable network policies for security

## Related Skills

**Prerequisites**:
- `/doctor` - Check kubectl, Docker, and Aspire CLI installation
- `/verify-feature` - Ensure build and tests pass before deployment

**Alternatives**:
- `/deploy-to-azure` - For simpler Azure Container Apps deployment

**Recovery**:
- `/rollback-deployment` - Rollback failed Kubernetes deployment

**See Also**:
- [aspire-deployment-guide](../../../docs/guides/aspire-deployment-guide.md) - Kubernetes deployment with Aspire
- [aspire-guide](../../../docs/guides/aspire-guide.md) - Aspire orchestration overview
- AppHost AGENTS.md - Aspire orchestration configuration
