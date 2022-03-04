open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Saturn
open Shared
open Shared.TermTypes

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration

let serviceApi = {
    getAppVersion = fun () -> async { return System.AssemblyVersionInformation.AssemblyVersion }
}

open ISADotNet
open Microsoft.AspNetCore.Http

let dagApiv1 = {
    parseAnnotationTablesToDagHtml = fun worksheetBuildingBlocks -> async {
        let factors, protocol, assay =  JsonExport.parseBuildingBlockSeqsToAssay worksheetBuildingBlocks
        let processSequence = Option.defaultValue [] assay.ProcessSequence
        let dag = Viz.DAG.fromProcessSequence (processSequence,Viz.Schema.NFDIBlue) |> CyjsAdaption.MyHTML.toEmbeddedHTML
        return dag
    }
}

let swateJsonAPIv1 = {
    parseAnnotationTableToAssayJson = fun (worksheetName,buildingblocks) -> async {
        let factors, protocol, assay = JsonExport.parseBuildingBlockToAssay worksheetName buildingblocks
        let parsedJsonStr = ISADotNet.Json.Assay.toString assay
        return parsedJsonStr
    }
    parseAnnotationTableToProcessSeqJson = fun (worksheetName,buildingblocks) -> async {
        let factors, protocol, assay = JsonExport.parseBuildingBlockToAssay worksheetName buildingblocks
        let parsedJsonStr = ISADotNet.Json.ProcessSequence.toString assay.ProcessSequence.Value
        return parsedJsonStr
    }
    parseAnnotationTableToTableJson = fun (worksheetName,buildingblocks) -> async {
        let factors, protocol, assay = JsonExport.parseBuildingBlockToAssay worksheetName buildingblocks
        let parsedJsonStr = (ISADotNet.Json.AssayCommonAPI.RowWiseAssay.fromAssay >> ISADotNet.Json.AssayCommonAPI.RowWiseAssay.toString) assay
        return parsedJsonStr
    }
    parseAnnotationTablesToAssayJson = fun worksheetBuildingBlocks -> async {
        let factors, protocol, assay =  JsonExport.parseBuildingBlockSeqsToAssay worksheetBuildingBlocks
        let parsedJsonStr = ISADotNet.Json.Assay.toString assay
        return parsedJsonStr
    }
    parseAnnotationTablesToProcessSeqJson = fun worksheetBuildingBlocks -> async {
        let factors, protocol, assay =  JsonExport.parseBuildingBlockSeqsToAssay worksheetBuildingBlocks
        let parsedJsonStr = ISADotNet.Json.ProcessSequence.toString assay.ProcessSequence.Value
        return parsedJsonStr
    }
    parseAnnotationTablesToTableJson = fun worksheetBuildingBlocks -> async {
        let factors, protocol, assay =  JsonExport.parseBuildingBlockSeqsToAssay worksheetBuildingBlocks
        let parsedJsonStr = (ISADotNet.Json.AssayCommonAPI.RowWiseAssay.fromAssay >> ISADotNet.Json.AssayCommonAPI.RowWiseAssay.toString) assay
        return parsedJsonStr
    }
    parseAssayJsonToBuildingBlocks = fun jsonString -> async {
        let table = JsonImport.assayJsonToTable jsonString
        if table.Sheets.Length = 0 then failwith "Unable to find any Swate annotation table information! Please check if uploaded json and chosen json import type match."
        let buildingBlocks = table.Sheets |> Array.ofList |> Array.map(fun s -> s.SheetName,s.toInsertBuildingBlockList |> Array.ofList)
        return buildingBlocks
    }
    parseTableJsonToBuildingBlocks = fun jsonString -> async {
        let table = JsonImport.tableJsonToTable jsonString
        if table.Sheets.Length = 0 then failwith "Unable to find any Swate annotation table information! Please check if uploaded json and chosen json import type match."
        let buildingBlocks = table.Sheets |> Array.ofList |> Array.map(fun s -> s.SheetName,s.toInsertBuildingBlockList |> Array.ofList)
        return buildingBlocks
    }
    parseProcessSeqToBuildingBlocks = fun jsonString -> async {
        let table = JsonImport.processSeqJsonToTable jsonString
        if table.Sheets.Length = 0 then failwith "Unable to find any Swate annotation table information! Please check if uploaded json and chosen json import type match."
        let buildingBlocks = table.Sheets |> Array.ofList |> Array.map(fun s -> s.SheetName,s.toInsertBuildingBlockList |> Array.ofList)
        return buildingBlocks
    }
}

