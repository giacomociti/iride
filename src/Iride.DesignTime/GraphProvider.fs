module GraphProviderProviderImplementation

open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Iride.SparqlHelper
open VDS.RDF.Query
open VDS.RDF.Parsing
open TypeProviderHelper
open VDS.RDF
open System

[<TypeProvider>]
type GraphProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let executingAssembly = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)
        


    let createType (typeName, sample) =
        let providedAssembly = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)

        let field = ProvidedField("_graph", typeof<IGraph>)
        providedType.AddMember field

        let ctor = 
            ProvidedConstructor(
                [ProvidedParameter("graph", typeof<IGraph>)], 
                invokeCode = fun args -> 
                    match args with
                    | [this; result] ->
                      Expr.FieldSet (this, field, result)
                    | _ -> failwith "wrong ctor params")
        providedType.AddMember ctor

        let classes = 
            sample
            |> RdfHelper.getGraph config.ResolutionFolder 
            |> GraphHelper.sample2classes
            |> Seq.toArray

        let types =
            classes
            |> Array.map (fun x -> x.Name, ProvidedTypeDefinition(providedAssembly, ns, RdfHelper.getName x.Name, Some typeof<obj>, isErased=false))
            |> dict

        let literalType = function
        | "http://www.w3.org/2001/XMLSchema#integer" -> typeof<int>
        | "http://www.w3.org/2001/XMLSchema#string" -> typeof<string>
        | "http://www.w3.org/2001/XMLSchema#dateTime" -> typeof<DateTimeOffset>
        | _ -> typeof<INode> 

        let getType = function
        | GraphHelper.PropertyType.Class x -> types.[x] :> Type
        | GraphHelper.PropertyType.Literal x -> literalType x.AbsoluteUri
            
        for c in classes do
            let t = types.[c.Name]
            c.Properties
            |> Seq.map (fun p ->
                ProvidedProperty(RdfHelper.getName p.Key, (getType p.Value).MakeArrayType(), getterCode = function
                | [this] ->
                   <@@ failwith "TODO" @@>
                | _ -> failwith "Expected a single parameter"))
            |> Seq.toList
            |> t.AddMembers

        //for c in classes do 
            ProvidedProperty("Get" +  RdfHelper.getName c.Name, types.[c.Name].MakeArrayType(), getterCode = function
            | [this] ->
               <@@ Array.empty @@>
            | _ -> failwith "Expected a single parameter")
            |> providedType.AddMember

        for t in types do providedType.AddMember t.Value

        providedAssembly.AddTypes [providedType]
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphProvider", Some typeof<obj>, isErased=false)
        let sample = ProvidedStaticParameter("Sample", typeof<string>)
        
        result.DefineStaticParameters([sample], fun typeName args -> 
            createType(typeName, string args.[0]))

        result.AddXmlDoc """<summary>Sample RDF.</summary>
           <param name='Sample'>Sample RDF as turtle.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])