# ADR 0002: Strategy Pattern for Storage Providers

## Status

Accepted

## Context

The `FileTransferService` originally handled all storage providers (HTTP, S3, Azure, GCS, Local) with switch statements, violating Open/Closed principle and making it a God class.

## Decision

Extract each storage backend into its own class implementing `IStorageProvider`. Use a `StorageProviderFactory` to create the appropriate provider based on configuration.

## Consequences

- Adding a new storage provider requires only a new class implementing `IStorageProvider`
- Each provider can be tested independently
- The `FileTransferService` is now agnostic to storage backend
- Factory manages lifecycle of disposable clients (S3, GCS)

## Files

- `StorageProviders/IStorageProvider.cs`
- `StorageProviders/StorageProviderFactory.cs`
- `StorageProviders/LocalStorageProvider.cs`
- `StorageProviders/S3StorageProvider.cs`
- `StorageProviders/AzureStorageProvider.cs`
- `StorageProviders/GcsStorageProvider.cs`
- `StorageProviders/HttpStorageProvider.cs`
