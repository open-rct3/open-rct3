[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'NativeSmokeAssertions.ps1')
. (Join-Path $PSScriptRoot 'NativeSmokeEvidence.ps1')
. (Join-Path $PSScriptRoot 'TestResults.ps1')

function Assert-Throws {
  param(
    [Parameter(Mandatory = $true)][scriptblock]$Action,
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$MessagePattern = $null
  )

  try {
    & $Action
  } catch {
    if (-not [string]::IsNullOrWhiteSpace($MessagePattern) -and
        $_.Exception.Message -notmatch $MessagePattern) {
      throw "Harness self-test '$Name' failed for the wrong reason: $($_.Exception.Message)"
    }
    return
  }
  throw "Harness self-test '$Name' expected an error, but the assertion passed."
}

function Write-SyntheticTrx {
  param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][string]$Outcome,
    [string]$RejectedTest = 'Harness.Tests.RejectedCase',
    [string]$FileName = 'synthetic.trx',
    [string]$StoragePath = $null,
    [switch]$CorruptPassedCounter,
    [switch]$DuplicateDefinitionId,
    [switch]$DuplicateResultTestId,
    [switch]$DuplicateResultExecutionId
  )

  New-Item -ItemType Directory -Path $Directory -Force | Out-Null
  if ([string]::IsNullOrWhiteSpace($StoragePath)) {
    $StoragePath = Join-Path $Directory 'Harness.Tests.dll'
  }
  $StoragePath = [System.IO.Path]::GetFullPath($StoragePath)
  $escapedStoragePath = [System.Security.SecurityElement]::Escape($StoragePath)
  $counterNames = @(
    'failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted',
    'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')
  $counterValues = @{}
  foreach ($counterName in $counterNames) { $counterValues[$counterName] = 0 }
  $counterNameByOutcome = @{
    Failed = 'failed'; Error = 'error'; Timeout = 'timeout'; Aborted = 'aborted'
    Inconclusive = 'inconclusive'; PassedButRunAborted = 'passedButRunAborted'
    NotRunnable = 'notRunnable'; Disconnected = 'disconnected'; Warning = 'warning'
    Completed = 'completed'; InProgress = 'inProgress'; Pending = 'pending'
  }
  if ($counterNameByOutcome.ContainsKey($Outcome)) {
    $counterValues[$counterNameByOutcome[$Outcome]] = 1
  }
  if ($Outcome -eq 'NotExecuted') { $counterValues.notExecuted = 1 }
  $rejectedExecuted = if ($Outcome -eq 'NotExecuted') { 0 } else { 1 }
  $expectedPassed = if ($Outcome -eq 'Passed') { 2 } else { 1 }
  $passedCounter = if ($CorruptPassedCounter) { $expectedPassed - 1 } else { $expectedPassed }
  $counterAttributes = ($counterNames | ForEach-Object {
    "$_=`"$($counterValues[$_])`""
  }) -join ' '
  $rejectedParts = $RejectedTest.Split('.')
  $rejectedMethod = $rejectedParts[-1]
  $rejectedClass = $RejectedTest.Substring(0, $RejectedTest.Length - $rejectedMethod.Length - 1)
  $rejectedDefinitionId = if ($DuplicateDefinitionId) { 'pass-id' } else { 'rejected-id' }
  $rejectedResultTestId = if ($DuplicateResultTestId) { 'pass-id' } else { 'rejected-id' }
  $rejectedExecutionId = if ($DuplicateResultExecutionId) {
    'pass-execution-id'
  } else {
    'rejected-execution-id'
  }
  @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult executionId="pass-execution-id" testId="pass-id" testName="PassedCase" outcome="Passed" />
    <UnitTestResult executionId="$rejectedExecutionId" testId="$rejectedResultTestId" testName="$rejectedMethod" outcome="$Outcome" />
  </Results>
  <TestDefinitions>
    <UnitTest id="pass-id" storage="$escapedStoragePath"><TestMethod className="Harness.Tests" name="PassedCase" /></UnitTest>
    <UnitTest id="$rejectedDefinitionId" storage="$escapedStoragePath"><TestMethod className="$rejectedClass" name="$rejectedMethod" /></UnitTest>
  </TestDefinitions>
  <ResultSummary>
    <Counters total="2" executed="$($rejectedExecuted + 1)" passed="$passedCounter" $counterAttributes />
  </ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath (Join-Path $Directory $FileName) -Encoding UTF8
}

function New-PassedEvidence {
  param(
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Map
  )

  $nonce = '0123456789abcdef0123456789abcdef'
  $artifactDirectory = Split-Path $Executable -Parent
  New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
  "native executable content $nonce" | Set-Content -LiteralPath $Executable -Encoding UTF8
  "map content $nonce" | Set-Content -LiteralPath $Map -Encoding UTF8
  $transcriptPath = Join-Path $artifactDirectory 'native-smoke.txt'
  $applicationLogPath = Join-Path $artifactDirectory 'app.log'
  @("run-id=$nonce", 'outcome=passed') |
    Set-Content -LiteralPath $transcriptPath -Encoding UTF8
  "run=$nonce|2026-01-01|INFO|Harness|real-content" |
    Set-Content -LiteralPath $applicationLogPath -Encoding UTF8
  $executableHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
  $mapFileHash = (Get-FileHash -LiteralPath $Map -Algorithm SHA256).Hash
  $evidence = New-NativeSmokeEvidenceRecord -RepoPath $Repo -RunNonce $nonce
  $evidence.outcome = 'passed'
  $evidence.executable = [PSCustomObject]@{
    available = $true
    path = $Executable
    preLaunchSha256 = $executableHash
    postLaunchSha256 = [PSCustomObject]@{ available = $true; value = $executableHash }
  }
  $evidence.requestedMap =
    [PSCustomObject]@{ available = $true; path = $Map; sha256 = $mapFileHash }
  $evidence.loadedMap =
    [PSCustomObject]@{ available = $true; path = $Map; sha256 = $mapFileHash }
  $evidence.process = [PSCustomObject]@{
    available = $true
    pid = 42
    hwnd = 84
    startTimeUtc = '2026-01-01T00:00:00.0000000Z'
    executablePath = $Executable
  }
  $evidence.window.observedDpi = [PSCustomObject]@{ available = $true; value = 96 }
  $evidence.window.observedClientSize =
    [PSCustomObject]@{ available = $true; width = 1280; height = 720 }
  $evidence.cleanup.attempted = $true
  $evidence.cleanup.closeResult = 'graceful'
  $evidence.cleanup.processExited = $true
  $evidence.artifacts.transcript = [PSCustomObject]@{
    available = $true
    path = $transcriptPath
    sha256 = (Get-FileHash -LiteralPath $transcriptPath -Algorithm SHA256).Hash
  }
  $evidence.artifacts.applicationLog = [PSCustomObject]@{
    available = $true
    path = $applicationLogPath
    sha256 = (Get-FileHash -LiteralPath $applicationLogPath -Algorithm SHA256).Hash
  }
  return $evidence
}

function Invoke-LoadedMapProbe {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$ConfiguredMapPath,
    [string]$EnvironmentMapPath = $null
  )

  $caseDirectory = Join-Path $results "loaded-map-$Name"
  $appData = Join-Path $caseDirectory 'AppData'
  $configDirectory = Join-Path $appData 'OpenRCT3'
  $installPath = Join-Path $caseDirectory 'install'
  New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
  @{
    InstallPath = $installPath
    MapPath = $ConfiguredMapPath
    SuppressCrashAlerts = $true
  } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $configDirectory 'config.json') -Encoding UTF8

  $probeNlog = Join-Path $caseDirectory 'nlog.config'
  Copy-Item -LiteralPath $builtNlogPath -Destination $probeNlog
  [xml]$probeConfig = Get-Content -Raw -LiteralPath $probeNlog
  $probeFileTarget = $probeConfig.SelectSingleNode("//*[local-name()='target' and @name='file']")
  $probeLogPath = Join-Path $caseDirectory 'app.log'
  $probeFileTarget.SetAttribute('fileName', $probeLogPath)
  $probeFileTarget.SetAttribute(
    'layout',
    'run=${environment:variable=OPENRCT3_SMOKE_RUN_ID}|${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}')
  $probeConfig.Save($probeNlog)

  $originalAppData = $env:APPDATA
  $originalMapPath = $env:OPENRCT3_MAP_PATH
  $originalRunId = $env:OPENRCT3_SMOKE_RUN_ID
  $env:APPDATA = $appData
  $env:OPENRCT3_MAP_PATH = $EnvironmentMapPath
  $env:OPENRCT3_SMOKE_RUN_ID = "probe-$Name"
  try {
    $probeOutput = & powershell -NoProfile -ExecutionPolicy Bypass `
      -File (Join-Path $PSScriptRoot 'Test-LoadedMapMarker.ps1') `
      -RepoPath $repo `
      -NlogConfigPath $probeNlog `
      -InstallPath $installPath `
      -ConfiguredMapPath $ConfiguredMapPath 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
      throw "Loaded-map application probe '$Name' failed: $probeOutput"
    }
  } finally {
    $env:APPDATA = $originalAppData
    $env:OPENRCT3_MAP_PATH = $originalMapPath
    $env:OPENRCT3_SMOKE_RUN_ID = $originalRunId
  }
  return [PSCustomObject]@{
    LogPath = $probeLogPath
    RunId = "probe-$Name"
  }
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$results = Join-Path $repo 'TestResults\harness-self-tests'
if (Test-Path -LiteralPath $results) {
  Remove-Item -LiteralPath $results -Recurse -Force
}
New-Item -ItemType Directory -Path $results -Force | Out-Null

