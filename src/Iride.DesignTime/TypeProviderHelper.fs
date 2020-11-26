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

    let getConverterMethodExpression = function
        | Node -> <@@ CommandRuntime.AsNode(Unchecked.defaultof<INode>) @@>
        | Iri -> <@@ CommandRuntime.AsUri(Unchecked.defaultof<INode>) @@>
        | Literal -> <@@ CommandRuntime.AsString(Unchecked.defaultof<INode>) @@>
        | Integer -> <@@ CommandRuntime.AsInt(Unchecked.defaultof<INode>) @@>
        | Number -> <@@ CommandRuntime.AsDecimal(Unchecked.defaultof<INode>) @@>
        | Date -> <@@ CommandRuntime.AsDateTime(Unchecked.defaultof<INode>) @@>
        | Time -> <@@ CommandRuntime.AsDateTimeOffset(Unchecked.defaultof<INode>) @@>
        | Boolean -> <@@ CommandRuntime.AsBoolean(Unchecked.defaultof<INode>) @@>

    let getNodeExtractorMethodExpression = function
        | Node -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<INode>) @@>
        | Iri -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<System.Uri>) @@>
        | Literal -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<string>) @@>
        | Integer -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<int>) @@>
        | Number -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<decimal>) @@>
        | Date -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<System.DateTime>) @@>
        | Time -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<System.DateTimeOffset>) @@>
        | Boolean -> <@@ CommandRuntime.ToNode(Unchecked.defaultof<bool>) @@>

    let getMethodInfo = function
       | Patterns.Call(_, methodInfo, _) -> methodInfo
       | _ -> failwith "Unexpected expression" 

    let getConverterMethod =  getConverterMethodExpression >> getMethodInfo

    let getNodeExtractorMethod = getNodeExtractorMethodExpression >> getMethodInfo

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
