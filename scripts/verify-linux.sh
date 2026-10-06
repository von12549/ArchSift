#!/usr/bin/env bash
set -euo pipefail
dotnet --version
git --version
dotnet restore ArchSift.slnx --locked-mode --configfile NuGet.Config --source /feed --disable-parallel -p:NuGetAudit=false
dotnet build ArchSift.slnx --no-restore --disable-build-servers -p:UseSharedCompilation=false -c Release
dotnet test tests/ArchSift.UnitTests --no-restore --no-build -c Release --logger trx --results-directory /work/test-results
dotnet test tests/ArchSift.ArchitectureTests --no-restore --no-build -c Release --logger trx --results-directory /work/test-results
# SDK 9-specific assembly fixtures run on Windows; Linux executes comparison, real Worker/metadata, CLI, and HTTP tests separately.
dotnet test tests/ArchSift.IntegrationTests --no-restore --no-build -c Release --filter 'FullyQualifiedName!~SupportedEarlierTfmsBuildAndAnalyzeRealArtifacts' --logger trx --results-directory /work/test-results
dotnet publish src/ArchSift.Cli --no-restore -c Release -p:UseAppHost=false -o /work/cli
dotnet /work/cli/archsift.dll --version
dotnet /work/cli/archsift.dll verify --config /work/linux-config.json > /work/linux-report.json