$logPath = Join-Path $results 'app.log'
$runId = 'current-run'
$mapPath = Join-Path $results 'fallback-map.dat'
$wrongMapPath = Join-Path $results 'wrong-map.dat'
$mapHash = 'A' * 64
$wrongMapHash = 'B' * 64
$stalePrefix = 'run=stale-run|2026-01-01|'
$currentPrefix = "run=$runId|2026-01-01|"
$mapJson = @{ path = $mapPath; sha256 = $mapHash } | ConvertTo-Json -Compress
$wrongMapJson = @{ path = $wrongMapPath; sha256 = $wrongMapHash } | ConvertTo-Json -Compress

@(
  "${stalePrefix}INFO|OpenRCT3.Program|Starting OpenRCT3 on Windows...",
  "${stalePrefix}DEBUG|OpenRCT3.Game|Game world loaded",
  "${stalePrefix}DEBUG|OpenRCT3.Game|Added terrain mesh",
  "${stalePrefix}DEBUG|OpenRCT3.Platforms.Windows.GameWindow|Presented initial scene frame",
  "${stalePrefix}INFO|OpenRCT3.Simulation.Terrain|Native smoke loaded map $mapJson",
  "${currentPrefix}INFO|OpenRCT3.Program|Starting OpenRCT3 on Windows..."
) | Set-Content -LiteralPath $logPath -Encoding UTF8
$state = Get-NativeSmokeLogState `
  -LogPath $logPath -RunId $runId -ExpectedMapPath $mapPath -ExpectedMapSha256 $mapHash
Assert-Throws { Assert-NativeSmokeCompletion -State $state } `
  'stale markers cannot pass a fresh run' 'required completion markers'

