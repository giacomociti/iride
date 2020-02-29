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

    let createTextMethod commandText inputParameters =
        let parameterNames = 
            inputParameters 
            |> List.map (fun x -> x.ParameterName)
        let parameters =
            inputParameters
            |> List.map (fun x -> ProvidedParameter(x.ParameterName, getType x.Type))
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
    
    let createType typeName (sparqlCommand: string) =
        let asm = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)

        let commandText = Helper.commandText config.ResolutionFolder sparqlCommand
        let names = parameterNames commandText
        Helper.createTextMethod commandText (parameters names)
        |> providedType.AddMember

        asm.AddTypes [providedType]
        providedType

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "SparqlParametrizedCommand", Some typeof<obj>, isErased = false)
        let par = ProvidedStaticParameter("CommandText", typeof<string>)
        result.DefineStaticParameters([par], fun typeName args -> 
            createType typeName (string args.[0]))

        result.AddXmlDoc """<summary>SPARQL parametrized command.</summary>
           <param name='CommandText'>Command text. Variables prefixed with '$' are treated as input parameters.</param>
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
    do assert (typeof<Iride.QueryRuntime>.Assembly.GetName().Name = asm.GetName().Name)

    let converterMethod = function
        | Node -> typeof<QueryRuntime>.GetMethod "AsNode"
        | Iri -> typeof<QueryRuntime>.GetMethod "AsUri"
        | Literal -> typeof<QueryRuntime>.GetMethod "AsString"
        | Integer -> typeof<QueryRuntime>.GetMethod "AsInt"
        | Number -> typeof<QueryRuntime>.GetMethod "AsDecimal"
        | Date -> typeof<QueryRuntime>.GetMethod "AsDateTime"
        | Time -> typeof<QueryRuntime>.GetMethod "AsDateTimeOffset"
        
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
                Expr.Call(converterMethod v.Type, [node])
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
                let typedValue = Expr.Call(converterMethod v.Type, [node])
                let some = Expr.Call(typ.GetMethod "Some", [typedValue])
                let none = Expr.PropertyGet(typ.GetProperty "None")
                Expr.IfThenElse(hasValue, some, none)
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember
        resultType



    let createType typeName (sparqlQuery: string) =
        let asm = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = false)
        
        let queryText = Helper.commandText config.ResolutionFolder sparqlQuery            
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
        let par = ProvidedStaticParameter("QueryText", typeof<string>)
        result.DefineStaticParameters([par], fun typeName args -> 
            createType typeName (string args.[0]))

        result.AddXmlDoc """<summary>SPARQL parametrized query.</summary>
           <param name='QueryText'>Query text. Variables prefixed with '$' are treated as input parameters.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])

// TODO
// add support for update commands
// add vocabulary parameter to check that IRIs in the command text are from the vocabulary
// load text from embedded resource.


