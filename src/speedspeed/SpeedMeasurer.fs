module speedspeed.SpeedMeasurer

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open ByteSizeLib
open RandomProvider

type Mode =
    | Send
    | Receive
    | Both

[<ReferenceEquality;NoComparison>]
type ServerOptions =
    { ListenAddress: IPEndPoint
      Mode: Mode
      CancelToken: CancellationToken
      RandomProvider: RandomProvider
      BufferSize: int }

[<ReferenceEquality;NoComparison>]
type ClientOptions =
    { RemoteAddress: IPEndPoint
      Mode: Mode
      CancelToken: CancellationToken
      RandomProvider: RandomProvider
      BufferSize: int }

[<ReferenceEquality;NoComparison>]
type SpeedReport =
    { Current: ByteSize
      Min: ByteSize
      Max: ByteSize
      Avg: ByteSize }


let defaultPort = 43399

let defaultMode = Both

let defaultRandomProvider = PRandom

let defaultBufferSizeBytes = 1024


let private serveClient (tcpClient: TcpClient) (serverOptions: ServerOptions) =
    async {
        use tcpClient = tcpClient
        use stream = tcpClient.GetStream ()

        let sender =
            match serverOptions.Mode with
            | Both
            | Send ->
                async {
                    let buffer : byte array = Array.zeroCreate serverOptions.BufferSize

                    try
                        while not serverOptions.CancelToken.IsCancellationRequested do
                            serverOptions.RandomProvider buffer
                            do! stream.WriteAsync (buffer, 0, Array.length buffer, serverOptions.CancelToken) |> Async.AwaitTask
                    with
                    | :? OperationCanceledException
                    | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                    | exc ->
                        Console.eprintfn "Client %O. General send error." tcpClient.Client.RemoteEndPoint
                        Console.eprintfn "%O" exc
                }
            | Receive -> async { return () }

        let receiver =
            match serverOptions.Mode with
            | Both
            | Receive ->
                async {
                    let buffer : byte array = Array.zeroCreate serverOptions.BufferSize

                    try
                        while not serverOptions.CancelToken.IsCancellationRequested do
                            let! _ = stream.ReadAsync (buffer, 0, Array.length buffer, serverOptions.CancelToken) |> Async.AwaitTask
                            ()
                    with
                    | :? OperationCanceledException
                    | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                    | exc ->
                        Console.eprintfn "Client %O. General receive error." tcpClient.Client.RemoteEndPoint
                        Console.eprintfn "%O" exc
                }
            | Send -> async { return () }

        let! _ = Async.Parallel [| sender; receiver |]
        Console.printfn "Done serving client %O" tcpClient.Client.RemoteEndPoint
        return ()
    }


let startServer (options: ServerOptions) =
    async {
        let mutable tasks : Task list = []
        use tcpListener = new TcpListener (options.ListenAddress)
        tcpListener.Start ()
        Console.printfn "Server listening on %O" options.ListenAddress

        try
            while not options.CancelToken.IsCancellationRequested do
                let! tcpClient = tcpListener.AcceptTcpClientAsync(options.CancelToken).AsTask() |> Async.AwaitTask
                Console.printfn "Got remote connection from %O" tcpClient.Client.RemoteEndPoint
                let task = serveClient tcpClient options |> Async.StartAsTask
                tasks <- (task :> Task) :: tasks
        with
        | :? OperationCanceledException
        | :? TaskCanceledException
        | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException || exc.InnerException :? TaskCanceledException) -> ()
        | exc ->
            Console.eprintfn "General server error."
            Console.eprintfn "%O" exc

        Console.printfn "Stopping server"
        tcpListener.Stop ()
        Console.printfn "Tcp listening stopped"
        Console.printfn "Waiting for current clients"
        do! Async.AwaitTask ( Task.WhenAll (Array.ofList tasks) )
        Console.printfn "Current clients are done."
    }


let getSpeedReport (speed: ByteSize list) =
    let zero = ByteSize.FromBytes 0
    match speed with
    | [] -> { Current = zero; Min = zero; Max = zero; Avg = zero }
    | speed ->
        let current = List.head speed
        let min = List.minBy (fun (l: ByteSize) -> l.Bytes) speed
        let max = List.maxBy (fun (l: ByteSize) -> l.Bytes) speed
        let avg =
            let sum = List.fold (fun (l: ByteSize) (r: ByteSize) -> l.Add r) zero speed
            ByteSize.FromBytes (sum.Bytes / (float (List.length speed)))
        { Current = current
          Min = min
          Max = max
          Avg = avg }


