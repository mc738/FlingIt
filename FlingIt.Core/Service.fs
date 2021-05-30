namespace FlingIt.Core

open System
open System.Net.Http
open System.Net.Http.Json
open FlingIt.Core.Context

[<AutoOpen>]
module private Internal =

    let configureClient (http: HttpClient) =
        http.BaseAddress <- Uri("http://localhost:42000")

type FlingItService(http: HttpClient) =
    
    let _ = configureClient http

    member _.Send(message: MessageRequest) =
        async {
            let content = JsonContent.Create(message)
            let! response = http.PostAsync("/send", content) |> Async.AwaitTask
            let! responseMessage = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return
                match response.IsSuccessStatusCode with
                | true -> Ok $"Request send successfully. Response: '{responseMessage}'"
                | false -> Error $"Could not send request. Error: {responseMessage}"
        }
    
    member _.Health() =
        async {
            let! response = http.GetAsync("/health") |> Async.AwaitTask
            let! responseMessage = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return
                match response.IsSuccessStatusCode with
                | true -> Ok $"Request send successfully. Response: '{responseMessage}'"
                | false -> Error $"Could not send request. Error: {responseMessage}"
        }