# Anti-tamper manifest for Release builds (Windows).
# Run after: dotnet publish src/ArcadeMania.App -f net10.0-windows10.0.19041.0 -c Release -o publish/win
# Writes integrity.json (SHA-256 of every shipped .dll/.exe) next to the exe. At startup the app re-hashes the
# files; if anything was modified it shows a message and exits cleanly. It never deletes or corrupts files.
param([string]$PublishDir = "publish/win")
$manifest = [ordered]@{}
Get-ChildItem -Path $PublishDir -Recurse -Include *.dll, *.exe | Sort-Object FullName | ForEach-Object {
    $rel = [IO.Path]::GetRelativePath((Resolve-Path $PublishDir), $_.FullName)
    $manifest[$rel] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
}
$manifest | ConvertTo-Json | Set-Content -Path (Join-Path $PublishDir "integrity.json") -Encoding UTF8
Write-Host "integrity.json written with $($manifest.Count) entries. Sign the exe/dlls with Authenticode (signtool) BEFORE running this script."
