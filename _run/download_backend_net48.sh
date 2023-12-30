#!/bin/bash

# Download PlayifyRpc
wget -q --show-progress -O /tmp/PlayifyRpc.nuget https://www.nuget.org/api/v2/package/PlayifyRpc
unzip -jo /tmp/PlayifyRpc.nuget "lib/net48/PlayifyRpc.exe" -d .
rm /tmp/PlayifyRpc.nuget

# Download PlayifyUtility
wget -q --show-progress -O /tmp/PlayifyUtility.nuget https://www.nuget.org/api/v2/package/PlayifyUtility
unzip -jo /tmp/PlayifyUtility.nuget "lib/net48/PlayifyUtility.dll" -d .
rm /tmp/PlayifyUtility.nuget

# Download rpc.js
tarball_url=$(curl -s "https://registry.npmjs.org/playify-rpc/latest" | jq -r '.dist.tarball')
wget -q --show-progress -O /tmp/playify-rpc.tgz "${tarball_url}"
tar -xzf /tmp/playify-rpc.tgz --strip-components=2 -C . package/dist/
rm /tmp/playify-rpc.tgz



#Make executable
chmod +x PlayifyRpc.exe