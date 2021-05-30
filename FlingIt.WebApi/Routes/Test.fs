namespace FlingIt.WebApi.Routes

open Microsoft.AspNetCore.Http
open Giraffe

module Test =

    let helloWorld = text "Hello, World!"

    let downloadTest : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            async {
                return streamFile true "/home/max/Downloads/derek-oyen-3Xd5j9-drDA-unsplash.jpg" None None next ctx }
            |> Async.RunSynchronously

    let routes: (HttpFunc -> HttpContext -> HttpFuncResult) list =
        [ GET
          >=> choose [ route "/test" >=> helloWorld
                       route "/test/download" >=> downloadTest ] ]