[<TypeProvider>]
type BasicProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<Iride.QueryRuntime>.Assembly.GetName().Name = asm.GetName().Name)

    let getType = function
        | Node -> typeof<INode>
        | Iri -> typeof<System.Uri>
        | Literal -> typeof<string>
        | Integer -> typeof<int>
        | Number -> typeof<decimal>
        | Date -> typeof<System.DateTime>
        | Time -> typeof<System.DateTimeOffset>

    let converterMethod = function
        | Node -> typeof<QueryRuntime>.GetMethod "AsNode"
        | Iri -> typeof<QueryRuntime>.GetMethod "AsUri"
        | Literal -> typeof<QueryRuntime>.GetMethod "AsString"
        | Integer -> typeof<QueryRuntime>.GetMethod "AsInt"
        | Number -> typeof<QueryRuntime>.GetMethod "AsDecimal"
        | Date -> typeof<QueryRuntime>.GetMethod "AsDateTime"
        | Time -> typeof<QueryRuntime>.GetMethod "AsDateTimeOffset"

    let createCtor (query: QueryDescriptor) =
        let parNames = query.input |> List.map (fun x -> x.ParameterName)
        let par = ProvidedParameter("storage", typeof<IQueryableStorage>)
        let commandText = query.commandText
        ProvidedConstructor ([par], function 
            | [storage] -> 
                <@@ QueryRuntime(%%storage, commandText, parNames) :> obj @@>
            | _ -> failwith "Expected a single parameter")
        
    let createResultType (bindings: ResultVariables) =
        
        let resultType = ProvidedTypeDefinition(asm, ns, "Result", Some typeof<obj>)
        let ctorParam = ProvidedParameter("result", typeof<SparqlResult>)
        let ctor = ProvidedConstructor([ctorParam], invokeCode = function
            | [result] -> <@@  %%result  @@>
            | _ -> failwith "Expected a single parameter")
        resultType.AddMember ctor

        bindings.Variables
        |> List.map (fun v -> ProvidedProperty(v.VariableName, getType v.Type, getterCode = function
            | [this] ->
                let varName = v.VariableName
                let node = <@@ ((%%this : obj) :?> SparqlResult).Item varName @@>
                Expr.Call(converterMethod v.Type, [node])
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember

        bindings.OptionalVariables
        |> List.map (fun v ->
            let typ = ProvidedTypeBuilder.MakeGenericType(typedefof<Option<_>>, [getType v.Type])
            ProvidedProperty(v.VariableName, typ, getterCode = function
            | [this] ->
                let varName = v.VariableName
                let sparqlResult = <@ ((%%this : obj) :?> SparqlResult) @>
                let hasValue = <@@ (%sparqlResult).HasBoundValue varName @@>
                let node = <@@ (%sparqlResult).Item varName @@>
                let typedValue = Expr.Call(converterMethod v.Type, [node])
                let some = Expr.Call(typ.GetMethod "Some", [typedValue])
                let none = Expr.PropertyGet(typ.GetProperty "None")
                Expr.IfThenElse(hasValue, some, none)
            | _ -> failwith "Expected a single parameter"))
        |>  List.iter resultType.AddMember
        resultType

    let createRunMethod (query: QueryDescriptor) resultType =
        let pars =
            query.input 
            |> List.map (fun x -> ProvidedParameter(x.ParameterName, getType x.Type))
                
        ProvidedMethod("Run", pars, resultType, invokeCode = function
            | this::pars ->
                let converters = pars |> List.map (fun par ->
                    let m = typeof<QueryRuntime>.GetMethod("ToNode", [| par.Type |])
                    Expr.Call(m, [par]))
                let array = Expr.NewArray(typeof<INode>, converters)
                match query.output with
                | QueryResult.Boolean ->
                    <@@ ((%%this: obj) :?> QueryRuntime).Ask(%%array) @@>
                | QueryResult.Graph ->
                    <@@ ((%%this: obj) :?> QueryRuntime).Construct(%%array) @@>
                | QueryResult.Bindings _ ->
                    <@@ ((%%this: obj) :?> QueryRuntime).Select(%%array) @@>
            | _ -> failwith "unexpected parameters")
 
    let createType typeName (sparqlQuery: string) =
        let providedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)

        let queryText =
            if sparqlQuery.EndsWith ".rq" 
            then 
                System.IO.Path.Combine(config.ResolutionFolder, sparqlQuery)
                |> System.IO.File.ReadAllText
            else sparqlQuery
        let query = queryDescriptor queryText

        createCtor query
        |> providedType.AddMember

        match query.output with
        | QueryResult.Boolean -> typeof<bool>
        | QueryResult.Graph -> typeof<IGraph>
        | QueryResult.Bindings bindings ->
            let resultType = createResultType bindings
            providedType.AddMember resultType
            resultType.MakeArrayType()
        |> createRunMethod query
        |> providedType.AddMember

        providedType

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "SparqlCommand", Some typeof<obj>)
        let par = ProvidedStaticParameter("SparqlQuery", typeof<string>)
        result.DefineStaticParameters([par], fun typeName args -> 
            createType typeName (string args.[0]))

        result.AddXmlDoc """<summary>SPARQL parametrized query.</summary>
           <param name='SparqlQuery'>Query text. Variables prefixed with '$' are treated as input parameters.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])