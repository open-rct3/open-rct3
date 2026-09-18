function Get-NormalizedSmokePath {
  param([Parameter(Mandatory = $true)][string]$Path)

  return [System.IO.Path]::GetFullPath($Path).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
}

function Get-NativeSmokeLogState {
  param(
    [Parameter(Mandatory = $true)]
    [string]$LogPath,
    [Parameter(Mandatory = $true)]
    [string]$RunId,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedMapPath,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedMapSha256
  )

  $prefix = "run=$RunId|"
  $lines = if (Test-Path -LiteralPath $LogPath -PathType Leaf) {
    @(Get-Content -LiteralPath $LogPath | Where-Object { $_.StartsWith($prefix) })
  } else {
    @()
  }
  $loadedMapLines = @($lines | Where-Object {
    $_ -match '\|INFO\|OpenRCT3\.Simulation\.Terrain\|Native smoke loaded map '
  })
  $loadedMapPath = $null
  $loadedMapSha256 = $null
  $loadedMapParseError = $null
  if ($loadedMapLines.Count -eq 1) {
    if ($loadedMapLines[0] -match 'Native smoke loaded map (?<identity>\{.+\})\s*$') {
      try {
        $identity = $Matches.identity | ConvertFrom-Json
        $loadedMapPath = [string]$identity.path
        $loadedMapSha256 = [string]$identity.sha256
      } catch {
        $loadedMapParseError = $_.Exception.Message
      }
    } else {
      $loadedMapParseError = 'The loaded-map marker does not contain a JSON identity.'
    }
  }

  $mapPathMatches = $false
  if (-not [string]::IsNullOrWhiteSpace($loadedMapPath)) {
    $mapPathMatches = (Get-NormalizedSmokePath $loadedMapPath).Equals(
      (Get-NormalizedSmokePath $ExpectedMapPath),
      [StringComparison]::OrdinalIgnoreCase)
  }

  return [PSCustomObject]@{
    Prefix = $prefix
    Lines = $lines
    HasStartup = @($lines | Where-Object {
      $_ -match '\|INFO\|OpenRCT3\.Program\|Starting OpenRCT3 on Windows'
    }).Count -gt 0
    HasWorldLoaded = @($lines | Where-Object {
      $_ -match '\|DEBUG\|OpenRCT3\.Game\|Game world loaded'
    }).Count -gt 0
    HasTerrainMesh = @($lines | Where-Object {
      $_ -match '\|DEBUG\|OpenRCT3\.Game\|Added terrain mesh'
    }).Count -gt 0
    HasInitialFrame = @($lines | Where-Object {
      $_ -match '\|DEBUG\|OpenRCT3\.Platforms\.Windows\.GameWindow\|Presented initial scene frame'
    }).Count -gt 0
    HasFailure = @($lines | Where-Object { $_ -match '\|(ERROR|FATAL)\|' }).Count -gt 0
    LoadedMapCount = $loadedMapLines.Count
    LoadedMapPath = $loadedMapPath
    LoadedMapSha256 = $loadedMapSha256
    LoadedMapParseError = $loadedMapParseError
    LoadedMapPathMatches = $mapPathMatches
    LoadedMapHashMatches = $loadedMapSha256 -eq $ExpectedMapSha256
  }
}

function Assert-NativeSmokeCompletion {
  param(
    [Parameter(Mandatory = $true)]
    [PSCustomObject]$State
  )

  if ($State.HasFailure) {
    throw 'The run-correlated native log contains an ERROR or FATAL event.'
  }

  $missing = @()
  if (-not $State.HasStartup) { $missing += 'Windows startup' }
  if (-not $State.HasWorldLoaded) { $missing += 'Game world loaded' }
  if (-not $State.HasTerrainMesh) { $missing += 'Added terrain mesh' }
  if (-not $State.HasInitialFrame) { $missing += 'Presented initial scene frame' }
  if ($State.LoadedMapCount -ne 1) { $missing += 'exactly one application loaded-map identity' }
  if ($missing.Count -gt 0) {
    throw "Native smoke did not reach required completion markers: $($missing -join ', ')."
  }
  if (-not [string]::IsNullOrWhiteSpace($State.LoadedMapParseError)) {
    throw "Native smoke loaded-map marker is invalid: $($State.LoadedMapParseError)"
  }
  if (-not $State.LoadedMapPathMatches) {
    throw "Application loaded a different map path: '$($State.LoadedMapPath)'."
  }
  if (-not $State.LoadedMapHashMatches) {
    throw "Application loaded a different map hash: '$($State.LoadedMapSha256)'."
  }
}

