param(
 [string]$ZipPath=(Join-Path (Split-Path $PSScriptRoot) 'release\DeskFrame-1.0.0.zip'),
 [string]$ChecksumPath=(Join-Path (Split-Path $PSScriptRoot) 'release\SHA256SUMS.txt'),
 [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string[]]$PrivatePatterns,
 [string]$PublicationRoot=(Split-Path $PSScriptRoot)
)
$ErrorActionPreference='Stop'
# Keep organization-specific expressions outside public files. This required list
# preserves private-identifier checks without disclosing the identifiers themselves.
foreach($pattern in $PrivatePatterns) {
 if([String]::IsNullOrWhiteSpace($pattern) -or [regex]::IsMatch('', $pattern)) {throw 'Supply nonempty, specific private patterns'}
}
$genericPatterns=@(
 '(?i)[a-z]:[\\/]+Users[\\/]+[^\s\\/]+',
 '(?i)/(?:home|Users)/[^\s/]+',
 '(?i)-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
 '(?i)\b(?:api[_-]?key|password|secret|access[_-]?token)\s*[:=]\s*["''][A-Za-z0-9_+/.=-]{16,}["'']',
 '\bAKIA[0-9A-Z]{16}\b',
 '\bgh[pousr]_[A-Za-z0-9]{30,}\b'
)
function Has-Exposure([string]$value,[string[]]$patterns) {
 foreach($pattern in $patterns) {if([regex]::IsMatch($value,$pattern,[Text.RegularExpressions.RegexOptions]::IgnoreCase)){return $true}}
 return $false
}
function Assert-Clean([byte[]]$bytes,[string]$label) {
 foreach($encoding in @([Text.Encoding]::UTF8,[Text.Encoding]::Unicode,[Text.Encoding]::BigEndianUnicode)) {
  if(Has-Exposure ($encoding.GetString($bytes)) ($genericPatterns+$PrivatePatterns)) {throw ('Privacy exposure detected in '+$label)}
 }
}
function Hash-Bytes([byte[]]$bytes) {
 $sha=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant()} finally {$sha.Dispose()}
}
# In-memory detector checks; fixtures are synthetic and never written to disk.
$profileFixture=('C:'+ [char]92 +'Users'+[char]92+'fixture-account'+[char]92+'photo.jpg')
if(!(Has-Exposure $profileFixture $genericPatterns)){throw 'Profile-path detector failed'}
if(!(Has-Exposure ('-----BEGIN '+'PRIVATE KEY-----') $genericPatterns)){throw 'Private-key detector failed'}
if(!(Has-Exposure ('api_key='+[char]34+'0123456789abcdefghijkl'+[char]34) $genericPatterns)){throw 'Credential detector failed'}
if(!(Has-Exposure 'internal-fixture' @('internal-fixture'))){throw 'Private deny-list detector failed'}
if(Has-Exposure 'DeskFrame generated artwork' $genericPatterns){throw 'Clean detector fixture failed'}

$expectedZip='035e4d85f08e271d20865777db6021607ee5cd0b67d968d70169bb58b35e8961'
$expectedChecksum='78a7e6404e68b5e0eb4fe6f0c3576655d6543ffe92567feb6d68970bc8034796'
$expected=@{
 'CHANGELOG.md'='def830d70825f56e8a833a04baed8a8394a6a97f6d21a4f6faf071e343eba66d'
 'DeskFrame.exe'='102c8846774293ba986e7e6b03c5ac8f8037d4910a890a9d172c9a92067cce43'
 'DeskFrame.exe.config'='be5443298b94cfbb4a4b609b918bed904df2ea101777a58ce1323778550d125d'
 'LICENSE'='1d535923406108a5adddca9daa537a173dfe9d18aa77865bd09159711dd8fde2'
 'README.txt'='242a936f4cf3544634c42173c5cd5d71f6dbbe73e73ff8875caf5e348bdc545f'
 'RELEASE_NOTES.md'='98c92833a64efc840b250f147dd3164b159a809f10bd4736fadfe67dfe63b08f'
 'Start portable.cmd'='efa65d799b46de475ccd3c4f9206f68d7540d76290544c103287f1834d709305'
 'THIRD_PARTY_NOTICES.md'='cc9c1133260808bb038cbff88873d8707f7a29a54c7fd2c2801b0d6cf76fa2c3'
}
$zipHash=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if($zipHash -ne $expectedZip){throw 'Not the verified 1.0.0 candidate ZIP'}
if((Get-FileHash -LiteralPath $ChecksumPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedChecksum){throw 'Candidate checksum file changed'}
$declared=(Get-Content -Raw -LiteralPath $ChecksumPath).Trim()
if($declared -ne ($expectedZip+'  DeskFrame-1.0.0.zip')){throw 'Declared checksum/name mismatch'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ZipPath))
try {
 # Windows PowerShell Compress-Archive emits backslash entry separators.
 $actual=@($zip.Entries | ForEach-Object {$_.FullName.Replace('\','/')} | Sort-Object)
 $allowed=@($expected.Keys | ForEach-Object {'DeskFrame-1.0.0/'+$_} | Sort-Object)
 if($actual.Count -ne 8 -or (Compare-Object $allowed $actual)){throw 'Unexpected or duplicate ZIP entries'}
 foreach($entry in $zip.Entries) {
  $stream=$entry.Open();$memory=New-Object IO.MemoryStream
  try {
   $stream.CopyTo($memory);$bytes=$memory.ToArray();$name=$entry.FullName.Substring('DeskFrame-1.0.0/'.Length)
   if((Hash-Bytes $bytes) -ne $expected[$name]){throw ('Candidate payload mismatch: '+$name)}
   Assert-Clean $bytes ('ZIP entry '+$name)
  } finally {$stream.Dispose();$memory.Dispose()}
 }
} finally {$zip.Dispose()}
$sourceMap=@{'DeskFrame.exe.config'='src\DeskFrame.exe.config';'README.txt'='USER_GUIDE.txt';'CHANGELOG.md'='CHANGELOG.md';'LICENSE'='LICENSE';'Start portable.cmd'='Start portable.cmd';'RELEASE_NOTES.md'='RELEASE_NOTES.md';'THIRD_PARTY_NOTICES.md'='THIRD_PARTY_NOTICES.md'}
foreach($name in $sourceMap.Keys) {
 if((Get-FileHash -LiteralPath (Join-Path $PublicationRoot $sourceMap[$name]) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected[$name]){throw ('Packaged/source content mismatch: '+$name)}
}
$publicFiles=@(Get-ChildItem -LiteralPath $PublicationRoot -File -Recurse -Force)
foreach($file in $publicFiles) {
 $relative=$file.FullName.Substring([IO.Path]::GetFullPath($PublicationRoot).TrimEnd('\','/').Length+1)
 if($relative -match '(?i)(^|[\\/])(?:DeskFrameData|\.git|build|bin|obj)[\\/]|\.(?:pdb|log|tmp|user|suo)$|(^|[\\/])(?:settings\.json|portable\.flag)$'){throw ('Unintended public artifact: '+$relative)}
 Assert-Clean ([Text.Encoding]::UTF8.GetBytes($relative)) 'public filename'
 Assert-Clean ([IO.File]::ReadAllBytes($file.FullName)) ('public file '+$relative)
}
Write-Output ('PASS candidate ZIP, checksum and all eight payload hashes; '+$publicFiles.Count+' public files scanned; detector fixtures passed. No files written or application launched.')
