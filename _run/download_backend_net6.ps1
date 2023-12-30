# Create temporary folder
$tempFolder = Join-Path $env:TEMP 'PlayifyRpc'
if (Test-Path -Path $tempFolder -PathType Container) {
    Remove-Item $tempFolder -Recurse -Force
}
$null=New-Item -ItemType Directory -Path $tempFolder -Force

# Download PlayifyRpc
Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/PlayifyRpc" -OutFile "$tempFolder\PlayifyRpc.zip"
Expand-Archive -Path "$tempFolder\PlayifyRpc.zip" -DestinationPath $tempFolder -Force
Copy-Item -Path "$tempFolder\lib/net6.0/PlayifyRpc.dll" -Destination . -Force

# Download PlayifyUtility
Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/PlayifyUtility" -OutFile "$tempFolder\PlayifyUtility.zip"
Expand-Archive -Path "$tempFolder\PlayifyUtility.zip" -DestinationPath $tempFolder -Force
Copy-Item -Path "$tempFolder\lib/net6.0/PlayifyUtility.dll" -Destination . -Force

# Download rpc.js
$tarballUrl = (Invoke-WebRequest -Uri "https://registry.npmjs.org/playify-rpc/latest" -UseBasicParsing).Content | ConvertFrom-Json | Select-Object -ExpandProperty dist | Select-Object -ExpandProperty tarball
Invoke-WebRequest -Uri $tarballUrl -OutFile "$tempFolder\playify-rpc.tgz" -UseBasicParsing
tar -xzf "$tempFolder\playify-rpc.tgz" --strip-components=2 -C . package/dist/

# Make executable
echo '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}' > PlayifyRpc.runtimeconfig.json

# Delete temporary folder
Remove-Item $tempFolder -Recurse -Force
