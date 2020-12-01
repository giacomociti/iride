module SparqlQueryProviderImplementation

open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Common
open SparqlProviderlHelper
open TypeProviderHelper
open VDS.RDF.Query
open VDS.RDF.Parsing

[<TypeProvider>]
type SparqlQueryProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let executingAssembly = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)
        
    let createResultType (providedAssembly: ProvidedAssembly) (bindings: ResultVariables) =
        let resultType = ProvidedTypeDefinition(providedAssembly, ns, "Result", Some typeof<obj>, isErased=false)

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
        |> List.map (fun v -> ProvidedProperty(v.VariableName, getType v.Type, getterCode = function
            | [this] ->
                let varName = v.VariableName
                let result = Expr.FieldGet(this, field)
                let node = <@@ ((%%result :  SparqlResult).Item varName) @@>
                Expr.Call(getConverterMethod v.Type, [node])
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember

        bindings.OptionalVariables
        |> List.map (fun v ->
            let typ = ProvidedTypeBuilder.MakeGenericType(typedefof<Option<_>>, [getType v.Type])
            ProvidedProperty(v.VariableName, typ, getterCode = function
            | [this] ->
                let varName = v.VariableName
                let result = Expr.FieldGet(this, field)
                let hasValue = <@@ ((%%result: SparqlResult).HasBoundValue varName) @@>
                let node = <@@ ((%%result: SparqlResult).Item varName) @@>
                let typedValue = Expr.Call(getConverterMethod v.Type, [node])
                let some = Expr.Call(typ.GetMethod "Some", [typedValue])
                let none = Expr.PropertyGet(typ.GetProperty "None")
                Expr.IfThenElse(hasValue, some, none)
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember
        resultType

    let createType (typeName, sparqlQuery, rdfSchema, schemaQuery) =
        let providedAssembly = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        
        let queryText = getSparqlText config.ResolutionFolder sparqlQuery
        let parameterNames = getParameterNames queryText
        let parameters = getParameters parameterNames
        let defaultValues = parameters |> List.map (fun x -> getDefaultValue x.Type)
        let parsedQuery = 
            CommandRuntime.GetCmdText(queryText, List.ofSeq parameterNames, List.toArray defaultValues)
            |> SparqlQueryParser().ParseFromString
        
        if rdfSchema <> "" then
            schemaQuery
            |> getGraphProperties config.ResolutionFolder rdfSchema 
            |> List.map (fun x -> x.Uri)
            |> checkSchema parsedQuery.NamespaceMap queryText
        
        match parsedQuery.QueryType with
        | SparqlQueryType.Ask
        | SparqlQueryType.Construct
        | SparqlQueryType.Describe
        | SparqlQueryType.DescribeAll -> ()
        | _ -> 
            getBindings parsedQuery parameterNames
            |> createResultType providedAssembly
            |> providedType.AddMember
                
        createTextMethod queryText parameters
        |> providedType.AddMember

        providedAssembly.AddTypes [providedType]
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "SparqlQueryProvider", Some typeof<obj>, isErased=false)
        let queryText = ProvidedStaticParameter("Query", typeof<string>)
        let rdfSchema = ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
        let schemaQuery = ProvidedStaticParameter("SchemaQuery", typeof<string>, parameterDefaultValue = SchemaQuery.RdfPropertiesAndClasses)
        result.DefineStaticParameters([queryText; rdfSchema; schemaQuery], fun typeName args -> 
            createType(typeName, string args.[0], string args.[1], string args.[2]))

        result.AddXmlDoc """<summary>SPARQL query.</summary>
           <param name='Query'>SPARQL query text. Variables prefixed with '$' are treated as input parameters.</param>
           <param name='Schema'>RDF vocabulary to limit allowed IRIs in the query.</param>
           <param name='SchemaQuery'>SPARQL query to extract the vocabulary. Default to classes and properties.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])