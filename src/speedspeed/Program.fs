module speedspeed.Program

open System
open System.Globalization
open System.Net
open System.Net.Sockets
open Argu
open CliArgs
open System.Runtime.InteropServices
open System.Threading
open SpeedMeasurer
open RandomProvider


let setupServer (mainArgs: ParseResults<MainArgs>) (cancelToken: CancellationToken) =
    let endpoint = IPEndPoint (IPAddress.Any, mainArgs.GetResult (MainArgs.Port, defaultValue = defaultPort ))
    let random = RandomProvider.get <| mainArgs.GetResult (MainArgs.Random, defaultValue = defaultRandomProvider)
    let bufferSize = mainArgs.GetResult (MainArgs.BufferSize, defaultValue = defaultBufferSizeBytes)
    let mode = mainArgs.GetResult (MainArgs.Mode, defaultValue = SpeedMeasurer.defaultMode)

    SpeedMeasurer.startServer { ListenAddress = endpoint
                                Mode = mode
                                CancelToken = cancelToken
                                RandomProvider = random
                                BufferSize = bufferSize }


let setupClient (mainArgs: ParseResults<MainArgs>) (clientArgs: ParseResults<ClientArgs>) (cancelToken: CancellationToken) =
    let hostname = clientArgs.GetResult ClientArgs.Address
    let fail msg = failwithf "Could not resolve %s to IP address: %s" hostname msg
    let port = mainArgs.GetResult (MainArgs.Port, defaultValue = defaultPort)
    let random = RandomProvider.get <| mainArgs.GetResult (MainArgs.Random, defaultValue = defaultRandomProvider)
    let bufferSize = mainArgs.GetResult (MainArgs.BufferSize, defaultValue = defaultBufferSizeBytes)
    let mode = mainArgs.GetResult (MainArgs.Mode, defaultValue = defaultMode)
    use dnsTimeout = new CancellationTokenSource (TimeSpan.FromSeconds 5)

    let endPoint =
        try
            let task = Dns.GetHostAddressesAsync (hostname, dnsTimeout.Token)
            task.Wait ()
            match task.Result with
            | [|  |] -> fail "Empty response"
            | ips ->
                match Array.tryFind (fun (ip: IPAddress) -> ip.AddressFamily = AddressFamily.InterNetwork) ips with
                | Some ipv4 -> IPEndPoint (ipv4, port)
                | None ->
                    match Array.tryFind (fun (ip: IPAddress) -> ip.AddressFamily = AddressFamily.InterNetworkV6) ips with
                    | Some ipv6 -> IPEndPoint (ipv6, port)
                    | None -> fail "Could not find either IPv4 nor IPv6."
        with
        | :? OperationCanceledException
        | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) ->
            failwithf "Timeout"

    Console.printfn "Resolved %s to %O" hostname endPoint
    SpeedMeasurer.runClient { RemoteAddress = endPoint
                              Mode = mode
                              CancelToken = cancelToken
                              RandomProvider = random
                              BufferSize = bufferSize }



[<EntryPoint>]
let main argv =
    CultureInfo.CurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.CurrentUICulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.InvariantCulture
    try
        let arguments = ArgumentParser.Create<MainArgs>().ParseCommandLine argv
        use cancelToken = new CancellationTokenSource ()
        use signalHandler = PosixSignalRegistration.Create (PosixSignal.SIGINT, (fun ctx ->
            ctx.Cancel <- true
            Console.printfn "Got SIGINT signal. Stopping..."
            cancelToken.Cancel ()))

        let job =
            match arguments.TryGetResult Server, arguments.TryGetResult Client with
            | None, None ->
                Console.printfn "No mode is specified (server or client). Run with '--help' to see usage."
                async { return () }
            | Some _, Some _ ->
                Console.printfn "Can't run both in server and client mode. Choose something one."
                async { return () }
            | Some _, None -> setupServer arguments cancelToken.Token
            | None, Some clientArgs -> setupClient arguments clientArgs cancelToken.Token

        Async.RunSynchronously job
        0
    with
    | :? ArguParseException as exc ->
        eprintfn "%s" exc.Message
        1
    | exc ->
        eprintfn "%O" exc
        1
