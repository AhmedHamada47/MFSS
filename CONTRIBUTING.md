# Contributing to MFSS

## Development Workflow

1. Fork the repository.
2. Create a feature branch (`git checkout -b feat/your-feature`).
3. Make changes following the project conventions.
4. Write or update tests to cover your changes.
5. Run the full test suite: `dotnet test MFSS.Tests/`
6. Ensure no lint or build warnings.
7. Commit using [conventional commits](https://www.conventionalcommits.org/):
   - `feat:` new feature
   - `fix:` bug fix
   - `refactor:` code restructuring
   - `perf:` performance improvement
   - `docs:` documentation changes
   - `test:` test additions/updates
   - `ci:` CI/CD changes
8. Open a pull request against `main`.

## Code Conventions

- **SOLID Principles**: All contributions should adhere to SOLID.
  - Single Responsibility: One class, one concern.
  - Open/Closed: Open for extension, closed for modification.
  - Liskov Substitution: Derived classes must be substitutable.
  - Interface Segregation: Keep interfaces focused.
  - Dependency Inversion: Depend on abstractions, not concretions.
- **Async All the Way**: Use async/await for I/O operations.
- **Configuration**: All secrets must use environment variable placeholders (`${VAR_NAME}`).
- **Thread Safety**: Services operating on shared state must be thread-safe.
- **Error Handling**: Use specific exception types; avoid swallowing exceptions.

## Architecture Overview

```
Abstractions/       - Interface definitions (contracts)
Models/             - Configuration and domain models
Services/           - Implementation of core business logic
StorageProviders/   - Strategy pattern for cloud storage backends
Orchestration/      - High-level workflow orchestration
Configuration/      - Validation pipelines (FluentValidation)
```

## Testing Guidelines

- Unit tests for all service classes.
- Integration tests for database and file transfer workflows.
- All tests must be deterministic and isolated.
- Use `TestDatabaseHelper` for database-backed tests.
- Use `LocalStorageProvider` for file transfer tests.

## Pull Request Checklist

- [ ] Code builds without warnings
- [ ] All tests pass
- [ ] New code includes new tests
- [ ] No secrets committed
- [ ] Conventional commit message format
