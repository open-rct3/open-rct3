[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$RepoPath,
  [Parameter(Mandatory = $true)][string]$NlogConfigPath,
  [Parameter(Mandatory = $true)][string]$InstallPath,
  [Parameter(Mandatory = $true)][string]$ConfiguredMapPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$probeDirectory = Join-Path $RepoPath 'TestResults\loaded-map-probe-runtime'
$projectPath = Join-Path $probeDirectory 'LoadedMapProbe.csproj'
$programPath = Join-Path $probeDirectory 'Program.cs'
New-Item -ItemType Directory -Path $probeDirectory -Force | Out-Null
$openRct3Project = Join-Path $RepoPath 'OpenRCT3\OpenRCT3.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows10.0.17763.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$openRct3Project" />
    <Compile Include="$RepoPath\OpenRCT3.Tests\Serialization\DatTerrainFixture.cs" Link="DatTerrainFixture.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding UTF8
@'
using NLog;
using NLog.Config;
using OpenRCT3.Platforms;
using OpenRCT3.Simulation;
using OpenRCT3.Tests.Serialization;
using System.Reflection;

LogManager.Configuration = new XmlLoggingConfiguration(args[0]);
File.WriteAllBytes(args[2], DatTerrainFixture.BuildMinimalTerrainBytes());
var config = new AppConfig { InstallPath = args[1], MapPath = args[2] };
typeof(AppConfig).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)!
  .SetValue(null, config);
try {
  Terrain.Load();
} catch {
  // The isolated probe intentionally has no proprietary terrain OVL. The loaded-map marker is
  // emitted immediately after DatTerrainReader.Read and before that later texture lookup fails.
} finally {
  LogManager.Flush();
  LogManager.Shutdown();
}
'@ | Set-Content -LiteralPath $programPath -Encoding UTF8

& dotnet run --project $projectPath -p:SolutionDir="$($RepoPath.Replace('\', '/'))/" -- `
  $NlogConfigPath $InstallPath $ConfiguredMapPath
if ($LASTEXITCODE -ne 0) { throw "Loaded-map probe host failed with exit code $LASTEXITCODE." }