let isaDotNetCommonAPIv1 : IISADotNetCommonAPIv1 =
    let assayFromByteArray (byteArray: byte []) =
        let ms = new MemoryStream(byteArray)
        let jsonStr = ISADotNet.XLSX.AssayFile.Assay.fromStream ms
        jsonStr
    let investigationFromByteArray (byteArray: byte []) =
        let ms = new MemoryStream(byteArray)
        let jsonStr =
            ISADotNet.XLSX.Investigation.fromStream ms
        jsonStr
    //let customXmlFromByteArray (byteArray: byte []) =
    //    let ms = new MemoryStream(byteArray)
    //    let jsonStr =
    //        ISADotNet.XLSX.AssayFile.SwateTable.SwateTable.readSwateTablesFromStream ms
    //        |> Array.ofSeq
    //        |> Array.map (fun x -> ISADotNet.JsonExtensions.toString x)
    //    jsonStr
    {
        /// This functions takes an ISA-XLSX file as byte [] and converts it to a ISA-JSON Assay.
        toAssayJson = fun byteArray -> async {
            let assay = assayFromByteArray byteArray |> fun (_,_,_,assay) -> assay
            return box assay
        }
        /// This functions reads an ISA-XLSX protocol template as byte [] and returns template metadata and the correlated assay.json.
        toSwateTemplateJson = fun byteArray -> async {
            let metadata = TemplateMetadata.parseDynMetadataFromByteArr byteArray
            let ms = new MemoryStream(byteArray)
            let doc = FSharpSpreadsheetML.Spreadsheet.fromStream ms false
            let tableName = metadata.TryGetValue "Table"
            let assay = ISADotNet.Assay.fromTemplateSpreadsheet (doc, string tableName.Value) 
            let assayJson = ISADotNet.Json.Assay.toString assay.Value
            metadata.SetValue("TemplateJson",assayJson)
            return box metadata
        }
        /// This functions takes an ISA-XLSX file as byte [] and converts it to a ISA-JSON Investigation.
        toInvestigationJson = fun byteArray -> async {
            let investigation = investigationFromByteArray byteArray
            return box investigation
        }
        toProcessSeqJson = fun byteArray -> async {
            let assay = assayFromByteArray byteArray 
            let processList = assay |> fun (_,_,_,assay) -> Option.defaultValue [] assay.ProcessSequence
            return box processList
        }
        toTableJson = fun byteArray -> async {
            let assay = assayFromByteArray byteArray 
            let table = assay |> fun (_,_,_,assay) -> assay |> ISADotNet.Json.AssayCommonAPI.RowWiseAssay.fromAssay
            return box table
        }
        /// This functions takes an ISA-XLSX file as byte [] and converts it to a ISA-JSON Assay.
        toAssayJsonStr = fun byteArray -> async {
            let assayJsonString = assayFromByteArray byteArray |> fun (_,_,_,assay) -> ISADotNet.Json.Assay.toString assay
            return assayJsonString
        }
        /// This functions reads an ISA-XLSX protocol template as byte [] and returns template metadata and the correlated assay.json.
        toSwateTemplateJsonStr = fun byteArray -> async {
            let metadata = TemplateMetadata.parseDynMetadataFromByteArr byteArray
            let ms = new MemoryStream(byteArray)
            let doc = FSharpSpreadsheetML.Spreadsheet.fromStream ms false
            let tableName = metadata.TryGetValue "Table"
            let assay = ISADotNet.Assay.fromTemplateSpreadsheet (doc, string tableName.Value) 
            let assayJson = ISADotNet.Json.Assay.toString assay.Value
            metadata.SetValue("TemplateJson",assayJson)
            let jsonExp = metadata.toJson()
            return jsonExp
        }
        /// This functions takes an ISA-XLSX file as byte [] and converts it to a ISA-JSON Investigation.
        toInvestigationJsonStr = fun byteArray -> async {
            let investigationJson = investigationFromByteArray byteArray |> ISADotNet.Json.Investigation.toString
            return investigationJson
        }
        toProcessSeqJsonStr = fun byteArray -> async {
            let assay = assayFromByteArray byteArray 
            let processJSon = assay |> fun (_,_,_,assay) -> Option.defaultValue "" (Option.map ISADotNet.Json.ProcessSequence.toString assay.ProcessSequence) 
            return processJSon
        }
        toTableJsonStr = fun byteArray -> async {
            let assay = assayFromByteArray byteArray 
            let processJSon = assay |> fun (_,_,_,assay) -> assay |> (ISADotNet.Json.AssayCommonAPI.RowWiseAssay.fromAssay >> ISADotNet.Json.AssayCommonAPI.RowWiseAssay.toString) 
            return processJSon
        }
        testPostNumber = fun num -> async {
            let res = $"Hey you just sent us a number. Is this your number {num}?"
            return res
        }
        getTestNumber = fun () -> async {
            return "42"
        }
    }