function Assert-NativeSmokeFileLoggingConfiguration {
  param([Parameter(Mandatory = $true)][string]$ConfigPath)

  [xml]$config = Get-Content -Raw -LiteralPath $ConfigPath
  $rules = @($config.SelectNodes("//*[local-name()='rules']/*[local-name()='logger']"))
  $levelOrder = @{
    Trace = 0
    Debug = 1
    Info = 2
    Warn = 3
    Error = 4
    Fatal = 5
  }
  $fileRule = $rules | Where-Object {
    @([string]$_.GetAttribute('writeTo') -split ',' | ForEach-Object { $_.Trim() }) -contains 'file'
  } | Where-Object {
    $minimumValue = $_.GetAttribute('minlevel')
    $maximumValue = $_.GetAttribute('maxlevel')
    $minimum = if ([string]::IsNullOrWhiteSpace($minimumValue)) { 'Trace' } else { $minimumValue }
    $maximum = if ([string]::IsNullOrWhiteSpace($maximumValue)) { 'Fatal' } else { $maximumValue }
    $levelOrder[$minimum] -le $levelOrder.Debug -and $levelOrder[$maximum] -ge $levelOrder.Info
  } | Select-Object -First 1

  if ($null -eq $fileRule) {
    throw 'nlog.config does not route the required Debug and Info smoke markers to the file target.'
  }
}

function Assert-NativeSmokePathWithinRoot {
  param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Name
  )

  if (-not [System.IO.Path]::IsPathRooted($Path)) {
    throw "$Name must be an absolute path."
  }
  if (-not [System.IO.Path]::IsPathRooted($Root)) {
    throw 'Native smoke application-data root must be an absolute path.'
  }

  $normalizedPath = Get-NormalizedSmokePath $Path
  $normalizedRoot = Get-NormalizedSmokePath $Root
  $rootPrefix = $normalizedRoot + [System.IO.Path]::DirectorySeparatorChar
  if (-not $normalizedPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "$Name must stay inside the isolated native smoke application-data root."
  }
}

function Assert-NativeSmokeApplicationDataRoot {
  param([Parameter(Mandatory = $true)][string]$ApplicationDataPath)

  if (-not [System.IO.Path]::IsPathRooted($ApplicationDataPath)) {
    throw 'Native smoke application-data root must be an absolute path.'
  }

  $knownApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::ApplicationData)
  if (-not [string]::IsNullOrWhiteSpace($knownApplicationData) -and
      (Get-NormalizedSmokePath $ApplicationDataPath).Equals(
        (Get-NormalizedSmokePath $knownApplicationData),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Native smoke application-data root must not be the user profile application-data path.'
  }
}

