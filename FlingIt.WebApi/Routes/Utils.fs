namespace FlingIt.WebApi.Routes

open System
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Giraffe

[<AutoOpen>]
module private Utils =
    
    let errorHandler (logger: ILogger) name code message =
        logger.LogError("Error '{code}' in route '{name}', message: '{message};.", code, name, message)
        setStatusCode code >=> text message

    let authorize : (HttpFunc -> HttpContext -> HttpFuncResult) =
        requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

    let getClaim (ctx: HttpContext) (name: string) = ctx.User.FindFirst(name).Value

    let getUserRef (ctx: HttpContext) =
        match Guid.TryParse(getClaim ctx "userRef") with
        | true, ref -> Some(ref)
        | false, _ -> None

    let tryBindRequest<'a> (ctx: HttpContext) =
        try
            let result =
                ctx.BindJsonAsync<'a>() |> Async.AwaitTask

            Ok(result)
        with ex -> Error(sprintf "Could not bind request to type '%s'" typeof<'a>.Name)