let ontologyApi (credentials : OntologyDB.Neo4JCredentials) : IOntologyAPIv1 =
    /// Neo4j prefix query does not provide any measurement on distance between query and result.
    /// Thats why we apply sorensen dice after the database search.
    let sorensenDiceSortTerms (searchStr:string) (terms: Term []) =
        terms |> SorensenDice.sortBySimilarity searchStr (fun term -> term.Name)
    
    {
        //Development
        getTestNumber = fun () -> async { return 42 }

        //Ontology related requests

        getAllOntologies = fun () ->
            async {
                printfn "get ontologies"
                let results = OntologyDB.Queries.Ontology(credentials).getAll() |> Array.ofSeq
                return results
            }

        // Term related requests
        getTermSuggestions = fun (max:int,typedSoFar:string) ->
            async {
                let dbSearchRes =
                    match typedSoFar with
                    | Regex.Aux.Regex Regex.Pattern.TermAccessionPatternSimplified foundAccession ->
                        OntologyDB.Queries.Term(credentials).getByAccession foundAccession
                    /// This suggests we search for a term name
                    | notAnAccession ->
                        OntologyDB.Queries.Term(credentials).getByName notAnAccession
                    |> Array.ofSeq
                    |> sorensenDiceSortTerms typedSoFar
                let arr = if dbSearchRes.Length <= max then dbSearchRes else Array.take max dbSearchRes
                return arr
            }

        getTermSuggestionsByParentTerm = fun (max:int,typedSoFar:string,parentTerm:TermMinimal) ->
            async {
                let dbSearchRes =
                    match typedSoFar with
                    | Regex.Aux.Regex Regex.Pattern.TermAccessionPatternSimplified foundAccession ->
                        OntologyDB.Queries.Term(credentials).getByAccession foundAccession
                    | _ ->
                        if parentTerm.TermAccession = ""
                        then
                            OntologyDB.Queries.Term(credentials).getByNameAndParent_Name(typedSoFar,parentTerm.Name)
                        else
                            OntologyDB.Queries.Term(credentials).getByNameAndParent(typedSoFar,parentTerm)
                    |> Array.ofSeq
                    |> sorensenDiceSortTerms typedSoFar
                let res = if dbSearchRes.Length <= max then dbSearchRes else Array.take max dbSearchRes
                return res
            }

        getAllTermsByParentTerm = fun (parentTerm:TermMinimal) ->
            async {
                let searchRes = OntologyDB.Queries.Term(credentials).getAllByParent parentTerm |> Array.ofSeq
                return searchRes  
            }

        getTermSuggestionsByChildTerm = fun (max:int,typedSoFar:string,childTerm:TermMinimal) ->
            async {

                let dbSearchRes =
                    match typedSoFar with
                    | Regex.Aux.Regex Regex.Pattern.TermAccessionPatternSimplified foundAccession ->
                        OntologyDB.Queries.Term(credentials).getByAccession foundAccession
                    | _ ->
                        if childTerm.TermAccession = ""
                        then
                            OntologyDB.Queries.Term(credentials).getByNameAndChild_Name (typedSoFar,childTerm.Name)
                        else
                            OntologyDB.Queries.Term(credentials).getByNameAndChild(typedSoFar,childTerm.TermAccession)
                    |> Array.ofSeq
                    |> sorensenDiceSortTerms typedSoFar
                let res = if dbSearchRes.Length <= max then dbSearchRes else Array.take max dbSearchRes
                return res
            }

        getAllTermsByChildTerm = fun (childTerm:TermMinimal) ->
            async {
                let searchRes = OntologyDB.Queries.Term(credentials).getAllByChild (childTerm) |> Array.ofSeq
                return searchRes  
            }

        getTermsForAdvancedSearch = fun advancedSearchOption ->
            async {
                let result = OntologyDB.Queries.Term(credentials).getByAdvancedTermSearch(advancedSearchOption) |> Array.ofSeq
                let filteredResult =
                    if advancedSearchOption.KeepObsolete then
                        result
                    else
                        result |> Array.filter (fun x -> x.IsObsolete |> not)
                return filteredResult
            }

        getUnitTermSuggestions = fun (max:int,typedSoFar:string, unit:UnitSearchRequest) ->
            async {
                let dbSearchRes =
                    match typedSoFar with
                    | Regex.Aux.Regex Regex.Pattern.TermAccessionPatternSimplified foundAccession ->
                        OntologyDB.Queries.Term(credentials).getByAccession foundAccession
                    | notAnAccession ->
                        OntologyDB.Queries.Term(credentials).getByName(notAnAccession,sourceOntologyName="uo")
                    |> Array.ofSeq
                    |> sorensenDiceSortTerms typedSoFar
                let res = if dbSearchRes.Length <= max then dbSearchRes else Array.take max dbSearchRes
                return (res, unit)
            }

        getTermsByNames = fun (queryArr) ->
            async {
                let result =
                    queryArr |> Array.map (fun searchTerm ->
                        {searchTerm with
                            SearchResultTerm =
                                // check if search string is empty. This case should delete TAN- and TSR- values in table
                                if searchTerm.Term.Name = "" then None
                                // check if term accession was found. If so search also by this as it is unique
                                elif searchTerm.Term.TermAccession <> "" then
                                    let searchRes = OntologyDB.Queries.Term(credentials).getByAccession searchTerm.Term.TermAccession
                                    if Seq.isEmpty searchRes then
                                        None
                                    else
                                        searchRes |> Seq.head |> Some
                                // if term is a unit it should be contained inside the unit ontology, if not it is most likely free text input.
                                elif searchTerm.IsUnit then
                                    let searchRes = OntologyDB.Queries.Term(credentials).getByName(searchTerm.Term.Name,sourceOntologyName="uo") |> Array.ofSeq |> sorensenDiceSortTerms searchTerm.Term.Name
                                    if Seq.isEmpty searchRes then
                                        None
                                    else
                                        searchRes |> Array.head |> Some
                                // check if parent term was found and try find term via parent term
                                elif searchTerm.ParentTerm.IsSome then
                                    let searchRes = OntologyDB.Queries.Term(credentials).getByNameAndParent(searchTerm.Term.Name,searchTerm.ParentTerm.Value)
                                    if Seq.isEmpty searchRes then
                                        // if no term can be found by is_a directed search do standard search by name
                                        // no need to search for name and accession, as accession clearly defines a term and is checked in the if branch above.
                                        let searchRes' = OntologyDB.Queries.Term(credentials).getByName(searchTerm.Term.Name) |> Array.ofSeq |> sorensenDiceSortTerms searchTerm.Term.Name
                                        if Array.isEmpty searchRes' then None else searchRes' |> Array.head |> Some
                                    else
                                        searchRes |> Seq.head |> Some
                                // if none of the above apply we do a standard term search
                                else
                                    let searchRes = OntologyDB.Queries.Term(credentials).getByName(searchTerm.Term.Name) |> Array.ofSeq |> sorensenDiceSortTerms searchTerm.Term.Name
                                    if Array.isEmpty searchRes then None else searchRes |> Array.head |> Some
                        }
                    )
                return result
            }
    }

