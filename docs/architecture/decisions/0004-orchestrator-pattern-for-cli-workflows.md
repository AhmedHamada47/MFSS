# ADR 0004: Orchestrator Pattern for CLI Workflows

## Status

Accepted

## Context

`Program.cs` contained the entire migration workflow in a single lambda handler, violating Single Responsibility Principle and making it impossible to test the orchestration logic.

## Decision

Extract the migration workflow into `MigrationOrchestrator` class. `Program.cs` now only handles CLI parsing, configuration loading, and calling the orchestrator.

## Consequences

- `Program.cs` is thin (configuration only)
- Orchestration logic is testable
- Error handling is centralized in the orchestrator
- Adding new workflows (e.g., `validate`, `list`) is straightforward

## Files

- `Orchestration/MigrationOrchestrator.cs`
- `Program.cs` (refactored)
