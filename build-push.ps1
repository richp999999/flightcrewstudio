# Builds the FlightCrewStudio image and pushes it to Docker Hub.
#
#   .\build-push.ps1                      # pushes <repo>:latest and <repo>:<yyyyMMdd-HHmm>
#   .\build-push.ps1 -Repository me/fcs   # another Docker Hub repository
#   .\build-push.ps1 -Platform linux/arm64  # for an ARM server (e.g. Raspberry/Orange Pi)
#
# Run `docker login` once first. Then on the server: `docker compose pull; docker compose up -d`.
param(
    [string]$Repository = "richp999999/flightcrewstudio",
    [string]$Platform = "linux/amd64"
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$version = Get-Date -Format "yyyyMMdd-HHmm"
Write-Host "Building $Repository ($version, $Platform)..."
docker buildx build --platform $Platform `
    -t "${Repository}:latest" -t "${Repository}:$version" `
    --push .
if ($LASTEXITCODE -ne 0) { throw "Build/push failed." }
Write-Host "Pushed ${Repository}:latest and ${Repository}:$version"