Add-Content -LiteralPath $logPath -Value @(
  "${currentPrefix}DEBUG|OpenRCT3.Game|Game world loaded",
  "${currentPrefix}DEBUG|OpenRCT3.Game|Added terrain mesh",
  "${currentPrefix}DEBUG|OpenRCT3.Platforms.Windows.GameWindow|Presented initial scene frame",
  "${currentPrefix}INFO|OpenRCT3.Simulation.Terrain|Native smoke loaded map $wrongMapJson"
)
$state = Get-NativeSmokeLogState `
  -LogPath $logPath -RunId $runId -ExpectedMapPath $mapPath -ExpectedMapSha256 $mapHash
Assert-Throws { Assert-NativeSmokeCompletion -State $state } `
  'application wrong-map identity fails' 'different map path'

@(
  "${currentPrefix}INFO|OpenRCT3.Program|Starting OpenRCT3 on Windows...",
  "${currentPrefix}DEBUG|OpenRCT3.Game|Game world loaded",
  "${currentPrefix}DEBUG|OpenRCT3.Game|Added terrain mesh",
  "${currentPrefix}DEBUG|OpenRCT3.Platforms.Windows.GameWindow|Presented initial scene frame",
  "${currentPrefix}INFO|OpenRCT3.Simulation.Terrain|Native smoke loaded map $mapJson"
) | Set-Content -LiteralPath $logPath -Encoding UTF8
$state = Get-NativeSmokeLogState `
  -LogPath $logPath -RunId $runId -ExpectedMapPath $mapPath -ExpectedMapSha256 $mapHash
Assert-NativeSmokeCompletion -State $state

Add-Content -LiteralPath $logPath -Value "${currentPrefix}FATAL|OpenRCT3.Program|synthetic failure"
$state = Get-NativeSmokeLogState `
  -LogPath $logPath -RunId $runId -ExpectedMapPath $mapPath -ExpectedMapSha256 $mapHash
