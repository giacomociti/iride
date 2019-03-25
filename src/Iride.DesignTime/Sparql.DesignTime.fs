module SparqlImplementation

open System
open System.Reflection
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride.SparqlHelper
open Iride
open VDS.RDF.Storage

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
    //do assert (typeof<Iride.IMarker>.Assembly.GetName().Name = asm.GetName().Name)   

    let createType typeName sparqlQuery =
        let asm = ProvidedAssembly()
        let result = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=true)
        
        let desc = getQueryDescriptor sparqlQuery

        let par = ProvidedParameter("storage", typeof<IQueryableStorage>)
        let ctor = ProvidedConstructor ([par], function 
            | [storage] -> <@@ QueryRuntime(%%storage, sparqlQuery, desc.parameterNames) :> obj @@>
            | _ -> failwith "Expected a single parameter")

        result.AddMember ctor

        asm.AddTypes [ result ]
        result

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "SparqlCommand", Some typeof<obj>, isErased=true)
        let par = ProvidedStaticParameter("SparqlQuery", typeof<string>)
        result.DefineStaticParameters([par], fun typeName args -> 
            createType typeName (string args.[0]))

        result.AddXmlDoc """<summary>TODO.</summary>
           <param name='SparqlQuery'>SPARQL parametrized query.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])
