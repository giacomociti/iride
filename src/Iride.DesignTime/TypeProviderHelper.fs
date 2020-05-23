module TypeProviderHelper

    open Iride
    open Iride.SparqlHelper
    open VDS.RDF
    open Microsoft.FSharp.Quotations
    open ProviderImplementation.ProvidedTypes

    let getSparqlText resolutionFolder (sparqlParameter: string) =
        if sparqlParameter.EndsWith ".rq" 
        then 
            System.IO.Path.Combine(resolutionFolder, sparqlParameter)
            |> System.IO.File.ReadAllText
        else sparqlParameter

    let getType = function
        | Node -> typeof<INode>
        | Iri -> typeof<System.Uri>
        | Literal -> typeof<string>
        | Integer -> typeof<int>
        | Number -> typeof<decimal>
        | Date -> typeof<System.DateTime>
        | Time -> typeof<System.DateTimeOffset>
        | Boolean -> typeof<bool>

    let getConverterMethod = function
        | Node -> typeof<CommandRuntime>.GetMethod "AsNode"
        | Iri -> typeof<CommandRuntime>.GetMethod "AsUri"
        | Literal -> typeof<CommandRuntime>.GetMethod "AsString"
        | Integer -> typeof<CommandRuntime>.GetMethod "AsInt"
        | Number -> typeof<CommandRuntime>.GetMethod "AsDecimal"
        | Date -> typeof<CommandRuntime>.GetMethod "AsDateTime"
        | Time -> typeof<CommandRuntime>.GetMethod "AsDateTimeOffset"
        | Boolean -> typeof<CommandRuntime>.GetMethod "AsBoolean"

    let private dummyUri = System.Uri "http://iride.dummy"

    let getDefaultValue = function
        | Node -> CommandRuntime.ToNode(dummyUri)
        | Iri -> CommandRuntime.ToNode(dummyUri)
        | Literal -> CommandRuntime.ToNode("")
        | Integer -> CommandRuntime.ToNode(0)
        | Number -> CommandRuntime.ToNode(0M)
        | Date -> CommandRuntime.ToNode(System.DateTime.Today)
        | Time -> CommandRuntime.ToNode(System.DateTimeOffset.Now)
        | Boolean -> CommandRuntime.ToNode(true)

    let createTextMethod commandText inputParameters =
        let parameterNames, parameters =
            inputParameters
            |> List.map (fun x -> x.ParameterName, ProvidedParameter(x.ParameterName, getType x.Type))
            |> List.unzip
        ProvidedMethod(
            methodName = "GetText", 
            parameters = parameters, 
            returnType = typedefof<string>, 
            invokeCode = (fun pars ->
                let converters = pars |> List.map (fun par ->
                    let m = typeof<CommandRuntime>.GetMethod("ToNode", [| par.Type |])
                    Expr.Call(m, [par]))
                let array = Expr.NewArray(typeof<INode>, converters)
                <@@ CommandRuntime.GetCmdText(commandText, parameterNames, %%array) @@>),
            isStatic = true)