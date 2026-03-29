# Aspire Pipelines Reference

## Overview
Aspire uses a pipeline-based deployment system that enables extensible, composable deployment workflows. Pipelines break deployment into discrete, well-defined steps that can be optimized for performance and reliability while maintaining clear visibility into the deployment process.

### Why pipelines matter
- **Intelligent concurrency:** Independent operations run in parallel, maximizing resource utilization and minimizing total deployment time.
- **Granular control:** Each step can be executed, monitored, and retried independently with specific error reporting and targeted recovery capabilities.
- **Smart dependency management:** Steps wait only for their actual dependencies, ensuring optimal execution order while maintaining correctness.
- **Comprehensive observability:** Real-time progress reporting at both step and sub-task levels provides clear visibility into deployment operations.
- **Flexible execution:** Run complete deployments or selective step execution based on your specific needs.

### Core capabilities
- **Extend deployments:** Add custom deployment steps for specific resources or scenarios.
- **Control execution order:** Declare dependencies between steps to ensure proper sequencing.
- **Run selective steps:** Execute only specific steps and their dependencies for faster iterations.
- **Parallelize operations:** Independent steps can run concurrently for improved performance.
- **State persistence:** Cache deployment state and parameters across multiple deployment runs.
- **Intelligent granularity:** Balance between fine-grained control and deployment efficiency.

## Core Concepts

### Pipeline Steps
A pipeline step represents a discrete unit of work in the application pipeline. Each step encapsulates a specific aspect of the deployment process, making the overall operation more manageable and debuggable.

Each step has:
- **Name:** A unique identifier for the step
- **Action:** The work to be performed during deployment
- **Dependencies:** Other steps that must complete before this step runs
- **Resource context:** Access to the application’s resource model and configuration

#### Example pipeline steps
**Infrastructure steps (can run in parallel):**
- `provision-cosmosdb`: Provision the CosmosDB database
- `provision-storage`: Provision Azure Storage account
- `provision-aca-env`: Provision Azure Container Apps environment
- `provision-container-registry`: Set up the container image registry

**Build steps (can run in parallel):**
- `build-apiservice`: Build container image for the API service
- `build-webfrontend`: Build container image for the frontend

**Deployment steps (depend on builds and infrastructure):**
- `push-apiservice`: Push API service image to registry
- `push-webfrontend`: Push frontend image to registry
- `deploy-apiservice`: Deploy API service to compute platform
- `deploy-webfrontend`: Deploy frontend to compute platform

### Step Execution Phases
The pipeline system operates through three distinct phases:
1. **Registration phase:** Steps are collected from resource annotations and pipeline API calls.
2. **Resolution phase:** The system validates dependencies, calculates execution order, and identifies concurrency opportunities.
3. **Execution phase:** Steps run according to the resolved plan, with parallel execution, dependency waiting, progress reporting, and error isolation.

### Dependency Management
Dependencies in Aspire pipelines are explicit and purpose-driven. Steps declare their relationships using:
- `PipelineStep.DependsOn`: This step must run after another step completes successfully
- `PipelineStep.RequiredBy`: Another step must run after this step (inverse dependency declaration)

The pipeline system validates dependencies to ensure:
- No circular dependencies
- All references exist
- Logical consistency

### Optimal Granularity Design
Resource-level granularity: Each infrastructure resource is provisioned as a separate step, enabling precise error reporting, targeted retry, progress transparency, and flexible recovery.
Operation-level organization: Related operations are grouped logically while maintaining independence (e.g., build, push, deploy).

## Well-known Steps

### Entry Point Steps
- `WellKnownSteps.Deploy`: Orchestrates the complete deployment process.
- `WellKnownSteps.Publish`: Generates deployment artifacts without executing the actual deployment.
- `WellKnownSteps.Build`: Builds container images for compute resources.
- `WellKnownSteps.Push`: Pushes container images to registries.

### Prerequisite Steps
- `WellKnownSteps.BuildPrereq`: Pre-requisites for building (dependency resolution, environment setup).
- `WellKnownSteps.DeployPrereqs`: Pre-requisites for deployment (authentication, environment validation).
- `WellKnownSteps.PublishPrereqs`: Pre-requisites for publishing (build environment setup).
- `WellKnownSteps.PushPrereq`: Pre-requisites for pushing images (registry authentication).

### Resource-contributed Steps
Resources can contribute their own pipeline steps through annotations. For example, a static site resource might contribute steps for building, configuring storage permissions, uploading files, and configuring a CDN.

### Application-specific Steps
Applications can add custom steps directly through the pipeline API for scenarios like database migrations, smoke tests, configuration updates, and notification workflows.

## Execution Control

### Running Specific Steps
The `aspire do` command allows you to execute individual steps and their dependencies, providing fine-grained control over the deployment process. Use cases include incremental deployments, troubleshooting, development workflows, and selective operations.

### Parallel Execution and Performance Optimization
The pipeline system maximizes deployment performance through intelligent parallel execution, resource grouping, critical path optimization, and resource-aware scheduling. It provides robust failure handling, error isolation, and comprehensive logging for efficient debugging and recovery.

---
For more, see the official Aspire documentation for pipelines.
