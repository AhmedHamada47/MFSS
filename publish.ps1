param(
    [string]$Runtime = "win-x64",
    [string]$Output = "./dist/mfss-$Runtime",
    [switch]$SelfContained = $true,
    [switch]$SingleFile = $false
)

$config = if ($SelfContained) { "release" } else { "Release" }

Write-Host "Publishing MFSS for $Runtime..." -ForegroundColor Cyan

$args = @(
    "publish",
    "-c", $config,
    "-r", $Runtime,
    "-o", $Output,
    "--self-contained", $SelfContained.ToString().ToLower()
)

if ($SingleFile) { $args += "-p:PublishSingleFile=true" }

dotnet $args

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublished to: $Output" -ForegroundColor Green
    Write-Host "Run: .\$Output\mfss.exe --help" -ForegroundColor Cyan
} else {
    Write-Host "Publish failed." -ForegroundColor Red
}
