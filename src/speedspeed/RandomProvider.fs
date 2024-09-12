module speedspeed.RandomProvider

open System

type RandomProvider = byte array -> unit

type Kind =
    | Zero
    | PRandom
    | OncePRandom


let private zeroProvider _ = ()


let private randomProvider (bytes: byte array) =
    Random.Shared.NextBytes bytes


let private oncePRandom () =
    let mutable isDone = false
    (fun (bytes: byte array) ->
        if isDone then
            ()
        else
            isDone <- true
            Random.Shared.NextBytes bytes)


let get (kind: Kind) : RandomProvider =
    match kind with
    | Zero -> zeroProvider
    | PRandom -> randomProvider
    | OncePRandom -> oncePRandom ()

