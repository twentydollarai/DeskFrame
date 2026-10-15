$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' | ForEach-Object FullName)
$sources+=Join-Path $PSScriptRoot 'Rendering.cs'
New-Item -ItemType Directory -Path (Join-Path $project 'build') -Force | Out-Null
$exe=Join-Path $project 'build\DeskFrame.RenderingTests.exe'
& $compiler /nologo /target:winexe /main:DeskFrame.RenderingTests /optimize+ "/out:$exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationProvider.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" $sources
if($LASTEXITCODE -ne 0) {throw 'Rendering test compilation failed'}
$p=Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
if(!$p.WaitForExit(60000)) {throw 'Rendering tests timed out'}
$p.Refresh()
Get-Content -LiteralPath (Join-Path $project 'build\render-investigation\render-results.txt')
if($p.ExitCode -ne 0) {throw 'Rendering tests failed'}

