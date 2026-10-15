$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$build = Join-Path $project 'build'
$report = Join-Path $build 'process-results.txt'
'DeskFrame release-process smoke test' | Set-Content -LiteralPath $report
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class DeskFrameProbe {
 public delegate bool Callback(IntPtr h,IntPtr p);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback cb,IntPtr p);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 public static IntPtr Find(int pid,bool frame) {
  IntPtr result=IntPtr.Zero;
  EnumWindows(delegate(IntPtr h,IntPtr p) {
   uint owner; GetWindowThreadProcessId(h,out owner);
   if(owner!=pid || !IsWindowVisible(h))return true;
   bool tool=(GetWindowLong(h,-20)&0x80)!=0;
   Rect r;GetWindowRect(h,out r);
   if(tool==frame && r.Right-r.Left>100 && r.Bottom-r.Top>100) {result=h;return false;}
   return true;
  },IntPtr.Zero);return result;
 }
 public static string Title(IntPtr h) {var s=new StringBuilder(512);GetWindowText(h,s,512);return s.ToString();}
}
'@
[DeskFrameProbe]::SetProcessDPIAware() | Out-Null
function Assert($condition,$label) { if (!$condition) { throw $label }; Add-Content -LiteralPath $report -Value ('PASS ' + $label) }
function Wait-Window($process,$frame) {
 for ($i=0;$i -lt 50;$i++) { $h=[DeskFrameProbe]::Find($process.Id,$frame); if ($h -ne [IntPtr]::Zero) {return $h}; Start-Sleep -Milliseconds 100 }
 throw 'Expected window did not appear'
}
$work=Join-Path $build ('process-work\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'DeskFrame.exe'),(Join-Path $build 'DeskFrame.exe.config') -Destination $work
$exe=Join-Path $work 'DeskFrame.exe'
$process=$null
try {
 $process=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 $setup=Wait-Window $process $false
 Assert ([DeskFrameProbe]::Title($setup) -eq 'Welcome to DeskFrame') 'Unmodified release executable opens first-run setup'
 & $exe --exit
 Assert ($process.WaitForExit(5000)) 'Clean command exit from first-run setup'
 $prior=Get-ChildItem -LiteralPath (Join-Path $build 'test-work') -Directory | Sort-Object LastWriteTime -Descending | Where-Object {
  $candidate=Join-Path $_.FullName 'prefs\settings.json'
  (Test-Path -LiteralPath $candidate) -and ((Get-Content -Raw -LiteralPath $candidate | ConvertFrom-Json).IntervalSeconds -eq 3)
 } | Select-Object -First 1
 if (!$prior) { throw 'Run the full native integration suite before the release-process smoke test' }
 $prefs=Get-Content -Raw -LiteralPath (Join-Path $prior.FullName 'prefs\settings.json') | ConvertFrom-Json
 $prefs.Folder=Join-Path $prior.FullName 'samples';$prefs.Files=@();$prefs.Width=320;$prefs.Corner='top-left';$prefs.X=$null;$prefs.Y=$null;$prefs.Crossfade=$true;$prefs.Topmost=$true;$prefs.ClickThrough=$true;$prefs.Hotkey='Ctrl+Alt+D';$prefs.Startup=$false;$prefs.ShowTray=$false;$prefs.FrameSizing='Fixed frame';$prefs.ImageFitting='Fit';$prefs.AnchorX=$null;$prefs.AnchorY=$null
 $prefs | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'DeskFrameData\settings.json') -Encoding UTF8
 $process=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 $frame=Wait-Window $process $true
 $heights=New-Object 'System.Collections.Generic.HashSet[int]'
 for($i=0;$i -lt 35;$i++) { $r=New-Object DeskFrameProbe+Rect; [DeskFrameProbe]::GetWindowRect($frame,[ref]$r) | Out-Null; $heights.Add($r.Bottom-$r.Top) | Out-Null;Start-Sleep -Milliseconds 200 }
 Assert ($heights.Count -eq 1) 'Release frame stays stable across portrait, landscape and square fades'
 $process.Refresh()
 Add-Content -LiteralPath $report -Value ('Release working set: '+[math]::Round($process.WorkingSet64/1MB,1)+' MB')
 Add-Content -LiteralPath $report -Value ('Release CPU seconds across approximately 7 seconds: '+$process.TotalProcessorTime.TotalSeconds)
 $second=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 Assert ($second.WaitForExit(3000)) 'Second instance exits'
 $setup=Wait-Window $process $false
 Assert ([DeskFrameProbe]::Title($setup) -eq 'DeskFrame Controls') 'Second launch opens existing instance controls with tray disabled'
 & $exe --exit
 Assert ($process.WaitForExit(5000)) 'Clean exit after normal slideshow'
 $saved=Get-Content -Raw -LiteralPath (Join-Path $work 'DeskFrameData\settings.json') | ConvertFrom-Json
 Assert ($null -ne $saved.X -and $saved.Folder -eq $prefs.Folder -and $saved.IntervalSeconds -eq 3) 'Position, source and UI-selected interval persist after restart'
 $process=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 $frame=Wait-Window $process $true
 Assert ($frame -ne [IntPtr]::Zero) 'Relaunch restores configured frame without setup'
 & $exe --exit
 Assert ($process.WaitForExit(5000)) 'Relaunched application exits cleanly'
 $saved.FrameSizing='Auto-fit';$saved.ImageFitting='Fill';$saved.Corner='bottom-right';$saved.AnchorX=$null;$saved.AnchorY=$null;$saved.X=$null;$saved.Y=$null
 $saved | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'DeskFrameData\settings.json') -Encoding UTF8
 $process=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 $frame=Wait-Window $process $true
 $heights=New-Object 'System.Collections.Generic.HashSet[int]'
 $anchors=New-Object 'System.Collections.Generic.HashSet[string]'
 for($i=0;$i -lt 40;$i++) {$r=New-Object DeskFrameProbe+Rect;[DeskFrameProbe]::GetWindowRect($frame,[ref]$r) | Out-Null;$heights.Add($r.Bottom-$r.Top) | Out-Null;$anchors.Add(('{0},{1}' -f $r.Right,$r.Bottom)) | Out-Null;Start-Sleep -Milliseconds 200}
 Assert ($heights.Count -gt 1 -and $anchors.Count -eq 1) 'Release Auto-fit changes dimensions with stable bottom-right anchor'
 $process.Refresh()
 Add-Content -LiteralPath $report -Value ('Auto-fit release working set: '+[math]::Round($process.WorkingSet64/1MB,1)+' MB; CPU seconds across approximately 8 seconds: '+$process.TotalProcessorTime.TotalSeconds)
 & $exe --exit
 Assert ($process.WaitForExit(5000)) 'Auto-fit release exits cleanly'
 $restored=Get-Content -Raw -LiteralPath (Join-Path $work 'DeskFrameData\settings.json') | ConvertFrom-Json
 Assert ($restored.FrameSizing -eq 'Auto-fit' -and $null -ne $restored.AnchorX -and $restored.ImageFitting -eq 'Fill') 'Auto-fit mode, anchor and inactive Fill persist in portable mode'
 $process=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 $frame=Wait-Window $process $true
 $r=New-Object DeskFrameProbe+Rect;[DeskFrameProbe]::GetWindowRect($frame,[ref]$r) | Out-Null
 Assert ($r.Right -eq $restored.AnchorX -and $r.Bottom -eq $restored.AnchorY) 'Auto-fit relaunch preserves selected corner'
 & $exe --exit
 Assert ($process.WaitForExit(5000)) 'Auto-fit relaunch exits cleanly'
 $saved.Folder=Join-Path $work 'unavailable';$saved | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'DeskFrameData\settings.json') -Encoding UTF8
 $process=Start-Process -FilePath $exe -ArgumentList '--portable' -WindowStyle Hidden -PassThru
 $setup=Wait-Window $process $false
 Assert ([DeskFrameProbe]::Title($setup) -eq 'Welcome to DeskFrame') 'Unavailable saved folder opens source recovery setup'
 & $exe --exit
 Assert ($process.WaitForExit(5000)) 'Unavailable-source exit remains clean'
 Add-Content -LiteralPath $report -Value 'ALL RELEASE-PROCESS TESTS PASSED'
} catch {Add-Content -LiteralPath $report -Value ('FAIL '+$_.Exception.Message);throw}
finally { if($process -and !$process.HasExited) { & $exe --exit; $process.WaitForExit(5000) | Out-Null } }
Get-Content -LiteralPath $report