Assert-Throws { Assert-NativeSmokeCompletion -State $state } `
  'shutdown-time correlated fatal event fails the final log refresh' 'ERROR or FATAL'

Assert-NativeSmokeFileLoggingConfiguration -ConfigPath (Join-Path $repo 'OpenRCT3\nlog.config')

$loggingContractDirectory = Join-Path $results 'isolated-logging-contract'
$isolatedApplicationData = Join-Path $loggingContractDirectory 'AppData'
$isolatedLogPath = Join-Path $isolatedApplicationData 'OpenRCT3\logs\app.log'
$isolatedArchivePath = Join-Path $isolatedApplicationData 'OpenRCT3\logs\app.archive.log'
$isolatedNlogPath = Join-Path $loggingContractDirectory 'nlog.config'
New-Item -ItemType Directory -Path $loggingContractDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'OpenRCT3\nlog.config') -Destination $isolatedNlogPath
Set-NativeSmokeIsolatedLoggingConfiguration `
  -ConfigPath $isolatedNlogPath `
  -ApplicationDataPath $isolatedApplicationData `
  -LogPath $isolatedLogPath `
  -ArchiveLogPath $isolatedArchivePath

$outsideLogPath = Join-Path $loggingContractDirectory 'outside.log'
Assert-Throws {
  Set-NativeSmokeIsolatedLoggingConfiguration `
    -ConfigPath $isolatedNlogPath `
    -ApplicationDataPath $isolatedApplicationData `
    -LogPath $outsideLogPath `
    -ArchiveLogPath $isolatedArchivePath
} 'native logging rejects a path outside isolated application data' 'must stay inside'

$knownApplicationData = [Environment]::GetFolderPath(
  [Environment+SpecialFolder]::ApplicationData)
if (-not [string]::IsNullOrWhiteSpace($knownApplicationData)) {
  Assert-Throws {
    Assert-NativeSmokeApplicationDataRoot -ApplicationDataPath $knownApplicationData
  } 'native logging rejects the user profile application-data root' 'must not be the user profile'
}

Add-Type -AssemblyName System.Drawing
$blankCapturePath = Join-Path $loggingContractDirectory 'blank.png'
$blankCapture = New-Object System.Drawing.Bitmap 160, 120
$blankGraphics = [System.Drawing.Graphics]::FromImage($blankCapture)
try {
  $blankGraphics.Clear([System.Drawing.Color]::White)
  $blankCapture.Save($blankCapturePath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
  $blankGraphics.Dispose()
  $blankCapture.Dispose()
}
Assert-Throws { Assert-NativeSmokeScreenshotContent -Path $blankCapturePath } `
  'native screenshot rejects a uniform client area' 'no rendered scene is visible'

