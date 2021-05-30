// Learn more about F# at http://fsharp.org

open System.IO
open System.Threading.Tasks
open FAuth
open FLite.Core
open FlingIt.Core.Context
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Diagnostics.HealthChecks
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Peeps.PeepsLogger
open Giraffe
open Microsoft.Extensions.Logging
open Peeps.Extensions
open FAuth.Extensions
open FlingIt.WebApi

let forwardingHeaderOptions =
    // Forwarding header options for nginx.
    // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-5.0
    let options = ForwardedHeadersOptions()
    options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto
    options

let configureApp (context : FAuthContext) (app : IApplicationBuilder) =    
    app
        .UseForwardedHeaders(forwardingHeaderOptions)
        .UseRouting()
        .UseCors(fun b -> b.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin() |> ignore)
        .UseEndpoints(fun ep ->
                let options = HealthCheckOptions()
                options.ResponseWriter <- fun c r -> Task.CompletedTask
                
                ep.MapHealthChecks("/health") |> ignore)
        .UseFAuth(context)
        .UseGiraffe App.routes
    
let configureServices (context : FAuthContext) (services : IServiceCollection) =
    services.AddSingleton<FAuthContext>(context)
            .AddLogging()
            .AddSingleton<QueryHandler>(fun _ -> QueryHandler.Open("/home/max/Data/app_data/FlingIt_test/data.db"))
            .AddSingleton<CommsContext>()
            //.AddHealthChecks()
            //.AddScoped<Froq.Context>(fun _ -> Froq.Context.Create(context.Security.ConnectionString))
            //.AddScoped<AuthHandler>()
            .AddCors()
            .AddGiraffe()
            .AddFAuth(context)

    services.AddHealthChecks() |> ignore

let configureLogging (peepsCtx : PeepsContext) (logging : ILoggingBuilder) =
    logging.ClearProviders()
           .AddPeeps(peepsCtx)
    |> ignore

[<EntryPoint>]
let main argv =
    
    let dir = "/home/max/Data/app_data/FlingIt_test/"
    
    match FAuthContext.Load(Path.Combine(dir, "config.json")) with
    | Ok auth ->
        let peepsCtx = PeepsContext.Create(Path.Combine(dir,"logs"), "FlingIt")
        Host.CreateDefaultBuilder()
            .ConfigureLogging(configureLogging peepsCtx)
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .UseKestrel()
                        .UseUrls("http://localhost:42000;https://localhost:42001;")
                        .Configure(configureApp auth)
                        .ConfigureServices(configureServices auth)
                        |> ignore)
            .Build()
            .Run()
        0 // return an integer exit code
    | Error message ->
        printfn "Could not load FAuth context. Error: '%s'" message
        -1