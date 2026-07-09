<#
  Drives the OpenRCT3 WinForms/OpenGL desktop app: build, launch, inspect, screenshot,
  click/type, read logs, and close. No Electron/CLI/browser surface exists for this app,
  so interaction goes through Win32 (SendInput, CopyFromScreen, WM_CLOSE) instead.

  Run each action as its own invocation (PowerShell tool state does not persist between
  calls) - the target window is re-resolved every time via a PID file + title search.

  Examples:
    powershell -File AppDriver.ps1 -Action Build
    powershell -File AppDriver.ps1 -Action Launch
    powershell -File AppDriver.ps1 -Action Info
    powershell -File AppDriver.ps1 -Action Screenshot -OutFile D:\scratch\shot.png
    powershell -File AppDriver.ps1 -Action Click -X 400 -Y 300 -ClientCoords
    powershell -File AppDriver.ps1 -Action Key -Keys "{ESC}"
    powershell -File AppDriver.ps1 -Action Logs -Lines 80
    powershell -File AppDriver.ps1 -Action Close
#>
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('Build', 'Launch', 'Info', 'Screenshot', 'Click', 'DoubleClick', 'RightClick', 'MouseMove', 'Key', 'Text', 'Close', 'Logs')]
  [string]$Action,

  [string]$Configuration = 'Debug',
  [string]$WindowTitle = 'OpenRCT3',
  [string]$OutFile,
  [int]$X,
  [int]$Y,
  [switch]$ClientCoords,
  [string]$Keys,
  [string]$Text,
  [int]$TimeoutSec = 30,
  [switch]$Force,
  [int]$Lines = 60
)

$ErrorActionPreference = 'Stop'
$PidFile = Join-Path $env:TEMP 'openrct3-driver.pid'

# --- Locate the repo root (walk up from this script until OpenRCT3.sln is found) ---
function Find-RepoRoot {
  $dir = $PSScriptRoot
  while ($dir) {
    if (Test-Path (Join-Path $dir 'OpenRCT3.sln')) { return $dir }
    $parent = Split-Path $dir -Parent
    if ($parent -eq $dir) { break }
    $dir = $parent
  }
  throw 'Could not locate OpenRCT3.sln from script location.'
}

# --- Win32 interop ---
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace Native {
  public static class Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT {
      public int type;
      public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT {
      public int dx, dy, mouseData, dwFlags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);

    public static List<IntPtr> FindWindowsByTitle(string titlePart) {
      var results = new List<IntPtr>();
      EnumWindows((hWnd, lParam) => {
        if (!IsWindowVisible(hWnd)) return true;
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return true;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        if (sb.ToString().IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0)
          results.Add(hWnd);
        return true;
      }, IntPtr.Zero);
      return results;
    }

    // Plain SetForegroundWindow silently no-ops when called from a background process -
    // Windows' foreground-lock protection blocks it unless the calling thread's input queue
    // is attached to the current foreground thread. Without this, a caller that assumes the
    // switch succeeded will screenshot/click/type into whatever window actually has focus.
    public static bool ForceForeground(IntPtr hWnd) {
      IntPtr foreWnd = GetForegroundWindow();
      if (foreWnd == hWnd) return true;

      uint dummy;
      uint foreThread = foreWnd == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreWnd, out dummy);
      uint targetThread = GetWindowThreadProcessId(hWnd, out dummy);
      uint curThread = GetCurrentThreadId();

      bool attachedFore = foreThread != 0 && foreThread != curThread && AttachThreadInput(curThread, foreThread, true);
      bool attachedTarget = targetThread != 0 && targetThread != curThread && AttachThreadInput(curThread, targetThread, true);

      BringWindowToTop(hWnd);
      SetForegroundWindow(hWnd);

      if (attachedTarget) AttachThreadInput(curThread, targetThread, false);
      if (attachedFore) AttachThreadInput(curThread, foreThread, false);

      return GetForegroundWindow() == hWnd;
    }
  }
}
'@

# Per-Monitor-V2 DPI awareness so screen coordinates match physical pixels (the game's
# manifest doesn't declare DPI awareness, so mismatches only bite if Windows is scaling).
[Native.Win32]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

function Get-TargetWindow {
  $hWnd = [IntPtr]::Zero
  if (Test-Path $PidFile) {
    $procId = Get-Content $PidFile -ErrorAction SilentlyContinue
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($proc) {
      $proc.Refresh()
      if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $hWnd = $proc.MainWindowHandle }
    }
  }
  if ($hWnd -eq [IntPtr]::Zero) {
    $matches = [Native.Win32]::FindWindowsByTitle($WindowTitle)
    if ($matches.Count -gt 0) { $hWnd = $matches[0] }
  }
  if ($hWnd -eq [IntPtr]::Zero) {
    throw "No visible window matching title '$WindowTitle' found. Is the app running? (Action: Launch)"
  }
  return $hWnd
}

