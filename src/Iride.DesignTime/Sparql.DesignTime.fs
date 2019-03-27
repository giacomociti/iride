module SparqlImplementation

open System
open System.Reflection
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride.SparqlHelper
open Iride
open VDS.RDF.Storage
open VDS.RDF

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

    let createType typeName sparqlQuery =
        let result = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
        let desc = getQueryDescriptor sparqlQuery
        let parNames = desc.parameterNames
        let par = ProvidedParameter("storage", typeof<IQueryableStorage>)
        let ctor = ProvidedConstructor ([par], function 
            | [storage] -> 
                <@@ QueryRuntime(%%storage, sparqlQuery, parNames) :> obj @@>
            | _ -> failwith "Expected a single parameter")
        result.AddMember ctor

        let resultType =
            match desc.resultType with
            | ResultType.Boolean -> typeof<bool>
            | ResultType.Graph -> typeof<IGraph>
            | ResultType.Bindings (variables, optionalVariables) ->
                let t = ProvidedTypeDefinition(asm, ns, "Result", Some typeof<obj>)
                variables
                |> List.map (fun v -> ProvidedProperty(v, typeof<obj>, getterCode = fun _ ->
                    <@@ "todo" :> obj @@>))
                |>  List.iter result.AddMember

                optionalVariables
                |> List.map (fun v -> ProvidedProperty(v, typeof<obj option>, getterCode = fun _ ->
                    <@@ None @@>))
                |>  List.iter result.AddMember
                
                result.AddMember t
                t.MakeArrayType()
            
        let pars = desc.parameterNames |> List.map (fun x -> ProvidedParameter(x, typeof<INode>))            
        let meth = ProvidedMethod("Run", pars, resultType, invokeCode = function
            | this::pars ->

                match desc.resultType with
                | ResultType.Boolean ->
                    // TODO Args
                    <@@ ((%%this : obj) :?> QueryRuntime).Ask( [] ) @@>
                | ResultType.Graph ->
                    <@@ ((%%this : obj) :?> QueryRuntime).Construct( [] ) @@>
                | ResultType.Bindings (vars, opts) ->
                    <@@ ((%%this : obj) :?> QueryRuntime).Select( [] ) @@>
            | _ -> failwith "unexpected parameters")
        result.AddMember meth
        
        result

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "SparqlCommand", Some typeof<obj>)
        let par = ProvidedStaticParameter("SparqlQuery", typeof<string>)
        result.DefineStaticParameters([par], fun typeName args -> 
            createType typeName (string args.[0]))

        result.AddXmlDoc """<summary>TODO.</summary>
           <param name='SparqlQuery'>SPARQL parametrized query.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])