param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$name = 'DeskFrame.exe'
$entry = '/main:DeskFrame.Program'
$framework = Split-Path $compiler
$extra = @("/reference:$framework\WPF\UIAutomationProvider.dll", "/reference:$framework\WPF\UIAutomationTypes.dll", "/reference:$framework\WPF\WindowsBase.dll")
$debugOptions = @('/optimize+', '/debug-')
if ($Tests) {
 $sources += Join-Path $PSScriptRoot 'tests\Integration.cs'; $name = 'DeskFrame.Tests.exe'; $entry = '/main:DeskFrame.Integration'
 $framework = Split-Path $compiler
 $extra += "/reference:$framework\WPF\UIAutomationClient.dll"
 $debugOptions = @('/optimize-', '/debug:full')
}
& $compiler /nologo /target:winexe /platform:anycpu $debugOptions /warn:4 $entry "/out:$out\$name" "/win32manifest:$PSScriptRoot\src\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $extra $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\DeskFrame.exe.config') -Destination (Join-Path $out "$name.config") -Force
Write-Output "Built $name"