function Get-WindowRect([IntPtr]$hWnd) {
  $rect = New-Object Native.Win32+RECT
  [Native.Win32]::GetWindowRect($hWnd, [ref]$rect) | Out-Null
  return $rect
}

# Restores + raises the target window before any screenshot or input action. This steals
# focus from whatever the user is currently looking at, and Click/MouseMove additionally
# warp the real system cursor - SKILL.md requires telling the user before triggering that.
#
# Verifies the switch actually landed (via ForceForeground's AttachThreadInput dance) and
# throws rather than continuing silently: a caller that assumes success on a no-op would
# screenshot, click, or type into whatever window actually has focus instead - e.g. capturing
# or clicking into an unrelated app on the user's desktop.
function Set-Foreground([IntPtr]$hWnd) {
  [Native.Win32]::ShowWindow($hWnd, 9) | Out-Null # SW_RESTORE, no-op if already normal
  $ok = $false
  for ($attempt = 0; $attempt -lt 3 -and -not $ok; $attempt++) {
    if ($attempt -gt 0) { Start-Sleep -Milliseconds 150 }
    $ok = [Native.Win32]::ForceForeground($hWnd)
  }
  if (-not $ok) {
    throw "Could not bring window $hWnd to the foreground (Windows blocked the focus switch). " +
      "Refusing to screenshot/click/type, since that would act on whatever window is actually " +
      "focused instead. Ask the user to click the OpenRCT3 window once, then retry."
  }
  Start-Sleep -Milliseconds 150
}

function Send-Click([IntPtr]$hWnd, [int]$px, [int]$py, [bool]$client, [string]$button, [bool]$double) {
  Set-Foreground $hWnd

  $screenX = $px
  $screenY = $py
  if ($client) {
    $pt = New-Object Native.Win32+POINT
    $pt.X = $px; $pt.Y = $py
    [Native.Win32]::ClientToScreen($hWnd, [ref]$pt) | Out-Null
    $screenX = $pt.X; $screenY = $pt.Y
  }
  [Native.Win32]::SetCursorPos($screenX, $screenY) | Out-Null
  Start-Sleep -Milliseconds 50

  $downFlag = if ($button -eq 'Right') { 0x0008 } else { 0x0002 } # MOUSEEVENTF_RIGHTDOWN / LEFTDOWN
  $upFlag = if ($button -eq 'Right') { 0x0010 } else { 0x0004 }   # MOUSEEVENTF_RIGHTUP / LEFTUP

  $clicks = if ($double) { 2 } else { 1 }
  for ($i = 0; $i -lt $clicks; $i++) {
    $down = New-Object Native.Win32+INPUT
    $down.type = 0
    $down.mi = New-Object Native.Win32+MOUSEINPUT
    $down.mi.dwFlags = $downFlag
    $up = New-Object Native.Win32+INPUT
    $up.type = 0
    $up.mi = New-Object Native.Win32+MOUSEINPUT
    $up.mi.dwFlags = $upFlag
    [Native.Win32]::SendInput(1, @($down), [Runtime.InteropServices.Marshal]::SizeOf($down)) | Out-Null
    Start-Sleep -Milliseconds 40
    [Native.Win32]::SendInput(1, @($up), [Runtime.InteropServices.Marshal]::SizeOf($up)) | Out-Null
    Start-Sleep -Milliseconds 60
  }
  return [PSCustomObject]@{ ScreenX = $screenX; ScreenY = $screenY }
}