$renderedCapturePath = Join-Path $loggingContractDirectory 'rendered.png'
$renderedCapture = New-Object System.Drawing.Bitmap 160, 120
$renderedGraphics = [System.Drawing.Graphics]::FromImage($renderedCapture)
try {
  $renderedGraphics.Clear([System.Drawing.Color]::CornflowerBlue)
  $renderedGraphics.FillRectangle(
    [System.Drawing.Brushes]::ForestGreen,
    0,
    60,
    160,
    60)
  $renderedCapture.Save($renderedCapturePath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
  $renderedGraphics.Dispose()
  $renderedCapture.Dispose()
}
Assert-NativeSmokeScreenshotContent -Path $renderedCapturePath

$nativeSmokeSource = Get-Content -Raw -LiteralPath (
  Join-Path $repo 'scripts\verification\Test-NativeSmoke.ps1')
if ($nativeSmokeSource -notmatch 'Use OpenRCT3 MCP') {
  throw 'Native inspection must direct callers to OpenRCT3 MCP.'
}
if (Test-Path -LiteralPath (Join-Path $repo '.claude/skills/drive-native-app/scripts/AppDriver.ps1')) {
  throw 'The removed desktop-input driver must not be restored.'
}

$terrainSource = Get-Content -Raw -LiteralPath (Join-Path $repo 'OpenRCT3\Simulation\Terrain.cs')
foreach ($requiredReadBoundary in @(
    'File.ReadAllBytes(loadedMapPath)',
    'new MemoryStream(loadedMapBytes, writable: false)',
    'DatTerrainReader.Read(loadedMap)',
    'SHA256.HashData(loadedMapBytes)')) {
  if ($terrainSource.IndexOf($requiredReadBoundary, [StringComparison]::Ordinal) -lt 0) {
    throw "Terrain smoke identity is not derived from the immutable parsed bytes: $requiredReadBoundary"
  }
}

$openRct3Assembly = Get-ChildItem -Path (Join-Path $repo 'OpenRCT3\bin\Debug') `
  -Filter 'OpenRCT3.dll' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1 `
  -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($openRct3Assembly)) {
  throw 'The loaded-map application probe requires the make test-build output.'
}
$builtNlogPath = Join-Path (Split-Path $openRct3Assembly -Parent) 'nlog.config'
$parkFixture = Join-Path $results 'loaded-map-fixture.dat'
$fallbackProbe = Invoke-LoadedMapProbe -Name 'fallback' -ConfiguredMapPath $parkFixture
$parkHash = (Get-FileHash -LiteralPath $parkFixture -Algorithm SHA256).Hash
$fallbackState = Get-NativeSmokeLogState `
  -LogPath $fallbackProbe.LogPath `
  -RunId $fallbackProbe.RunId `
  -ExpectedMapPath $parkFixture `
  -ExpectedMapSha256 $parkHash
if ($fallbackState.LoadedMapCount -ne 1 -or -not $fallbackState.LoadedMapPathMatches -or
    -not $fallbackState.LoadedMapHashMatches) {
  throw 'Application fallback-map marker did not identify the config-selected fixture.'
}

