# ADR 0003: Interfaces and Dependency Injection for Service Abstraction

## Status

Accepted

## Context

All services were concrete classes with no interfaces, making them tightly coupled and difficult to test or replace.

## Decision

Define interfaces in `Abstractions/` for every service. Services implement these interfaces. `Program.cs` creates concrete instances and passes them to `MigrationOrchestrator` which depends only on abstractions.

## Consequences

- Services can be mocked for unit testing
- Implementation can be swapped without changing consumers
- Clear contract boundaries between components
- Future DI container migration is straightforward

## Files

- `Abstractions/` - All interface definitions
- `Orchestration/MigrationOrchestrator.cs` - Orchestration logic depending on interfaces
