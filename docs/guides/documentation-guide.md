# Documentation Guide

The BookStore documentation is built using **DocFX**, a static site generator for .NET projects. It combines API reference documentation (generated from source code) with narrative articles (markdown files).

The site is automatically built and deployed to **GitHub Pages** via GitHub Actions.

## Structure

The documentation source files are located in the `docs/` directory:

```
docs/
├── api/                   # Generated API docs override (index.md)
├── guides/                # Narrative documentation (markdown)
├── images/                # Static images
├── template/              # Custom DocFX template (layout overrides)
├── architecture.md        # Core architecture documentation
├── getting-started.md     # Core getting started guide
├── index.md               # Home page
├── docfx.json             # DocFX build configuration
├── toc.yml                # Table of Contents
└── robots.txt             # SEO configuration
```

## Running Locally

To preview the documentation locally, you need the DocFX CLI tool.

### 1. Install DocFX

```bash
dotnet tool update -g docfx
```

### 2. Build and Serve

Run the following command from the root of the repository:

```bash
docfx docs/docfx.json --serve
```

The site will be available at `http://localhost:8080`.

> [!TIP]
> Use `--serve` to run a local web server that hosts the generated site. DocFX will watch for file changes and rebuild automatically.

## Configuration

The build is configured in `docs/docfx.json`:

- **metadata**: Scans the source code (`src/**/*.csproj`) to generate API reference documentation.
- **build**:
  - **content**: Includes markdown files, `toc.yml`, and images.
  - **resource**: Copies static resources.
  - **globalMetadata**: Sets site-wide variables like `_appTitle`, `_baseUrl`, and Open Graph images.
  - **template**: Uses the `modern` template with custom overrides (`template/`).
  - **sitemap**: Generates `sitemap.xml` for SEO.

### Custom Template

The `docs/template` directory contains overrides for the standard DocFX template. We use this to:
- Inject custom meta tags (Open Graph, Bing validation).
- Customize the layout or footer.
- Add custom scripts or styles.

## GitHub Actions Workflow

The documentation is built and deployed automatically by the `.github/workflows/docs.yml` workflow.

### Triggers
- Pushes to the `main` branch.
- Manual execution via `workflow_dispatch`.

### Process
1. **Checkout**: Clones the repository.
2. **Setup .NET**: Installs the .NET SDK.
3. **Install DocFX**: Installs the `docfx` global tool.
4. **Build**: Runs `docfx docs/docfx.json` to generate the static site in `_site/`.
5. **Upload Artifact**: Uploads the `_site/` directory as a GitHub Actions artifact.
6. **Deploy**: Deploys the artifact to GitHub Pages.

## Contributing to Documentation

1. Create or edit a markdown file in `docs/guides/`.
2. If it's a new file, add it to `docs/toc.yml` to make it appear in the navigation.
3. Run `docfx docs/docfx.json --serve` to verify formatting and links.
4. Commit and push your changes.
