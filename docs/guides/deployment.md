# Deploying to GitHub Pages

This project uses **GitHub Actions** to automatically build and deploy the documentation site.

## Configuration

To enable deployment, you must configure your repository settings:

1. Go to your repository **Settings**.
2. Select **Pages** from the left sidebar.
3. Under **Build and deployment** > **Source**, select **GitHub Actions**.

## Workflow

The deployment is handled by the `.github/workflows/docs.yml` workflow, which:
1. Triggers on pushes to `main`.
2. Sets up the .NET environment.
3. Installs and runs DocFX.
4. Uploads the generated `_site` artifact.
5. Deploys the artifact to GitHub Pages.

## Verification

After configuration, push a change to `main` to trigger the workflow. You can view the deployment status in the **Actions** tab.