let runClient (options: ClientOptions) =
    async {
        use tcpClient = new TcpClient ()
        Console.printfn "Connecting to %O" options.RemoteAddress
        do! tcpClient.ConnectAsync(options.RemoteAddress, options.CancelToken).AsTask() |> Async.AwaitTask
        Console.printfn "Connected"
        use stream = tcpClient.GetStream ()

        let mutable transmitSpeed : ByteSize list = []
        let mutable receiveSpeed : ByteSize list = []

        let sender =
            match options.Mode with
            | Both
            | Send ->
                async {
                    let buffer : byte array = Array.zeroCreate options.BufferSize
                    let stopwatch = Stopwatch.StartNew ()
                    let mutable sentBytes = 0L

                    try
                        while not options.CancelToken.IsCancellationRequested do
                            options.RandomProvider buffer
                            do! stream.WriteAsync (buffer, 0, Array.length buffer, options.CancelToken) |> Async.AwaitTask
                            sentBytes <- sentBytes + (int64 (Array.length buffer))

                            if stopwatch.ElapsedMilliseconds >= 1000 then
                                let seconds = stopwatch.Elapsed.TotalSeconds
                                let speed = ByteSize.FromBytes ((float sentBytes) / seconds)

                                transmitSpeed <- speed :: transmitSpeed
                                stopwatch.Restart ()
                                sentBytes <- 0L

                    with
                    | :? OperationCanceledException
                    | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                    | exc ->
                        Console.eprintfn "General send error"
                        Console.eprintfn "%O" exc
                }
            | Receive -> async { return () }

        let receiver =
            match options.Mode with
            | Both
            | Receive ->
                async {
                    let buffer : byte array = Array.zeroCreate options.BufferSize
                    let stopwatch = Stopwatch.StartNew ()
                    let mutable receivedBytes = 0L

                    try
                        while not options.CancelToken.IsCancellationRequested do
                            let! gotBytes = stream.ReadAsync (buffer, 0, Array.length buffer, options.CancelToken) |> Async.AwaitTask
                            receivedBytes <- receivedBytes + (int64 gotBytes)

                            if stopwatch.ElapsedMilliseconds >= 1000 then
                                let seconds = stopwatch.Elapsed.TotalSeconds
                                let speed = ByteSize.FromBytes ((float receivedBytes) / seconds)

                                receiveSpeed <- speed :: receiveSpeed
                                stopwatch.Restart();
                                receivedBytes <- 0L
                    with
                    | :? OperationCanceledException
                    | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                    | exc ->
                        Console.eprintfn "General receive error."
                        Console.eprintfn "%O" exc
                }
            | Send -> async { return () }

        let printSpeedReport () =
            let tx = getSpeedReport transmitSpeed
            let rx = getSpeedReport receiveSpeed
            let lines = [
                "Transmit"
                (sprintf "\tCurrent: %.2f MBytes/sec, %.2f MBits/sec" tx.Current.MebiBytes ((float tx.Current.Bits / 1024.0 / 1024.0 )))
                (sprintf "\tMin....: %.2f MBytes/sec, %.2f MBits/sec" tx.Min.MebiBytes ((float tx.Min.Bits / 1024.0 / 1024.0 )))
                (sprintf "\tMax....: %.2f MBytes/sec, %.2f MBits/sec" tx.Max.MebiBytes ((float tx.Max.Bits / 1024.0 / 1024.0 )))
                (sprintf "\tAvg....: %.2f MBytes/sec, %.2f MBits/sec" tx.Avg.MebiBytes ((float tx.Avg.Bits / 1024.0 / 1024.0 )))

                "Receive"
                (sprintf "\tCurrent: %.2f MBytes/sec, %.2f MBits/sec" rx.Current.MebiBytes ((float rx.Current.Bits / 1024.0 / 1024.0 )))
                (sprintf "\tMin....: %.2f MBytes/sec, %.2f MBits/sec" rx.Min.MebiBytes ((float rx.Min.Bits / 1024.0 / 1024.0 )))
                (sprintf "\tMax....: %.2f MBytes/sec, %.2f MBits/sec" rx.Max.MebiBytes ((float rx.Max.Bits / 1024.0 / 1024.0 )))
                (sprintf "\tAvg....: %.2f MBytes/sec, %.2f MBits/sec" rx.Avg.MebiBytes ((float rx.Avg.Bits / 1024.0 / 1024.0 )))
            ]
            Console.printMultiline lines


        let reporter =
            async {
                while not options.CancelToken.IsCancellationRequested do
                    do! Async.Sleep (TimeSpan.FromSeconds 1)
                    printSpeedReport ()
            }

        Console.printfn "Sending data"
        let! _ = Async.Parallel [ sender; receiver; reporter ]
        Console.printfn "Client done"
        printSpeedReport ()
    }
