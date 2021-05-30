namespace FlingIt.WebApi.Routes

open FlingIt.Core.Context
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Giraffe

module Messaging =

    let messageRequest : HttpHandler =
        let name = "send"

        fun (next: HttpFunc) (ctx: HttpContext) ->
            async {
                let log = ctx.GetLogger(name)
                log.LogInformation("Request received.")

                
                match tryBindRequest<MessageRequest> ctx with
                | Ok r ->
                    let! request = r
                    log.LogInformation($"Message request '{request.Reference}' .")
                    let comms = ctx.GetService<CommsContext>()
                    comms.QueueRequest request
                    return text $"Message '{request.Reference}' received and queued for processing." next ctx
                | Error e ->
                    log.LogError(sprintf "Could not bind request. Error: '%s'" e)
                    return errorHandler log name 400 e earlyReturn ctx
            }
            |> Async.RunSynchronously
            
    let routes: (HttpFunc -> HttpContext -> HttpFuncResult) list =
            [ POST
              >=> choose [ route "/send" >=> messageRequest ] ]
    