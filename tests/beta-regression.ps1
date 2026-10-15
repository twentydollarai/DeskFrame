param([string]$Executable,[switch]$Baseline,[switch]$UiOnly)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
if(!$Executable){$Executable=Join-Path $project 'build\DeskFrame.exe'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$harness=Join-Path $project 'build\DeskFrame.BetaRegression.exe'
& $compiler /nologo /target:winexe /optimize+ "/out:$harness" "/win32manifest:$project\src\app.manifest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/reference:$framework\WPF\UIAutomationClient.dll" "/reference:$framework\WPF\UIAutomationTypes.dll" "/reference:$framework\WPF\WindowsBase.dll" (Join-Path $PSScriptRoot 'BetaRegression.cs')
if($LASTEXITCODE -ne 0){throw 'Beta regression compilation failed'}
$arguments=@(('"'+$Executable+'"'))
if($UiOnly){$arguments+='--ui-only'}
if($Baseline){$arguments+='--baseline'}
$p=Start-Process -FilePath $harness -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(!$p.WaitForExit(120000)){throw 'Beta regression timed out; inspect isolated processes'}
$p.Refresh()
$run=Get-ChildItem -LiteralPath (Join-Path $project 'build\beta-fixes-20260926') -Directory | Where-Object {$_.Name -match '^(baseline|candidate)-'} | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content -LiteralPath (Join-Path $run.FullName 'results.txt') -Tail 25
Write-Output ('Evidence: '+$run.FullName)
if($p.ExitCode -ne 0){throw 'Targeted beta regression failed'}
