param(
    [string]$DotNetPath = ".\build\.dotnet\dotnet.exe"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dotnet = Join-Path $repoRoot $DotNetPath

if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = "dotnet"
}

Push-Location $repoRoot
try {
    & $dotnet test ".\tests\FolderGate.IntegrationTests\FolderGate.IntegrationTests.csproj" `
        --filter "FullyQualifiedName~ActualAclBehaviorIntegrationTests.HardenedLock_BlocksCommonOperations_ThenUnlockRestoresAclAndOperations" `
        --verbosity minimal
}
finally {
    Pop-Location
}
