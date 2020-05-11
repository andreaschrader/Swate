open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Saturn
open Shared

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.Extensions.Logging

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let port = 8080us

let annotatorApi = {
    testOntologyInsert =
        fun (name,version,definition,created,user) ->
            async {
                let createdEntry = OntologyDB.insertOntology name version definition created user
                printfn "created ontology entry: \t%A" createdEntry
                return createdEntry
            }
}

let docs = Docs.createFor<IAnnotatorAPI>()

let apiDocumentation =
    Remoting.documentation "CSBAnnotatorAPI" [
        docs.route <@ fun api (name,version,definition,created,user) -> api.testOntologyInsert (name,version,definition,created,user) @>
        |> docs.alias "maketestinsert"
        |> docs.description "I dont know i just want to test xd"
        |> docs.example<@ fun api -> api.testOntologyInsert ("Name","SooSOSO","FIIIF",System.DateTime.UtcNow,"MEEM") @>
    ]


let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue annotatorApi
    |> Remoting.withDocs "/api/docs" apiDocumentation
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler(
        (fun x y -> Propagate (sprintf "[ERROR]: %A @ %A" x y))
    )
    |> Remoting.buildHttpHandler

let topLevelRouter = router {
    get "/test/test1" (htmlString "<h1>Hi this is test response 1</h1>")
    forward "/api" webApp
}

let app = application {
    url ("https://0.0.0.0:" + port.ToString() + "/")
    force_ssl
    use_router topLevelRouter
    memory_cache
    use_static publicPath
    use_gzip
    //logging (fun (builder: ILoggingBuilder) -> builder.SetMinimumLevel(LogLevel.Trace) |> ignore)
}

run app