switch ($Action) {

  'Build' {
    $repo = Find-RepoRoot
    $proj = Join-Path $repo 'OpenRCT3\OpenRCT3.csproj'
    & dotnet build $proj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
  }

  'Launch' {
    $repo = Find-RepoRoot
    $binRoot = Join-Path $repo "OpenRCT3\bin\$Configuration"
    $exe = Get-ChildItem -Path $binRoot -Filter 'OpenRCT3.exe' -Recurse -ErrorAction SilentlyContinue |
      Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $exe) { throw "OpenRCT3.exe not found under $binRoot - run Action Build first." }

    # Must run with CWD = the exe's own folder: Program.windows.cs loads "nlog.config" via a
    # path relative to the working directory, not the assembly location.
    $proc = Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName -PassThru
    Set-Content -Path $PidFile -Value $proc.Id

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
      Start-Sleep -Milliseconds 250
      $proc.Refresh()
    } while ($proc.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)

    if ($proc.MainWindowHandle -eq [IntPtr]::Zero) {
      throw "Window did not appear within $TimeoutSec s (process id $($proc.Id) still running: $(-not $proc.HasExited))"
    }
    $rect = Get-WindowRect $proc.MainWindowHandle
    [PSCustomObject]@{
      ProcessId = $proc.Id
      WindowHandle = $proc.MainWindowHandle
      Title = $proc.MainWindowTitle
      ScreenRect = "$($rect.Left),$($rect.Top) - $($rect.Right),$($rect.Bottom)"
    } | Format-List
  }

  'Info' {
    $hWnd = Get-TargetWindow
    [uint32]$procId = 0
    [Native.Win32]::GetWindowThreadProcessId($hWnd, [ref]$procId) | Out-Null
    $rect = Get-WindowRect $hWnd
    $client = New-Object Native.Win32+RECT
    [Native.Win32]::GetClientRect($hWnd, [ref]$client) | Out-Null
    [PSCustomObject]@{
      WindowHandle = $hWnd
      ProcessId = $procId
      IsMinimized = [Native.Win32]::IsIconic($hWnd)
      IsForeground = ([Native.Win32]::GetForegroundWindow() -eq $hWnd)
      ScreenRect = "$($rect.Left),$($rect.Top) - $($rect.Right),$($rect.Bottom)"
      ClientSize = "$($client.Right - $client.Left) x $($client.Bottom - $client.Top)"
    } | Format-List
  }

  'Screenshot' {
    Add-Type -AssemblyName System.Drawing
    $hWnd = Get-TargetWindow
    Set-Foreground $hWnd
    Start-Sleep -Milliseconds 150 # let the window repaint after being raised

    $rect = Get-WindowRect $hWnd
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { throw "Window has invalid bounds ($w x $h) - is it minimized off-screen?" }

    if (-not $OutFile) {
      $OutFile = Join-Path $env:TEMP "openrct3-$(Get-Date -Format 'yyyyMMdd-HHmmss').png"
    }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    # CopyFromScreen (not PrintWindow): the game renders via raw WGL SwapBuffers, which
    # PrintWindow's DC-replay does not reliably capture. This requires the window to be
    # actually on-screen and unoccluded, which SetForegroundWindow above ensures.
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    Write-Output $OutFile
  }

  'Click' { $hWnd = Get-TargetWindow; Send-Click $hWnd $X $Y $ClientCoords.IsPresent 'Left' $false | Format-List }
  'DoubleClick' { $hWnd = Get-TargetWindow; Send-Click $hWnd $X $Y $ClientCoords.IsPresent 'Left' $true | Format-List }
  'RightClick' { $hWnd = Get-TargetWindow; Send-Click $hWnd $X $Y $ClientCoords.IsPresent 'Right' $false | Format-List }

  'MouseMove' {
    $hWnd = Get-TargetWindow
    Set-Foreground $hWnd
    $screenX = $X; $screenY = $Y
    if ($ClientCoords) {
      $pt = New-Object Native.Win32+POINT
      $pt.X = $X; $pt.Y = $Y
      [Native.Win32]::ClientToScreen($hWnd, [ref]$pt) | Out-Null
      $screenX = $pt.X; $screenY = $pt.Y
    }
    [Native.Win32]::SetCursorPos($screenX, $screenY) | Out-Null
  }

  'Key' {
    if (-not $Keys) { throw 'Key action requires -Keys, e.g. "{ESC}" or "^s" (see SendKeys syntax).' }
    Add-Type -AssemblyName System.Windows.Forms
    $hWnd = Get-TargetWindow
    Set-Foreground $hWnd
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
  }

  'Text' {
    if ($null -eq $Text) { throw 'Text action requires -Text "literal string to type".' }
    Add-Type -AssemblyName System.Windows.Forms
    $hWnd = Get-TargetWindow
    Set-Foreground $hWnd
    # Escape SendKeys-special characters so the string is typed literally.
    $escaped = $Text -replace '([+^%~(){}\[\]])', '{$1}'
    [System.Windows.Forms.SendKeys]::SendWait($escaped)
  }

  'Close' {
    $hWnd = Get-TargetWindow
    [uint32]$procId = 0
    [Native.Win32]::GetWindowThreadProcessId($hWnd, [ref]$procId) | Out-Null
    if ($Force) {
      Stop-Process -Id $procId -Force
    } else {
      # WM_CLOSE lets GameWindow_FormClosing run Game.Quit() cleanly instead of killing the process.
      [Native.Win32]::PostMessage($hWnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
      $deadline = (Get-Date).AddSeconds($TimeoutSec)
      $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
      while ($proc -and -not $proc.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $proc.Refresh()
      }
      if ($proc -and -not $proc.HasExited) {
        Write-Warning "Process $procId did not exit within $TimeoutSec s after WM_CLOSE; re-run with -Force to kill it."
      }
    }
    Remove-Item $PidFile -ErrorAction SilentlyContinue
  }

  'Logs' {
    $logPath = Join-Path $env:APPDATA 'OpenRCT3\logs\app.log'
    if (-not (Test-Path $logPath)) { throw "No log file at $logPath" }
    Get-Content $logPath -Tail $Lines
  }
}
