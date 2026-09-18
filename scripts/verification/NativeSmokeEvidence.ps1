function New-NativeSmokeEvidenceRecord {
  param(
    [Parameter(Mandatory = $true)][string]$RepoPath,
    [Parameter(Mandatory = $true)][string]$RunNonce
  )

  $unavailable = {
    param([string]$Reason)
    return [PSCustomObject]@{ available = $false; reason = $Reason }
  }
  return [PSCustomObject]@{
    schemaVersion = 2
    check = 'native-map-smoke'
    runNonce = $RunNonce
    outcome = 'failed'
    reason = $null
    repoPath = [System.IO.Path]::GetFullPath($RepoPath)
    executable = & $unavailable 'native executable not built'
    requestedMap = & $unavailable 'requested map not resolved'
    loadedMap = & $unavailable 'application loaded-map marker not observed'
    process = & $unavailable 'candidate process not launched'
    window = [PSCustomObject]@{
      requestedDpi = & $unavailable 'the smoke does not request a target DPI'
      observedDpi = & $unavailable 'target window not observed'
      requestedClientSize = & $unavailable 'the smoke does not request a client size'
      observedClientSize = & $unavailable 'target window not observed'
    }
    capture = [PSCustomObject]@{ mode = 'none'; artifacts = @() }
    cleanup = [PSCustomObject]@{
      attempted = $false
      closeResult = 'not-started'
      processExited = $null
    }
    artifacts = [PSCustomObject]@{
      transcript = & $unavailable 'verification transcript not written'
      applicationLog = & $unavailable 'application log not written'
    }
  }
}

function Assert-Sha256Value {
  param(
    [Parameter(Mandatory = $true)][string]$Value,
    [Parameter(Mandatory = $true)][string]$Name
  )

  if ($Value -notmatch '^[0-9A-Fa-f]{64}$') { throw "$Name is not a SHA-256 value." }
}

function Assert-AvailableFileIdentity {
  param(
    [Parameter(Mandatory = $true)][PSCustomObject]$Identity,
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$HashProperty = 'sha256'
  )

  if ($Identity.available -ne $true) { throw "$Name is unavailable." }
  if ([string]::IsNullOrWhiteSpace([string]$Identity.path) -or
      -not [System.IO.Path]::IsPathRooted([string]$Identity.path)) {
    throw "$Name path is not absolute."
  }
  if (-not (Test-Path -LiteralPath $Identity.path -PathType Leaf)) {
    throw "$Name file does not exist: $($Identity.path)"
  }
  $claimedHash = [string]$Identity.$HashProperty
  Assert-Sha256Value -Value $claimedHash -Name "$Name $HashProperty"
  $actualHash = (Get-FileHash -LiteralPath $Identity.path -Algorithm SHA256).Hash
  if ($actualHash -ne $claimedHash) {
    throw "$Name $HashProperty does not match the file content."
  }
}

function Assert-OptionalFileIdentity {
  param(
    [Parameter(Mandatory = $true)][PSCustomObject]$Identity,
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$HashProperty = 'sha256'
  )

  if ($Identity.available -eq $true) {
    Assert-AvailableFileIdentity -Identity $Identity -Name $Name -HashProperty $HashProperty
    return
  }
  if ($Identity.available -ne $false -or
      [string]::IsNullOrWhiteSpace([string]$Identity.reason)) {
    throw "$Name must be available or include an unavailable reason."
  }
}