function Assert-NativeSmokeIsolatedLoggingConfiguration {
  param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [Parameter(Mandatory = $true)][string]$ApplicationDataPath,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [Parameter(Mandatory = $true)][string]$ArchiveLogPath
  )

  Assert-NativeSmokeApplicationDataRoot -ApplicationDataPath $ApplicationDataPath
  Assert-NativeSmokePathWithinRoot `
    -Path $LogPath -Root $ApplicationDataPath -Name 'Native smoke application log'
  Assert-NativeSmokePathWithinRoot `
    -Path $ArchiveLogPath -Root $ApplicationDataPath -Name 'Native smoke archive log'

  [xml]$config = Get-Content -Raw -LiteralPath $ConfigPath
  $fileTarget = $config.SelectSingleNode("//*[local-name()='target' and @name='file']")
  if ($null -eq $fileTarget) { throw 'Native nlog.config has no file target.' }

  $configuredLogPath = [string]$fileTarget.GetAttribute('fileName')
  $configuredArchivePath = [string]$fileTarget.GetAttribute('archiveFileName')
  if (-not (Get-NormalizedSmokePath $configuredLogPath).Equals(
      (Get-NormalizedSmokePath $LogPath),
      [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Native nlog.config file target is not bound to the isolated application log.'
  }
  if (-not (Get-NormalizedSmokePath $configuredArchivePath).Equals(
      (Get-NormalizedSmokePath $ArchiveLogPath),
      [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Native nlog.config archive target is not bound to the isolated archive log.'
  }

  $expectedLayout =
    'run=${environment:variable=OPENRCT3_SMOKE_RUN_ID}|${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}'
  if ([string]$fileTarget.GetAttribute('layout') -ne $expectedLayout) {
    throw 'Native nlog.config file target is not bound to the smoke run nonce.'
  }
}

function Set-NativeSmokeIsolatedLoggingConfiguration {
  param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [Parameter(Mandatory = $true)][string]$ApplicationDataPath,
    [Parameter(Mandatory = $true)][string]$LogPath,
    [Parameter(Mandatory = $true)][string]$ArchiveLogPath
  )

  Assert-NativeSmokeApplicationDataRoot -ApplicationDataPath $ApplicationDataPath
  Assert-NativeSmokeFileLoggingConfiguration -ConfigPath $ConfigPath
  Assert-NativeSmokePathWithinRoot `
    -Path $LogPath -Root $ApplicationDataPath -Name 'Native smoke application log'
  Assert-NativeSmokePathWithinRoot `
    -Path $ArchiveLogPath -Root $ApplicationDataPath -Name 'Native smoke archive log'

  [xml]$config = Get-Content -Raw -LiteralPath $ConfigPath
  $fileTarget = $config.SelectSingleNode("//*[local-name()='target' and @name='file']")
  if ($null -eq $fileTarget) { throw 'Native nlog.config has no file target.' }
  $fileTarget.SetAttribute('fileName', (Get-NormalizedSmokePath $LogPath))
  $fileTarget.SetAttribute('archiveFileName', (Get-NormalizedSmokePath $ArchiveLogPath))
  $fileTarget.SetAttribute(
    'layout',
    'run=${environment:variable=OPENRCT3_SMOKE_RUN_ID}|${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}')
  $config.Save($ConfigPath)

  Assert-NativeSmokeIsolatedLoggingConfiguration `
    -ConfigPath $ConfigPath `
    -ApplicationDataPath $ApplicationDataPath `
    -LogPath $LogPath `
    -ArchiveLogPath $ArchiveLogPath
}

function Assert-NativeSmokeScreenshotContent {
  param([Parameter(Mandatory = $true)][string]$Path)

  Add-Type -AssemblyName System.Drawing
  $bitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Path).Path)
  try {
    if ($bitmap.Width -lt 100 -or $bitmap.Height -lt 100) {
      throw 'Native smoke screenshot is too small to prove the rendered game window.'
    }

    $left = [int]($bitmap.Width * 0.1)
    $right = [int]($bitmap.Width * 0.9)
    $top = [int]($bitmap.Height * 0.12)
    $bottom = [int]($bitmap.Height * 0.9)
    $stepX = [Math]::Max(1, [int](($right - $left) / 64))
    $stepY = [Math]::Max(1, [int](($bottom - $top) / 64))
    $colors = @{}
    foreach ($y in $top..($bottom - 1) | Where-Object { ($_ - $top) % $stepY -eq 0 }) {
      foreach ($x in $left..($right - 1) | Where-Object { ($_ - $left) % $stepX -eq 0 }) {
        $color = $bitmap.GetPixel($x, $y).ToArgb()
        $colors[$color] = 1 + [int]$colors[$color]
      }
    }

    if ($colors.Count -lt 2) {
      throw 'Native smoke screenshot has a uniform client area; no rendered scene is visible.'
    }
  } finally {
    $bitmap.Dispose()
  }
}

function Assert-NativeSmokeConfig {
  param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath,
    [Parameter(Mandatory = $true)]
    [string]$InstallPath,
    [Parameter(Mandatory = $true)]
    [string]$MapPath
  )

  $config = Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json
  $expectedInstall = Get-NormalizedSmokePath $InstallPath
  $expectedMap = Get-NormalizedSmokePath $MapPath
  $actualInstall = Get-NormalizedSmokePath ([string]$config.InstallPath)
  $actualMap = Get-NormalizedSmokePath ([string]$config.MapPath)

  if (-not $actualInstall.Equals($expectedInstall, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Native smoke config InstallPath mismatch: expected '$expectedInstall', got '$actualInstall'."
  }
  if (-not $actualMap.Equals($expectedMap, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Native smoke config MapPath mismatch: expected '$expectedMap', got '$actualMap'."
  }
  if ($config.SuppressCrashAlerts -ne $true) {
    throw 'Native smoke config must suppress modal crash alerts.'
  }
}

function Read-NativeDriverPidMetadata {
  param([Parameter(Mandatory = $true)][string]$PidFile)

  if (-not (Test-Path -LiteralPath $PidFile -PathType Leaf)) {
    throw "Native driver PID metadata is missing: $PidFile"
  }
  $metadata = Get-Content -Raw -LiteralPath $PidFile | ConvertFrom-Json
  if ([int]$metadata.ProcessId -le 0) { throw 'Native driver PID metadata has no valid ProcessId.' }
  if ([string]::IsNullOrWhiteSpace([string]$metadata.ExecutablePath) -or
      -not [System.IO.Path]::IsPathRooted([string]$metadata.ExecutablePath)) {
    throw 'Native driver PID metadata has no absolute ExecutablePath.'
  }
  $parsedStartTime = [DateTime]::MinValue
  if (-not [DateTime]::TryParse(
      [string]$metadata.StartTimeUtc,
      [Globalization.CultureInfo]::InvariantCulture,
      [Globalization.DateTimeStyles]::RoundtripKind,
      [ref]$parsedStartTime)) {
    throw 'Native driver PID metadata has no valid StartTimeUtc.'
  }
  return $metadata
}

function Assert-NativeSmokeProcessExited {
  param([Parameter(Mandatory = $true)][int]$ProcessId)

  if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    throw "Native smoke candidate process $ProcessId is still running after cleanup."
  }
}
