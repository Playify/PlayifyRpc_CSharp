This library allows calling functions accross different devices. It works for:

* [C# Applications](https://www.nuget.org/packages/PlayifyRpc/)
* [Web Applications (and NodeJs)](https://www.npmjs.com/package/playify-rpc)
* [Microcontrollers (Esp8266 and ESP32)](https://registry.platformio.org/libraries/playify/playify-rpc)

# Web Interface

You can access http://127.0.0.1:4590/rpc to get to a simple web interface,
here you can evaluate simple RPC calls directly from the browser.

Additionally you can directly evaluate calls using
http://127.0.0.1:4590/rpc/EXPRESSION where expression is a valid function call,
e.g. if you want to call Rpc.getRegistrations(), you can access
http://127.0.0.1:4590/rpc/Rpc.getRegistrations() and get the return value as http response

# Server

The RPC Server is only available for C#, clients are available in other languages as well.
The server should run on a device that is reachable by all its clients.
Clients can only communicate with each other, if they are connected to the server,
direct connections between clients are not supported.

## Installation (Server)

Make sure dotnet 6 (or above) is installed.

Install the server files into an empty directory using one of the commands.

Linux:

```(shell)
 curl -sSL https://raw.githubusercontent.com/Playify/PlayifyRpc_CSharp/master/_run/get-rpc.sh | bash
```

Windows:

```(shell)
powershell "iex (iwr https://raw.githubusercontent.com/Playify/PlayifyRpc_CSharp/master/_run/get-rpc.ps1 -UseBasicParsing).Content"
```

Now you can use the `rpc.bat` or the `rpc.sh` script to run the rpc server.

## Security

When the server sets the `RPC_TOKEN` environment variable to some arbitrary passphrase,
then only clients are accepted, that also have the RPC_TOKEN set to the same passphrase

* Web clients need the `RPC_TOKEN` Cookie
* C# clients either need to define the `RPC_TOKEN` Environment variable,
  or pass it into `Rpc.Connect`
* PlatformIO clients need to pass it into `Rpc::connect`

## Nginx

If you want to use nginx (for e.g. https, or having multiple webpages on a single server)
you should use the following location block

```(config)
location ~ ^/rpc {
	proxy_http_version 1.1;
	proxy_set_header Host $http_host;
	proxy_set_header Upgrade $http_upgrade;
	proxy_set_header Connection "upgrade";
	proxy_pass http://127.0.0.1:4590;
}
```

PlayifyRpc needs access to /rpc.js, /rpc.js.map, /rpc (for WebSockets)
and also to /rpc.html /rpc (as webpage) and /rpc/* for easy debugging