function Assert-NativeSmokeEvidenceRecord {
  param([Parameter(Mandatory = $true)][PSCustomObject]$Evidence)

  if ($Evidence.schemaVersion -ne 2) { throw 'Native evidence schemaVersion must be 2.' }
  if ($Evidence.check -ne 'native-map-smoke') { throw 'Native evidence check name is invalid.' }
  if ([string]$Evidence.runNonce -notmatch '^[0-9a-f]{32}$') {
    throw 'Native evidence runNonce must be a lowercase 128-bit hex nonce.'
  }
  if (@('passed', 'failed', 'skipped') -notcontains $Evidence.outcome) {
    throw "Native evidence outcome '$($Evidence.outcome)' is invalid."
  }
  if (-not [System.IO.Path]::IsPathRooted([string]$Evidence.repoPath) -or
      -not (Test-Path -LiteralPath $Evidence.repoPath -PathType Container)) {
    throw 'Native evidence repoPath must identify an existing directory.'
  }

  Assert-OptionalFileIdentity -Identity $Evidence.executable -Name 'Executable identity' `
    -HashProperty 'preLaunchSha256'
  if ($Evidence.executable.available -eq $true) {
    if ($Evidence.executable.postLaunchSha256.available -eq $true) {
      Assert-Sha256Value -Value ([string]$Evidence.executable.postLaunchSha256.value) `
        -Name 'Executable identity postLaunchSha256'
      $actualExecutableHash = (Get-FileHash -LiteralPath $Evidence.executable.path `
        -Algorithm SHA256).Hash
      if ($actualExecutableHash -ne $Evidence.executable.postLaunchSha256.value -or
          $Evidence.executable.preLaunchSha256 -ne $Evidence.executable.postLaunchSha256.value) {
        throw 'Executable identity changed between pre-launch and post-launch hashing.'
      }
    } elseif ($Evidence.executable.postLaunchSha256.available -ne $false -or
        [string]::IsNullOrWhiteSpace([string]$Evidence.executable.postLaunchSha256.reason)) {
      throw 'Executable post-launch hash must be available or include an unavailable reason.'
    }
  }
  Assert-OptionalFileIdentity -Identity $Evidence.requestedMap -Name 'Requested map identity'
  Assert-OptionalFileIdentity -Identity $Evidence.loadedMap -Name 'Loaded map identity'
  Assert-OptionalFileIdentity -Identity $Evidence.artifacts.transcript -Name 'Verification transcript'
  Assert-OptionalFileIdentity -Identity $Evidence.artifacts.applicationLog -Name 'Application log'
  if ($Evidence.artifacts.transcript.available -eq $true) {
    $transcript = Get-Content -Raw -LiteralPath $Evidence.artifacts.transcript.path
    if ($transcript -notmatch "(?m)^run-id=$([regex]::Escape($Evidence.runNonce))\r?$") {
      throw 'Verification transcript is not bound to the evidence runNonce.'
    }
  }
  if ($Evidence.artifacts.applicationLog.available -eq $true) {
    $applicationLog = Get-Content -Raw -LiteralPath $Evidence.artifacts.applicationLog.path
    if ($applicationLog -notmatch "(?m)^run=$([regex]::Escape($Evidence.runNonce))\|") {
      throw 'Application log is not bound to the evidence runNonce.'
    }
  }

  if (@('none', 'screenshot') -notcontains $Evidence.capture.mode) {
    throw "Native evidence capture mode '$($Evidence.capture.mode)' is invalid."
  }
  if ($Evidence.capture.mode -eq 'none' -and @($Evidence.capture.artifacts).Count -ne 0) {
    throw 'Native evidence cannot list capture artifacts when capture mode is none.'
  }
  if ($Evidence.capture.mode -eq 'screenshot') {
    if (@($Evidence.capture.artifacts).Count -eq 0) {
      throw 'Screenshot capture mode requires at least one artifact.'
    }
    foreach ($artifact in @($Evidence.capture.artifacts)) {
      Assert-AvailableFileIdentity -Identity $artifact -Name 'Screenshot artifact'
    }
  }

  foreach ($optionalField in @(
      $Evidence.window.requestedDpi,
      $Evidence.window.requestedClientSize)) {
    if ($optionalField.available -eq $false -and
        [string]::IsNullOrWhiteSpace([string]$optionalField.reason)) {
      throw 'Unavailable optional native evidence fields require a reason.'
    }
  }

  if ($Evidence.outcome -ne 'passed') { return }

  Assert-AvailableFileIdentity -Identity $Evidence.executable -Name 'Executable identity' `
    -HashProperty 'preLaunchSha256'
  if ($Evidence.executable.postLaunchSha256.available -ne $true) {
    throw 'Passed native evidence requires a post-launch executable hash.'
  }
  Assert-AvailableFileIdentity -Identity $Evidence.requestedMap -Name 'Requested map identity'
  Assert-AvailableFileIdentity -Identity $Evidence.loadedMap -Name 'Loaded map identity'
  Assert-AvailableFileIdentity -Identity $Evidence.artifacts.transcript -Name 'Verification transcript'
  Assert-AvailableFileIdentity -Identity $Evidence.artifacts.applicationLog -Name 'Application log'
  if (-not (Get-NormalizedSmokePath $Evidence.requestedMap.path).Equals(
      (Get-NormalizedSmokePath $Evidence.loadedMap.path),
      [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Passed native evidence requested and loaded map paths do not match.'
  }
  if ($Evidence.requestedMap.sha256 -ne $Evidence.loadedMap.sha256) {
    throw 'Passed native evidence requested and loaded map hashes do not match.'
  }

  if ($Evidence.process.available -ne $true) { throw 'Passed native evidence requires process identity.' }
  if ([int]$Evidence.process.pid -le 0 -or [long]$Evidence.process.hwnd -le 0) {
    throw 'Passed native evidence requires positive PID and HWND values.'
  }
  $startTime = [DateTime]::MinValue
  if (-not [DateTime]::TryParse(
      [string]$Evidence.process.startTimeUtc,
      [Globalization.CultureInfo]::InvariantCulture,
      [Globalization.DateTimeStyles]::RoundtripKind,
      [ref]$startTime)) {
    throw 'Passed native evidence process startTimeUtc is invalid.'
  }
  if (-not (Get-NormalizedSmokePath $Evidence.process.executablePath).Equals(
      (Get-NormalizedSmokePath $Evidence.executable.path),
      [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Passed native evidence process executable binding does not match the binary identity.'
  }

  if ($Evidence.window.observedDpi.available -ne $true -or
      [int]$Evidence.window.observedDpi.value -le 0) {
    throw 'Passed native evidence requires an observed window DPI.'
  }
  if ($Evidence.window.observedClientSize.available -ne $true -or
      [int]$Evidence.window.observedClientSize.width -le 0 -or
      [int]$Evidence.window.observedClientSize.height -le 0) {
    throw 'Passed native evidence requires a positive observed client size.'
  }
  if ($Evidence.cleanup.attempted -ne $true -or $Evidence.cleanup.processExited -ne $true -or
      @('graceful', 'already-exited') -notcontains $Evidence.cleanup.closeResult) {
    throw 'Passed native evidence requires successful candidate cleanup.'
  }
}

function Write-NativeSmokeEvidenceManifest {
  param(
    [Parameter(Mandatory = $true)][PSCustomObject]$Evidence,
    [Parameter(Mandatory = $true)][string]$Path
  )

  Assert-NativeSmokeEvidenceRecord -Evidence $Evidence
  $Evidence | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
}