//let protocolApi cString = {
//    getAllProtocolsWithoutXml = fun () -> async {
//        let protocols = ProtocolDB.getAllProtocols cString
//        return protocols
//    }

//    getProtocolByName = fun prot -> async { return ProtocolDB.getProtocolByName cString prot }

//    getProtocolsByName = fun (names) -> async {
//        let prot = names |> Array.map (fun x -> ProtocolDB.getProtocolByName cString x)
//        let protsWithXml = protsWithoutXml |> Array.map (ProtocolDB.getXmlByProtocol cString)
//        return prot
//    }

//    increaseTimesUsed = fun templateName -> async {
//        ProtocolDB.increaseTimesUsed cString templateName
//        return ()
//    }
//}

let testApi (ctx: HttpContext): ITestAPI = {
    test = fun () -> async {
        let c =
            let settings = ctx.GetService<IConfiguration>()
            let credentials : OntologyDB.Neo4JCredentials= {
                User        = settings.["neo4j-username"]
                Pw          = settings.["neo4j-pw"]
                BoltUrl     = settings.["neo4j-uri"]
                DatabaseName= settings.["neo4j-db"]
            }
            credentials
        //let exmp = OntologyDB.Queries.Term(c).getByAdvancedTermSearch(termName="insturment~ -Shimadzu")
        return "Info", "nothing active here"
    }
    postTest = fun (termName) -> async {
        let c =
            let settings = ctx.GetService<IConfiguration>()
            let credentials : OntologyDB.Neo4JCredentials= {
                User        = settings.["neo4j-username"]
                Pw          = settings.["neo4j-pw"]
                BoltUrl     = settings.["neo4j-uri"]
                DatabaseName= settings.["neo4j-db"]
            }
            credentials
        let exmp = OntologyDB.Queries.Term(c).getByName(termName,sourceOntologyName="ms")
        return "Info", sprintf "%A" (exmp |> Seq.length)
    }
}

