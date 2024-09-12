# speedspeed

Network speed tester utility. Much like `iperf3`. Main distinction is that `speedspeed` sends pseudo random traffic.
This way, in case something like DPI detects `iperf3` traffic and does something with it, making results unrepresentative,
you can use `speedspeed` to measure network speed. This way DPI should not detect particular protocol as it will see pseudo random
stream of bytes.

## Building

- Install [.NET 8.0](https://dot.net)
- Run this command

```bash
dotnet build src/speedspeed/speedspeed.fsproj -c Release
dotnet publish src/speedspeed/speedspeed.fsproj -c Release -o publish
```

## Usage

Run `speedspeed` with `--help` flag to see help.

```
USAGE: speedspeed [--help] [--port <int>] [--mode <send|receive|both>] [--random <zero|prandom|onceprandom>] [--buffersize <int>] [--server] [<subcommand> [<options>]]

SUBCOMMANDS:

    --client, -c <options>
                          Start in client mode.

    Use 'speedspeed <subcommand> --help' for additional information.

OPTIONS:

    --port, -p <int>      Port to listen/connect to. Default 43399
    --mode <send|receive|both>
                          Send/Receive mode. Default Both
    --random, --rnd <zero|prandom|onceprandom>
                          Random bytes provider. Default PRandom
    --buffersize, --buffer-size <int>
                          Send/Receive buffer size in bytes. Default 1024
    --server, -s          Start in server mode.
    --help                display this list of options.
```

### Server mode

```
speedspeed -s
```

This wil listen on all IPs on default port.

### Client mode

```
speedspeed -c --address 127.0.0.1
```

This will start client, connect to 127.0.0.1 and measture send/receive speeds.

### Mode (Send, Receive, Both)

Server and client does not negotiate send(receive) mode. They both just send & receive data in parallel (by default, `--mode both`).
In case you want to test one direction (**server -> client** or **server <- client** or **server <-> client**) you need
to specify `--mode` flag appropriately on both sides. For example,

- Speed from server to client: Start server with `--mode send` and client with `--mode receive`
- Speed to server from client: Start server with `--mode receive` and client with `--mode send`
- Speed to/from server and client: Start both server & client with `--mode both`
