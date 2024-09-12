module speedspeed.Console

open System
open Printf

let private locker = obj ()

let mutable private lastPrintWasMultilined = false

let printfn fmt =
    ksprintf (fun msg -> lock locker (fun () ->
        lastPrintWasMultilined <- false
        Console.WriteLine msg)) fmt


let printf fmt =
    ksprintf (fun msg -> lock locker (fun () ->
        lastPrintWasMultilined <- false
        Console.Write msg)) fmt


let eprintfn fmt =
    ksprintf (fun msg -> lock locker (fun () ->
        lastPrintWasMultilined <- false
        Console.Error.WriteLine msg)) fmt


let eprintf fmt =
    ksprintf (fun msg -> lock locker (fun () ->
        lastPrintWasMultilined <- false
        Console.Error.Write msg)) fmt


let printMultiline (lines: string list) =
    match lines with
    | [] -> ()
    | lines ->
        lock locker (fun () ->
            let lineCount = List.length lines

            if lastPrintWasMultilined then
                let startLine = Math.Clamp (Console.CursorTop - lineCount, 0, Console.WindowHeight)
                let spaces = String (Array.init Console.WindowWidth (fun _ -> ' '))
                Console.SetCursorPosition (0, startLine)
                for _ in 0..(lineCount - 1) do
                    Console.WriteLine spaces
                Console.SetCursorPosition (0, startLine)

            for line in lines do
                Console.WriteLine line

            lastPrintWasMultilined <- true)

