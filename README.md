# MFSS - Migration File Storage System

[![Build & Test](https://github.com/Ahmed-Hamada0/MFSS/actions/workflows/ci.yml/badge.svg)](https://github.com/Ahmed-Hamada0/MFSS/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/MFSS.svg)](https://www.nuget.org/packages/MFSS)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A high-performance .NET CLI tool for migrating files between **any cloud storage providers** (Amazon S3, Azure Blob Storage, Google Cloud Storage, HTTP sources, or local filesystem) with full tracking, retry logic, circuit breaker pattern, and rollback support.

## ✨ Features

- **🚀 High Performance** — Parallel downloads with configurable concurrency
- **☁️ Any-to-Any Cloud Storage** — Transfer between S3, Azure Blob, Google Cloud Storage, HTTP, and local filesystem
- **🔄 Resumable** — Safely re-run after crashes; duplicate records are skipped
- **♻️ Rollback** — Delete migrated files from destination with a single command
- **🛡️ Circuit Breaker** — Automatically stops requests after consecutive failures
- **🔁 Retry with Backoff** — Exponential backoff with jitter for transient failures
- **⚡ Rate Limiting** — Token-bucket rate limiter to avoid overwhelming sources
- **📊 Progress Tracking** — Real-time progress with throughput metrics
- **🔒 Secure** — Environment variable resolution, secret masking in logs
- **✅ Config Validation** — Validates all settings before migration starts
- **🛑 Graceful Shutdown** — Ctrl+C finishes current operations cleanly
- **📋 Per-Table Logging** — Separate migration log tables per source table
- **📦 Self-Contained Deploy** — Publish as a single-file executable with no dependencies
- **🧪 Tested** — Comprehensive unit test suite

## 📦 Installation

### As a .NET Global Tool (CLI / PowerShell)

```bash
dotnet tool install -g MFSS
```

Then use from any terminal (CMD, PowerShell, Bash):

```bash
mfss --mode migrate --config appsettings.json
```

### From Source

```bash
git clone https://github.com/Ahmed-Hamada0/MFSS.git
cd MFSS
dotnet build
dotnet run -- --mode migrate
```

### Self-Contained Executable

```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish
./publish/MFSS --mode migrate --config appsettings.json
```

Available runtime identifiers: `linux-x64`, `win-x64`, `osx-x64`, `osx-arm64`.

## 🚀 Quick Start

### 1. Create Configuration

Create an `appsettings.json` in your working directory:

```json
{
    "Migration": {
        "Name": "my-migration",
        "Mode": "migrate",
        "DryRun": false,
        "FreshStart": false,
        "ParallelDownloads": 4,
        "MaxRetries": 3,
        "RateLimitPerSecond": 10,
        "MaxFileSizeMB": 100
    },
    "SourceDb": {
        "ConnectionString": "Server=${DB_HOST};Database=source_db;User=${DB_USER};******;",
        "Tables": [
            {
                "TableName": "products",
                "UrlColumn": "image_url",
                "IdColumn": "id"
            }
        ]
    },
    "SourceFileSystem": {
        "Type": "http",
        "BasePath": ""
    },
    "DestinationFileSystem": {
        "Type": "s3",
        "BucketName": "my-bucket",
        "Region": "us-east-1",
        "AccessKey": "${AWS_ACCESS_KEY}",
        "SecretKey": "${AWS_SECRET_KEY}"
    },
    "DestinationDb": {
        "ConnectionString": "Server=${DB_HOST};Database=migration_logs;User=${DB_USER};******;",
        "SeparateTablesPerSource": true
    },
    "ThirdDb": {
        "Enabled": false,
        "ConnectionString": "",
        "UpdateQuery": "UPDATE media SET url = {url} WHERE id = {id}"
    }
}
```

### 2. Set Environment Variables

```bash
# Linux/Mac
export DB_HOST=localhost
export DB_USER=root
export DB_PASS=secret
export AWS_ACCESS_KEY=AKIA...
export AWS_SECRET_KEY=...

# PowerShell
$env:DB_HOST = "localhost"
$env:DB_USER = "root"
$env:DB_PASS = "secret"
$env:AWS_ACCESS_KEY = "AKIA..."
$env:AWS_SECRET_KEY = "..."
```

You can also use the `MFSS_` prefix with environment variables for override (e.g., `MFSS_Migration__DryRun=true`).

### 3. Run Migration

```bash
# Preview (no changes)
mfss --dry-run

# Run migration
mfss --mode migrate

# Rollback (delete from destination)
mfss --mode rollback

# Verbose output
mfss --mode migrate --verbose

# Custom config path
mfss --config /path/to/config.json --mode migrate
```

## 📖 CLI Reference

| Option | Description | Default |
|--------|-------------|---------|
| `--mode` | Migration mode: `migrate` or `rollback` | `migrate` |
| `--dry-run` | Preview without making changes | `false` |
| `--config` | Path to configuration file | `appsettings.json` |
| `--verbose` | Show detailed per-file progress | `false` |
| `--version` | Show version information | — |
| `--help` | Show help | — |

## ⚙️ Configuration Reference

### Migration Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Name` | string | `"default"` | Migration name (used in log filenames) |
| `Mode` | string | `"migrate"` | `migrate` or `rollback` |
| `DryRun` | bool | `false` | Preview mode |
| `FreshStart` | bool | `false` | Drop and recreate log tables |
| `ParallelDownloads` | int | `4` | Max concurrent file transfers (1-100) |
| `MaxRetries` | int | `3` | Max retry attempts per file (1-20) |
| `RateLimitPerSecond` | int | `10` | Max requests per second (1-1000) |
| `MaxFileSizeMB` | int | `100` | Max individual file size (1-10240) |

### Source Database

| Setting | Type | Description |
|---------|------|-------------|
| `ConnectionString` | string | MySQL connection string (supports `${ENV_VAR}` placeholders) |
| `Tables` | array | List of tables to migrate |
| `Tables[].TableName` | string | Source table name |
| `Tables[].UrlColumn` | string | Column containing file URLs |
| `Tables[].IdColumn` | string | Primary key column |

### Source File System

| Setting | Type | Description |
|---------|------|-------------|
| `Type` | string | `http`, `s3`, `azure`, `gcs`, or `local` |
| `BucketName` | string | S3 bucket name (required for S3 source) |
| `Region` | string | AWS region (required for S3 source) |
| `AccessKey` | string | AWS access key (supports `${ENV_VAR}`) |
| `SecretKey` | string | AWS secret key (supports `${ENV_VAR}`) |
| `AzureConnectionString` | string | Azure Blob connection string (required for Azure source) |
| `ContainerName` | string | Azure container name (required for Azure source) |
| `GcsBucket` | string | GCS bucket name (required for GCS source) |
| `GcsCredentialPath` | string | Path to GCS service account JSON |
| `GcsProjectId` | string | GCS project ID |
| `BasePath` | string | Local base path (required for local source) |

### Destination File System

| Setting | Type | Description |
|---------|------|-------------|
| `Type` | string | `s3`, `azure`, `gcs`, or `local` |
| `BucketName` | string | S3 bucket name (required for S3) |
| `Region` | string | AWS region (required for S3) |
| `AccessKey` | string | AWS access key (supports `${ENV_VAR}`) |
| `SecretKey` | string | AWS secret key (supports `${ENV_VAR}`) |
| `AzureConnectionString` | string | Azure Blob connection string (required for Azure) |
| `ContainerName` | string | Azure container name (required for Azure) |
| `GcsBucket` | string | GCS bucket name (required for GCS) |
| `GcsCredentialPath` | string | Path to GCS service account JSON |
| `GcsProjectId` | string | GCS project ID |
| `BasePath` | string | Local base path (required for local) |

### Destination Database (Migration Log)

| Setting | Type | Description |
|---------|------|-------------|
| `ConnectionString` | string | MySQL connection string for log database |
| `SeparateTablesPerSource` | bool | Create per-source-table log tables |

### Third-Party Database (Optional)

| Setting | Type | Description |
|---------|------|-------------|
| `Enabled` | bool | Enable third-party DB updates |
| `ConnectionString` | string | MySQL connection string |
| `UpdateQuery` | string | SQL with `{id}` and `{url}` placeholders |

## 🏗️ Architecture

```
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐
│  Source DB   │────▶│   MFSS CLI   │────▶│  Destination    │
│  (MySQL)     │     │              │     │  (S3/Azure/GCS/ │
└─────────────┘     │  ┌────────┐  │     │   Local)        │
                    │  │Retry   │  │     └─────────────────┘
┌─────────────┐     │  │Policy  │  │
│  Source      │────▶│  ├────────┤  │     ┌─────────────────┐
│  (HTTP/S3/   │     │  │Circuit │  │────▶│  Log DB (MySQL) │
│  Azure/GCS/  │     │  │Breaker │  │     │  MigrationLog_* │
│  Local)      │     │  ├────────┤  │     └─────────────────┘
                    │  │Rate    │  │
                    │  │Limiter │  │     ┌─────────────────┐
                    │  └────────┘  │────▶│  Third DB       │
                    └──────────────┘     │  (Optional)     │
                                        └─────────────────┘
```

### Key Services

| Service | Responsibility |
|---------|---------------|
| `SourceDbService` | Fetches file URLs from source tables |
| `FileTransferService` | Downloads and uploads files between any cloud storage |
| `DestinationDbService` | Manages migration log tables |
| `ThirdDbService` | Updates third-party DB with new URLs |
| `CircuitBreaker` | Stops requests after consecutive failures |
| `RetryPolicy` | Exponential backoff with jitter |
| `ProgressTracker` | Thread-safe progress metrics |
| `ConfigValidator` | Pre-flight configuration validation |
| `EnvConfigResolver` | Resolves `${ENV_VAR}` placeholders |
| `SecretMasker` | Masks passwords in log output |

## 🔄 Resumability

MFSS is fully resumable. If a migration is interrupted:

1. Records use `INSERT IGNORE` with a unique constraint — duplicates are skipped
2. Only records with `Status = 'pending'` are processed on re-run
3. Failed records can be retried by re-running (up to `MaxRetries`)

Just run the same command again:

```bash
mfss --mode migrate
```

## 🧪 Running Tests

```bash
dotnet test MFSS.Tests/
```

## 📦 Publishing to NuGet

```bash
# Pack the tool
dotnet pack -c Release

# Push to NuGet
dotnet nuget push ./nupkg/MFSS.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## 📦 Self-Contained Deployment

You can publish MFSS as a single-file, self-contained executable that requires no .NET runtime on the target machine:

```bash
# Linux
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# Windows
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish
```

Then copy the executable and your `appsettings.json` to the target machine:

```bash
# Run migration
./publish/MFSS --mode migrate --config appsettings.json

# Dry run
./publish/MFSS --dry-run --config appsettings.json
```

## 📄 License

MIT — see [LICENSE](LICENSE)