let errorHandler (ex:exn) (routeInfo:RouteInfo<HttpContext>) =
    let msg = sprintf "%A %s @%s." ex.Message System.Environment.NewLine routeInfo.path
    Propagate msg

//let createIProtocolApiv1 cString =
//    Remoting.createApi()
//    |> Remoting.withRouteBuilder Route.builder
//    |> Remoting.fromValue (protocolApi cString)
//    //|> Remoting.withDocs Shared.URLs.DocsApiUrl DocsAnnotationAPIvs1.ontologyApiDocsv1
//    |> Remoting.withDiagnosticsLogger(printfn "%A")
//    |> Remoting.withErrorHandler errorHandler
//    |> Remoting.buildHttpHandler

let createIOntologyApiv1 credentials =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue (ontologyApi credentials)
    //|> Remoting.withDocs Shared.URLs.DocsApiUrl DocsAnnotationAPIvs1.ontologyApiDocsv1
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let createIServiceAPIv1 =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue serviceApi
    |> Remoting.withDocs Shared.URLs.DocsApiUrl2 DocsServiceAPIvs1.serviceApiDocsv1
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let createISADotNetCommonAPIv1 =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue isaDotNetCommonAPIv1
    //|> Remoting.withDocs "/api/IISADotNetCommonAPIv1/docs" DocsISADotNetAPIvs1.isaDotNetCommonApiDocsv1
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let createExpertAPIv1 =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue swateJsonAPIv1
    //|> Remoting.withDocs "/api/IExpertAPIv1/docs" DocsISADotNetAPIvs1.isaDotNetCommonApiDocsv1
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let createDagApiv1 =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue dagApiv1
    //|> Remoting.withDocs "/api/IExpertAPIv1/docs" DocsISADotNetAPIvs1.isaDotNetCommonApiDocsv1
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let createTestApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromContext testApi
    //|> Remoting.withDocs "/api/IExpertAPIv1/docs" DocsISADotNetAPIvs1.isaDotNetCommonApiDocsv1
    |> Remoting.withDiagnosticsLogger(printfn "%A")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

