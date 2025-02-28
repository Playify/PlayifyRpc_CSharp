This library allows calling functions across different devices. It works for:

* [C# Applications](https://www.nuget.org/packages/PlayifyRpc/)
* [Web Applications (and NodeJs)](https://www.npmjs.com/package/playify-rpc)
* [Microcontrollers (ESP8266 and ESP32)](https://registry.platformio.org/libraries/playify/playify-rpc)

# Web Interface

You can access `http://127.0.0.1:4590/rpc` to get to a simple web interface, where you can evaluate simple RPC calls
directly from the browser (and the browser devtools).

Additionally, you can call functions directly using `http://127.0.0.1:4590/rpc/EXPRESSION`.

For example, if you want to call `Rpc.getRegistrations()`, you can
access `http://127.0.0.1:4590/rpc/Rpc.getRegistrations()` and get the return value from the HTTP response.

Using `http://127.0.0.1:4590/rpc/EXPRESSION/pretty` you can get a prettified JSON back.

Using `http://127.0.0.1:4590/rpc/EXPRESSION/void` you don't get any response
back ([HTTP Status Code 204](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/204)).

Using `http://127.0.0.1:4590/rpc/EXPRESSION/download=test.txt` you get the response as file download<br/>
Using `http://127.0.0.1:4590/rpc/EXPRESSION/file=test.html` you open the response directly in the browser

# Server

The RPC Server is only available for C#, clients are available in other languages as well. The server should run on a
device that is reachable by all its clients. Clients can only communicate with each other if they are connected to the
server, direct connections between clients are not supported.

## Installation (Server)

Make sure `dotnet 6` (or above) is installed.

Install the server files into an empty directory using one of the following commands.

Linux:

```sh
curl -sSL https://raw.githubusercontent.com/Playify/PlayifyRpc_CSharp/master/_run/get-rpc.sh | bash
```

Windows:

```cmd
powershell "irm https://raw.githubusercontent.com/Playify/PlayifyRpc_CSharp/master/_run/get-rpc.ps1|iex"
```

Now you can use the `rpc.sh` or the `rpc.bat` script to run the RPC server.

## Security

When the server sets the `RPC_TOKEN` environment variable to some arbitrary passphrase, only clients that also have
the `RPC_TOKEN` set to the same passphrase are accepted.

* Web clients need the `RPC_TOKEN` cookie.
* NodeJs clients need to define the `RPC_TOKEN` environment variable.
* C# clients either need to define the `RPC_TOKEN` environment variable or pass it into `Rpc.Connect`.
* PlatformIO clients need to pass it into `Rpc::connect`.

## Nginx

If you want to use Nginx (e.g., for HTTPS or having multiple web pages on a single server), you should use the following
location block:

```nginx
location ~ ^/rpc {
	proxy_http_version 1.1;
	proxy_set_header Host $http_host;
	proxy_set_header Upgrade $http_upgrade;
	proxy_set_header Connection "upgrade";
	proxy_pass http://127.0.0.1:4590;
}
```

PlayifyRpc needs access to `/rpc.js`, `/rpc.js.map`, `/rpc` (HTTP & WebSockets), and `/rpc/*`.

# Client

Call `Rpc.connect()` to connect to a server. By default, it uses the `RPC_URL` and `RPC_TOKEN` environment variables,
but you can specify them as well.

## Calling Functions

Functions can be called using `Rpc.CallFunction` or using the `RpcFunction` class.

When calling a function, you get a `PendingCall`, which acts like a `Task<?>`. It can be cast to a `Task` of any
supported data type.

A `PendingCall` has a `Cancel()` method to signal to the other end that the operation should be cancelled. This is only
useful if the executing end also handles the cancellation, otherwise, nothing will happen.

A `PendingCall` also has a `SendMessage()` method, which is used to send arbitrary messages to the executor. Using
the `AddMessageListener()` method, you can listen to messages that get sent by the executor. Alternatively, you can use
the `PendingCall` as an `IAsyncEnumerable` to consume the messages within an `await foreach` loop.

## Registering Types

Using `Rpc.RegisterType()`, you can register a:

* Type (using `typeof(XXX)`) to make a static class accessible to others.
* Instance (using `new XXX`).
* Invoker, preferably `DictionaryInvoker`, to register methods one by one.

You can also use the `RpcProviderAttribute` to register a static class.

## Receiving Function Calls

A method on a registered type can have any number of parameters, even `params` is supported. The parameters should be
any of the supported types. The return type should be a `Task`, `Task<?>`, a supported type, or `void`.

When a function gets executed, you can use `Rpc.GetContext()` (before any `await` statement) to get access to
RPC-specific features, as they are not available through method parameters. The resulting `FunctionCallContext` can be
used similarly to the `PendingCall`.

It has a `SendMessage()` and `AddMessageListener()` method and can also be used as an `IAsyncEnumerable`. It also has
access to a `CancellationToken` that should be used whenever possible.