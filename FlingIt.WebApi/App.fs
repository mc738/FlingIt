namespace FlingIt.WebApi

open System
open Microsoft.AspNetCore.Http
open FlingIt.WebApi.Routes
open Giraffe

module App =

    let routes : (HttpFunc -> HttpContext -> HttpFuncResult) =
        let routes =
            List.concat [ Test.routes; Messaging.routes; ]

        choose routes