///// due to a bug in Fable.Remoting this does currently not work as inteded and is ignored. (https://github.com/Zaid-Ajaj/Fable.Remoting/issues/198)
//let mainApiController = router {

    ////
    //forward @"/IOntologyAPIv1" (fun next ctx ->
    //    let cString = 
    //        let settings = ctx.GetService<IConfiguration>()
    //        settings.["Swate:ConnectionString"]
    //    createIOntologyApiv1 cString next ctx
    //)

//    //
//    forward @"/IServiceAPIv1" (fun next ctx ->
//        createIServiceAPIv1 next ctx
//    )
//}

let topLevelRouter = router {
    get "/test/test1" (htmlString "<h1>Hi this is test response 1</h1>")

    forward @"" (fun next ctx ->
        let credentials =
            let settings = ctx.GetService<IConfiguration>()
            let (credentials : OntologyDB.Neo4JCredentials) = {
                User        = settings.["neo4j-username"]
                Pw          = settings.["neo4j-pw"]
                BoltUrl     = settings.["neo4j-uri"]
                DatabaseName= settings.["neo4j-db"]
            }
            credentials
        createIOntologyApiv1 credentials next ctx
    )

    //forward @"" (fun next ctx ->
    //    let cString = 
    //        let settings = ctx.GetService<IConfiguration>()
    //        settings.["Swate:ConnectionString"]
    //    createIProtocolApiv1 cString next ctx
    //)

    //
    forward @"" (fun next ctx ->
        createIServiceAPIv1 next ctx
    )

    
    forward @"" (fun next ctx ->
        createISADotNetCommonAPIv1 next ctx
    )

    forward @"" (fun next ctx ->
        createExpertAPIv1 next ctx
    )

    forward @""(fun next ctx ->
        createDagApiv1 next ctx
    )

    forward @""(fun next ctx ->
        createTestApi next ctx
    )
}

let app = application {
    url "http://localhost:5000/"//"http://0.0.0.0:5000/"
    use_router topLevelRouter
    memory_cache
    //logging 
    use_static "public"
    use_gzip
    logging (fun (builder: ILoggingBuilder) -> builder.SetMinimumLevel(LogLevel.Debug) |> ignore)
}

app
    .ConfigureAppConfiguration(
        System.Action<Microsoft.Extensions.Hosting.HostBuilderContext,IConfigurationBuilder> ( fun ctx config ->
            config.AddUserSecrets("6de80bdf-2a05-4cf7-a1a8-d08581dfa887") |> ignore
            config.AddJsonFile("dev.json",true,true)            |> ignore
            config.AddJsonFile("production.json",true,true)     |> ignore
        )
)
|> run

