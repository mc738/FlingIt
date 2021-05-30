// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Net.Http
open FLite.Core
open FlingIt.Core
open FlingIt.Core.Context

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

[<EntryPoint>]
let main argv =

    let subscription = Guid.Parse("5421713b-603f-404c-9e26-616866d7ae2f")
    
    
    
    let client = FlingItService(new HttpClient())

    [1..5]
    |> List.map (fun i ->
        
        printf $"Sending request {i}..."
        
        let request = {
            Reference = Guid.NewGuid()
            Subscription = subscription
            Token = "test"
            Template = "password-reset"
            To = [ "me@mclifford.dev" ]
            Cc = []
            Bcc = []
            Attachments = []
            Replacements = [
                { Key = "FIRST_NAME"; Value = "" }
                { Key = "RESET_DATE"; Value = "" }
                { Key = "RESET_URL"; Value = "" }
            ]
        }
        
        match client.Send request |> Async.RunSynchronously with
        | Ok msg -> printfn $"{msg}"
        | Error e -> printfn $"{e}"
        Async.Sleep 1000 |> Async.RunSynchronously 
        ) |> ignore
        
    
    
    
    //let qh = QueryHandler.Create("/home/max/Data/app_data/FlingIt_test/data.db")
    
    //initialize qh

    0 // return an integer exit code