$wrongApplicationMap = Join-Path $results 'environment-selected-wrong-map.dat'
Copy-Item -LiteralPath $parkFixture -Destination $wrongApplicationMap
$wrongProbe = Invoke-LoadedMapProbe `
  -Name 'wrong' `
  -ConfiguredMapPath $parkFixture `
  -EnvironmentMapPath $wrongApplicationMap
$wrongState = Get-NativeSmokeLogState `
  -LogPath $wrongProbe.LogPath `
  -RunId $wrongProbe.RunId `
  -ExpectedMapPath $parkFixture `
  -ExpectedMapSha256 $parkHash
if ($wrongState.LoadedMapCount -ne 1 -or $wrongState.LoadedMapPathMatches) {
  throw 'Application wrong-map probe did not expose environment-path precedence.'
}

$configPath = Join-Path $results 'config.json'
$installPath = Join-Path $results 'install'
@{
  InstallPath = $installPath
  MapPath = $mapPath
  SuppressCrashAlerts = $true
} | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
Assert-NativeSmokeConfig -ConfigPath $configPath -InstallPath $installPath -MapPath $mapPath
Assert-Throws {
  Assert-NativeSmokeConfig -ConfigPath $configPath -InstallPath $installPath -MapPath $wrongMapPath
} 'ignored selected map fails config validation' 'MapPath mismatch'

$approvedSkip = 'Dumper.Tests.TruncatedLabelTests.TestVeryLongPath_PreservesFilename'
$approvedTrx = Join-Path $results 'trx-approved-skip'
$approvedStorage = Join-Path $approvedTrx 'Harness.Tests.dll'
Write-SyntheticTrx `
  -Directory $approvedTrx `
  -Outcome NotExecuted `
  -RejectedTest $approvedSkip `
  -StoragePath $approvedStorage
$approvedSummary = Get-TrxSummary `
  -ResultsDirectory $approvedTrx `
  -ApprovedSkippedTests @($approvedSkip) `
  -ExpectedTestAssemblies @($approvedStorage)
if ($approvedSummary.Passed -ne 1 -or $approvedSummary.Skipped -ne 1) {
  throw 'Approved TRX skip was not reported honestly.'
}

$rejectedOutcomes = @(
  'NotExecuted', 'Failed', 'Error', 'Timeout', 'Aborted', 'Inconclusive',
  'PassedButRunAborted', 'NotRunnable', 'Disconnected', 'Warning', 'Completed',
  'InProgress', 'Pending')
