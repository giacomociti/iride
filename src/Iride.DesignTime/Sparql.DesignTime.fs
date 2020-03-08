module SparqlImplementation
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Iride.SparqlHelper
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage
open ProviderImplementation.ProvidedTypes
open VDS.RDF.Parsing

module Helper =
    let commandText resolutionFolder (sparqlCommand: string) =
        if sparqlCommand.EndsWith ".rq" 
        then 
            System.IO.Path.Combine(resolutionFolder, sparqlCommand)
            |> System.IO.File.ReadAllText
        else sparqlCommand

    let getType = function
        | Node -> typeof<INode>
        | Iri -> typeof<System.Uri>
        | Literal -> typeof<string>
        | Integer -> typeof<int>
        | Number -> typeof<decimal>
        | Date -> typeof<System.DateTime>
        | Time -> typeof<System.DateTimeOffset>

    let converterMethod = function
        | Node -> typeof<CommandRuntime>.GetMethod "AsNode"
        | Iri -> typeof<CommandRuntime>.GetMethod "AsUri"
        | Literal -> typeof<CommandRuntime>.GetMethod "AsString"
        | Integer -> typeof<CommandRuntime>.GetMethod "AsInt"
        | Number -> typeof<CommandRuntime>.GetMethod "AsDecimal"
        | Date -> typeof<CommandRuntime>.GetMethod "AsDateTime"
        | Time -> typeof<CommandRuntime>.GetMethod "AsDateTimeOffset"


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

[<TypeProvider>]
type BasicCommandProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<Iride.CommandRuntime>.Assembly.GetName().Name = asm.GetName().Name)
    
    let checkSchema (queryText: string) uris =
        let ns = SparqlUpdateParser().ParseFromString(queryText).NamespaceMap
        let errors = SparqlHelper.check ns uris queryText
        if errors.Length > 0 then failwithf "Unknown Uris: %A\n Allowed: %A" errors uris

    let createType typeName (sparqlCommand: string) rdfSchema schemaQuery =
        let asm = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)

        let commandText = Helper.commandText config.ResolutionFolder sparqlCommand

        if rdfSchema <> "" then
            schemaQuery
            |> Iride.RdfHelper.getGraphProperties config.ResolutionFolder rdfSchema 
            |> List.map (fun x -> x.Uri.AbsoluteUri)
            |> checkSchema commandText

        commandText
        |> parameterNames
        |> parameters  
        |> Helper.createTextMethod commandText
        |> providedType.AddMember

        asm.AddTypes [providedType]
        providedType

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "SparqlParametrizedCommand", Some typeof<obj>, isErased = false)
        let commandText = ProvidedStaticParameter("Command", typeof<string>)
        let rdfSchema = ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
        let schemaQuery = ProvidedStaticParameter("SchemaQuery", typeof<string>, parameterDefaultValue = SchemaQuery.RdfPropertiesAndClasses)

        result.DefineStaticParameters([commandText; rdfSchema; schemaQuery], fun typeName args -> 
            createType typeName (string args.[0]) (string args.[1]) (string args.[2]))

        result.AddXmlDoc """<summary>SPARQL parametrized command.</summary>
           <param name='Command'>Command text. Variables prefixed with '$' are treated as input parameters.</param>
           <param name='Schema'>RDF vocabulary to limit allowed IRIs in the query.</param>
           <param name='SchemaQuery'>SPARQL query to extract the vocabulary. Default to classes and properties.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])

[<TypeProvider>]
type BasicQueryProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<Iride.CommandRuntime>.Assembly.GetName().Name = asm.GetName().Name)

        
    let createResultType (asm: ProvidedAssembly) (bindings: ResultVariables) =
        let resultType = ProvidedTypeDefinition(asm, ns, "Result", Some typeof<obj>, isErased = false)

        let field = ProvidedField("_result", typeof<SparqlResult>)
        resultType.AddMember field

        let ctor = 
            ProvidedConstructor(
                [ProvidedParameter("result", typeof<SparqlResult>)], 
                invokeCode = fun args -> 
                    match args with
                    | [this; result] ->
                      Expr.FieldSet (this, field, result)
                    | _ -> failwith "wrong ctor params")
        resultType.AddMember ctor

        bindings.Variables
        |> List.map (fun v -> ProvidedProperty(v.VariableName, Helper.getType v.Type, getterCode = function
            | [this] ->
                let varName = v.VariableName
                let result = Expr.FieldGet(this, field)
                let node = <@@ ((%%result :  SparqlResult).Item varName) @@>
                Expr.Call(Helper.converterMethod v.Type, [node])
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember

        bindings.OptionalVariables
        |> List.map (fun v ->
            let typ = ProvidedTypeBuilder.MakeGenericType(typedefof<Option<_>>, [Helper.getType v.Type])
            ProvidedProperty(v.VariableName, typ, getterCode = function
            | [this] ->
                let varName = v.VariableName
                let result = Expr.FieldGet(this, field)
                let hasValue = <@@ ((%%result: SparqlResult).HasBoundValue varName) @@>
                let node = <@@ ((%%result: SparqlResult).Item varName) @@>
                let typedValue = Expr.Call(Helper.converterMethod v.Type, [node])
                let some = Expr.Call(typ.GetMethod "Some", [typedValue])
                let none = Expr.PropertyGet(typ.GetProperty "None")
                Expr.IfThenElse(hasValue, some, none)
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember
        resultType

    let checkSchema (queryText: string) uris =
        let ns = SparqlQueryParser().ParseFromString(queryText).NamespaceMap
        let errors = SparqlHelper.check ns uris queryText
        if errors.Length > 0 then failwithf "Unknown Uris: %A\n Allowed: %A" errors uris

    let createType typeName (sparqlQuery: string) rdfSchema schemaQuery =
        let asm = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = false)
        
        let queryText = Helper.commandText config.ResolutionFolder sparqlQuery
        if rdfSchema <> "" then
            schemaQuery
            |> Iride.RdfHelper.getGraphProperties config.ResolutionFolder rdfSchema 
            |> List.map (fun x -> x.Uri.AbsoluteUri)
            |> checkSchema queryText

        let query = queryDescriptor queryText

        match query.output with
        | QueryResult.Bindings bindings ->
            let resultType = createResultType asm bindings
            providedType.AddMember resultType
        | _ -> ()
                
        Helper.createTextMethod queryText query.input
        |> providedType.AddMember

        asm.AddTypes [providedType]
        providedType

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "SparqlParametrizedQuery", Some typeof<obj>, isErased = false)
        let queryText = ProvidedStaticParameter("Query", typeof<string>)
        let rdfSchema = ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
        let schemaQuery = ProvidedStaticParameter("SchemaQuery", typeof<string>, parameterDefaultValue = SchemaQuery.RdfPropertiesAndClasses)
        result.DefineStaticParameters([queryText; rdfSchema; schemaQuery], fun typeName args -> 
            createType typeName (string args.[0]) (string args.[1]) (string args.[2]))

        result.AddXmlDoc """<summary>SPARQL query.</summary>
           <param name='Query'>SPARQL query text. Variables prefixed with '$' are treated as input parameters.</param>
           <param name='Schema'>RDF vocabulary to limit allowed IRIs in the query.</param>
           <param name='SchemaQuery'>SPARQL query to extract the vocabulary. Default to classes and properties.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])