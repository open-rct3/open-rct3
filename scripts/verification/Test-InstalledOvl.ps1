[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:OPENRCT3_VERIFY_INSTALLED -ne '1') {
  Write-Output 'SKIP: set OPENRCT3_VERIFY_INSTALLED=1 to run installed-OVL verification.'
  exit 0
}

$rct3Path = $env:RCT3_PATH
if ([string]::IsNullOrWhiteSpace($rct3Path) -or -not (Test-Path -LiteralPath $rct3Path -PathType Container)) {
  Write-Output 'SKIP: RCT3_PATH does not identify an installed RCT3 asset directory.'
  exit 0
}

$requiredAssets = @(
  'Main.common.ovl',
  'Characters\AF\AF01_Body_Main.common.ovl',
  'terrain\RCT3\Terrain_RCT3.common.ovl'
)
foreach ($asset in $requiredAssets) {
  if (-not (Test-Path -LiteralPath (Join-Path $rct3Path $asset) -PathType Leaf)) {
    Write-Output "SKIP: the RCT3 installation is missing required asset $asset."
    exit 0
  }
}

. (Join-Path $PSScriptRoot 'TestResults.ps1')

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repo 'OpenCobra\Tests\Integration\IntegrationTests.csproj'
$settings = Join-Path $PSScriptRoot 'verification.runsettings'
$results = Join-Path $repo 'TestResults\installed-ovl'
$solutionDirectory = $repo.Replace('\', '/') + '/'
$filter = 'FullyQualifiedName~OpenCobra.Tests.Integration.TextureDecodeVerification|' +
  'FullyQualifiedName~OpenCobra.Tests.Integration.IngestionTests'

if (Test-Path -LiteralPath $results) {
  Remove-Item -LiteralPath $results -Recurse -Force
}
New-Item -ItemType Directory -Path $results -Force | Out-Null

Push-Location $repo
try {
  & dotnet test $project `
    --settings $settings `
    --filter $filter `
    --logger 'trx;LogFileName=installed-ovl.trx' `
    --results-directory $results `
    --verbosity normal `
    "-p:SolutionDir=$solutionDirectory"
  if ($LASTEXITCODE -ne 0) { throw "Installed-OVL tests failed with exit code $LASTEXITCODE." }
} finally {
  Pop-Location
}

$summary = Get-TrxSummary -ResultsDirectory $results
Write-TestSummary -Name 'Installed OVL' -Summary $summary