foreach ($outcome in $rejectedOutcomes) {
  $outcomeDirectory = Join-Path $results "trx-rejected-$outcome"
  Write-SyntheticTrx -Directory $outcomeDirectory -Outcome $outcome
  Assert-Throws { Get-TrxSummary -ResultsDirectory $outcomeDirectory } `
    "TRX rejects $outcome" 'Rejected'
}
$countMismatch = Join-Path $results 'trx-passed-count-mismatch'
Write-SyntheticTrx -Directory $countMismatch -Outcome Passed -CorruptPassedCounter
Assert-Throws { Get-TrxSummary -ResultsDirectory $countMismatch } `
  'TRX rejects passed counter mismatch' 'passed count mismatch'

$duplicateDefinition = Join-Path $results 'trx-duplicate-definition-id'
Write-SyntheticTrx -Directory $duplicateDefinition -Outcome Passed -DuplicateDefinitionId
Assert-Throws { Get-TrxSummary -ResultsDirectory $duplicateDefinition } `
  'TRX rejects duplicate definition IDs' 'duplicate test definition id'
$duplicateResultTestId = Join-Path $results 'trx-duplicate-result-test-id'
Write-SyntheticTrx -Directory $duplicateResultTestId -Outcome Passed -DuplicateResultTestId
Assert-Throws { Get-TrxSummary -ResultsDirectory $duplicateResultTestId } `
  'TRX rejects duplicate result testIds' 'duplicate result testId'
$duplicateExecutionId = Join-Path $results 'trx-duplicate-result-execution-id'
Write-SyntheticTrx `
  -Directory $duplicateExecutionId `
  -Outcome Passed `
  -DuplicateResultExecutionId
Assert-Throws { Get-TrxSummary -ResultsDirectory $duplicateExecutionId } `
  'TRX rejects duplicate result executionIds' 'duplicate result executionId'

$assemblyBoundary = Join-Path $results 'trx-assembly-boundary'
$expectedOpenCobra = Join-Path $assemblyBoundary 'OpenCobra.Tests.dll'
$expectedOpenRct3 = Join-Path $assemblyBoundary 'OpenRCT3.Tests.dll'
$expectedDumper = Join-Path $assemblyBoundary 'Dumper.Tests.dll'
Write-SyntheticTrx `
  -Directory $assemblyBoundary -FileName 'dumper.trx' -Outcome Passed -StoragePath $expectedDumper
Write-SyntheticTrx `
  -Directory $assemblyBoundary -FileName 'openrct3-a.trx' -Outcome Passed -StoragePath $expectedOpenRct3
Write-SyntheticTrx `
  -Directory $assemblyBoundary -FileName 'openrct3-b.trx' -Outcome Passed -StoragePath $expectedOpenRct3
Assert-Throws {
  Get-TrxSummary `
    -ResultsDirectory $assemblyBoundary `
    -ExpectedTestAssemblies @($expectedOpenCobra, $expectedOpenRct3, $expectedDumper)
} 'TRX rejects duplicate OpenRCT3 storage with missing OpenCobra storage' `
  'Duplicate TRX storage identity'

$missingAssembly = Join-Path $results 'trx-missing-assembly'
Write-SyntheticTrx `
  -Directory $missingAssembly -FileName 'dumper.trx' -Outcome Passed -StoragePath $expectedDumper
Write-SyntheticTrx `
  -Directory $missingAssembly -FileName 'openrct3.trx' -Outcome Passed -StoragePath $expectedOpenRct3
Assert-Throws {
  Get-TrxSummary `
    -ResultsDirectory $missingAssembly `
    -ExpectedTestAssemblies @($expectedOpenCobra, $expectedOpenRct3, $expectedDumper)
} 'TRX rejects a missing expected assembly storage' 'has no TRX result'

$unexpectedAssembly = Join-Path $results 'trx-unexpected-assembly'
$foreignAssembly = Join-Path $unexpectedAssembly 'Foreign.Tests.dll'
Write-SyntheticTrx `
  -Directory $unexpectedAssembly -Outcome Passed -StoragePath $foreignAssembly
Assert-Throws {
  Get-TrxSummary `
    -ResultsDirectory $unexpectedAssembly `
    -ExpectedTestAssemblies @($expectedOpenCobra)
} 'TRX rejects an unexpected assembly storage' 'Unexpected TRX storage identity'

$manifestPath = Join-Path $results 'native-smoke.json'
$executablePath = Join-Path $results 'OpenRCT3.exe'
$passedEvidence = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
Write-NativeSmokeEvidenceManifest -Evidence $passedEvidence -Path $manifestPath
$roundTrip = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($roundTrip.outcome -ne 'passed' -or
    $roundTrip.loadedMap.sha256 -ne $roundTrip.requestedMap.sha256) {
  throw 'Native evidence manifest did not preserve its required content.'
}
$missingLoadedMap = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$missingLoadedMap.loadedMap = [PSCustomObject]@{ available = $false; reason = 'marker missing' }
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $missingLoadedMap } `
  'manifest rejects missing loaded map' 'Loaded map identity is unavailable'
$wrongLoadedMap = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$wrongLoadedMap.loadedMap.sha256 = $wrongMapHash
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $wrongLoadedMap } `
  'manifest rejects a loaded-map hash not bound to real content' 'does not match the file content'
$wrongExecutable = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$wrongExecutable.process.executablePath = Join-Path $results 'foreign.exe'
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $wrongExecutable } `
  'manifest rejects foreign process binding' 'executable binding'

$missingExecutable = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
Remove-Item -LiteralPath $missingExecutable.executable.path -Force
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $missingExecutable } `
  'manifest rejects nonexistent executable evidence' 'file does not exist'
$missingMap = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
Remove-Item -LiteralPath $missingMap.requestedMap.path -Force
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $missingMap } `
  'manifest rejects nonexistent map evidence' 'file does not exist'
