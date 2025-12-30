# Contributing to Book Store

Thank you for your interest in contributing to the Book Store project! This document provides guidelines and instructions for contributing.

## Code of Conduct

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on constructive feedback
- Respect differing viewpoints and experiences

## How to Contribute

### Reporting Issues

Before creating an issue:
1. **Search existing issues** to avoid duplicates
2. **Use a clear title** that describes the problem
3. **Provide details**:
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment (OS, .NET version, Docker version)
   - Relevant logs from Aspire dashboard

### Suggesting Features

Feature requests are welcome! Please:
1. **Check existing issues** for similar requests
2. **Describe the use case** - why is this feature needed?
3. **Provide examples** of how it would work
4. **Consider the scope** - does it fit the project goals?

### Pull Requests

#### Before You Start

1. **Fork the repository** and create a branch from `main`
2. **Discuss major changes** in an issue first
3. **Follow the coding standards** (see below)

#### Development Setup

**Requirements**: .NET 10 SDK with C# 14, Docker Desktop

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/BookStore.git
cd BookStore

# Install dependencies
dotnet restore

# Install Aspire workload (if not already installed)
dotnet workload install aspire

# Run the application
aspire run
```

**Note**: The project uses the new `.slnx` solution file format introduced in .NET 10.

#### Making Changes

1. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the coding standards

3. **Write tests** for new functionality

4. **Run tests** to ensure nothing breaks:
   ```bash
   dotnet test
   ```

5. **Update documentation** if needed

6. **Commit with clear messages**:
   ```bash
   git commit -m "feat: add book rating feature"
   ```

#### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `style:` - Code style changes (formatting, etc.)
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

Examples:
```
feat: add book rating endpoint
fix: correct ETag validation in update handler
docs: update getting-started guide
refactor: simplify book search projection
```

#### Submitting Pull Request

1. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create a Pull Request** with:
   - Clear title and description
   - Reference to related issues
   - Screenshots (if UI changes)
   - Test results

3. **Respond to feedback** from reviewers

4. **Keep your PR updated** with the main branch

## Coding Standards

### C# Guidelines

**Follow the project's [.editorconfig](.editorconfig)** - All C# coding standards are defined there and enforced by the IDE and build process.

Key conventions:
- Use **meaningful names** for variables, methods, and classes
- Add **XML documentation** for public APIs
- Keep methods **small and focused** (single responsibility)
- Use **async/await** for I/O operations
- Prefer **records** for immutable data
- Always use `DateTimeOffset.UtcNow` (never `DateTime.Now`)
- Always use `Guid.CreateVersion7()` (never `Guid.NewGuid()`)

Your IDE will automatically apply formatting rules from `.editorconfig`. Run `dotnet build` to see any style violations.

#### Code Analyzers

The project uses **[Roslynator.Analyzers](https://github.com/dotnet/roslynator)** (version 4.15.0) for enhanced code analysis:

- **500+ analyzers** for code quality, style, and best practices
- **Automatic refactorings** suggested by your IDE
- **Build-time enforcement** - violations appear as warnings during build
- **Modern C# patterns** - Encourages collection expressions, pattern matching, etc.

Common Roslynator suggestions you'll see:
- Use collection expressions: `[]` instead of `new()`
- Simplify LINQ expressions
- Remove unnecessary code
- Use pattern matching where applicable
- Optimize string operations

All analyzer settings are configured in `Directory.Build.props` and apply to all projects in the solution.

### Event Sourcing Patterns

- **Events are immutable** - never modify event definitions
- **Events are past tense** - `BookAdded`, not `AddBook`
- **Include timestamps** - always use `DateTimeOffset.UtcNow`
- **Use correlation IDs** - for distributed tracing

### API Design

- Use **minimal APIs** for endpoints
- Follow **REST conventions** for public endpoints
- Use **command/handler pattern** (Wolverine) for writes
- Return **appropriate status codes**
- Include **ETag support** for updates/deletes
- Add **XML documentation** for OpenAPI

### Testing

- Write **unit tests** for handlers
- Use **NSubstitute** for mocking
- Test **edge cases** and error conditions
- Aim for **high code coverage** on business logic

### JSON Standards

- Use **camelCase** for property names
- Serialize **enums as strings** (not integers)
- Use **ISO 8601** for dates (`DateTimeOffset`)
- Always use **UTC timezone**

Example:
```json
{
  "bookId": "018d5e4a-7b2c-7000-8000-123456789abc",
  "title": "Clean Code",
  "status": "active",
  "lastModified": "2025-12-26T17:26:14.123+00:00"
}
```

## Project Structure

Understanding the architecture helps you contribute effectively:

- **BookStore.ApiService** - Backend API (event sourcing, CQRS)
- **BookStore.Web** - Blazor frontend
- **BookStore.AppHost** - Aspire orchestration
- **BookStore.ServiceDefaults** - Shared configuration
- **BookStore.Shared** - Shared library
- **BookStore.Shared.Tests** - Shared unit tests
- **BookStore.ApiService.Tests** - API unit tests
- **BookStore.Web.Tests** - Web tests

Key patterns:
- **Event Sourcing** - All state changes via events
- **CQRS** - Separate read/write models
- **Command/Handler** - Wolverine mediator pattern
- **Projections** - Async read model updates

## Documentation

When adding features, update:
- **README.md** - If it affects quick start or features
- **docs/getting-started.md** - If it changes setup
- **docs/architecture.md** - If it changes design
- **XML comments** - For all public APIs
- **OpenAPI** - Automatically generated from code

## Questions?

- **Check the docs** in the `/docs` folder
- **Open an issue** for questions
- **Review existing code** for examples

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (see [LICENSE](LICENSE)).

## Thank You!

Your contributions make this project better for everyone. We appreciate your time and effort! 🎉
