param(
    [string]$TargetPath,
    [string]$Configuration
) 

$key = "Registry::HKEY_CURRENT_USER\Software\Classes\*\shell\ShareX\command"
$value = "(default)"
$ShareXPrefix = (Get-ItemProperty -Path $key -Name $value).$value[0]
$ShareXPrefix = $ShareXPrefix.Substring(0,$ShareXPrefix.length-5)

$UploadFilename = "$($TargetPath)"

Copy-Item $TargetPath -Destination "..\..\..\1_app\Plugins\FFXIVAPP.Plugin.Log\FFXIVAPP.Plugin.Log.dll"

Start-Process $ShareXPrefix $UploadFilename