$tamperedMap = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
Add-Content -LiteralPath $tamperedMap.requestedMap.path -Value 'tampered after hashing'
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $tamperedMap } `
  'manifest rejects map content changed after hashing' 'does not match the file content'
$postLaunchMismatch = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$postLaunchMismatch.executable.postLaunchSha256.value = $wrongMapHash
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $postLaunchMismatch } `
  'manifest rejects executable changed after launch' 'changed between pre-launch and post-launch'

$badNonce = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$badNonce.runNonce = 'caller-supplied'
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $badNonce } `
  'manifest rejects invalid run nonce' 'runNonce'
$missingTranscript = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
Remove-Item -LiteralPath $missingTranscript.artifacts.transcript.path -Force
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $missingTranscript } `
  'manifest rejects nonexistent transcript evidence' 'file does not exist'
$unboundTranscript = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
'no run binding' | Set-Content -LiteralPath $unboundTranscript.artifacts.transcript.path -Encoding UTF8
$unboundTranscript.artifacts.transcript.sha256 =
  (Get-FileHash -LiteralPath $unboundTranscript.artifacts.transcript.path -Algorithm SHA256).Hash
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $unboundTranscript } `
  'manifest rejects transcript without run nonce' 'transcript is not bound'
$unboundApplicationLog = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
'run=foreign|INFO|Harness|wrong run' |
  Set-Content -LiteralPath $unboundApplicationLog.artifacts.applicationLog.path -Encoding UTF8
$unboundApplicationLog.artifacts.applicationLog.sha256 =
  (Get-FileHash -LiteralPath $unboundApplicationLog.artifacts.applicationLog.path `
    -Algorithm SHA256).Hash
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $unboundApplicationLog } `
  'manifest rejects application log without run nonce' 'Application log is not bound'

$boundCapture = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$boundCapturePath = Join-Path $results 'bound-screen.png'
'synthetic screenshot artifact' | Set-Content -LiteralPath $boundCapturePath -Encoding UTF8
$boundCapture.capture.mode = 'screenshot'
$boundCapture.capture.artifacts = @([PSCustomObject]@{
  available = $true
  path = $boundCapturePath
  sha256 = (Get-FileHash -LiteralPath $boundCapturePath -Algorithm SHA256).Hash
})
Assert-NativeSmokeEvidenceRecord -Evidence $boundCapture

$missingCaptureHash = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$capturePath = Join-Path $results 'screen.png'
'real screenshot bytes' | Set-Content -LiteralPath $capturePath -Encoding UTF8
$missingCaptureHash.capture.mode = 'screenshot'
$missingCaptureHash.capture.artifacts = @(
  [PSCustomObject]@{ available = $true; path = $capturePath; sha256 = 'bad' })
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $missingCaptureHash } `
  'manifest rejects unhashed screenshot' 'SHA-256'
$missingCapture = New-PassedEvidence -Repo $repo -Executable $executablePath -Map $mapPath
$missingCapture.capture.mode = 'screenshot'
$missingCapture.capture.artifacts = @(
  [PSCustomObject]@{ available = $true; path = (Join-Path $results 'missing.png'); sha256 = $mapHash })
Assert-Throws { Assert-NativeSmokeEvidenceRecord -Evidence $missingCapture } `
  'manifest rejects nonexistent screenshot' 'file does not exist'

Assert-Throws { & (Join-Path $PSScriptRoot 'Test-NativeSmoke.ps1') } `
  'removed desktop runner directs callers to MCP' 'Use OpenRCT3 MCP'

Assert-NativeSmokeProcessExited -ProcessId ([int]::MaxValue)
Assert-Throws { Assert-NativeSmokeProcessExited -ProcessId $PID } `
  'live candidate fails cleanup validation' 'still running'

# Native application inspection is performed exclusively through OpenRCT3 MCP.

Write-Output 'Harness self-tests passed: native identity/logging, strict TRX, manifest, and driver guards.'
