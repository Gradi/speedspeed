module speedspeed.CliArgs

open Argu
open SpeedMeasurer


[<NoEquality;NoComparison>]
type ClientArgs =
    | [<Mandatory>] Address of string

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Address _ -> "Remove endpoint to connect to."


[<NoEquality;NoComparison>]
type MainArgs =
    | [<AltCommandLine("-p");Inherit>] Port of int
    | [<Inherit>] Mode of Mode
    | [<AltCommandLine("--rnd");Inherit>] Random of RandomProvider.Kind
    | [<AltCommandLine("--buffer-size");Inherit>] BufferSize of int
    | [<AltCommandLine("-s")>] Server
    | [<AltCommandLine("-c")>] Client of ParseResults<ClientArgs>

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Port _ -> sprintf "Port to listen/connect to. Default %d" defaultPort
            | Mode _ -> sprintf "Send/Receive mode. Default %A" defaultMode
            | Random _ -> sprintf "Random bytes provider. Default %A" defaultRandomProvider
            | BufferSize _ -> sprintf "Send/Receive buffer size in bytes. Default %d" defaultBufferSizeBytes
            | Server -> "Start in server mode."
            | Client _ -> "Start in client mode."
