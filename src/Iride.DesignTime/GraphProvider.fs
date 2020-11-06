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

    let addCtorWithField (providedType: ProvidedTypeDefinition, fieldName, fieldType) =
        let field = ProvidedField("_" + fieldName, fieldType)
        providedType.AddMember field

        //TODO add public getter?

        let ctor = 
            ProvidedConstructor(
                [ProvidedParameter(fieldName, fieldType)], 
                invokeCode = fun args -> 
                    match args with
                    | [this; result] ->
                      Expr.FieldSet (this, field, result)
                    | _ -> failwith "wrong ctor params")
        providedType.AddMember ctor
        field

    let literalType = function
    | "http://www.w3.org/2001/XMLSchema#string" -> KnownDataType.Literal
    | "http://www.w3.org/2001/XMLSchema#integer" -> KnownDataType.Integer
    | "http://www.w3.org/2001/XMLSchema#date" -> KnownDataType.Date
    | "http://www.w3.org/2001/XMLSchema#dateTime" -> KnownDataType.Time
    | "http://www.w3.org/2001/XMLSchema#decimal" -> KnownDataType.Number
    | "http://www.w3.org/2001/XMLSchema#boolean" -> KnownDataType.Boolean
    | _ -> KnownDataType.Node

    let createType (typeName, sample) =
        let providedAssembly = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)

        let _graphField = addCtorWithField(providedType, "graph", typeof<IGraph>)

        let classes = 
            sample
            |> RdfHelper.getGraph config.ResolutionFolder 
            |> GraphHelper.sample2classes
            |> Seq.toArray

        let types =
            classes
            |> Array.map (fun x -> x.Name, ProvidedTypeDefinition(providedAssembly, ns, RdfHelper.getName x.Name, Some typeof<obj>, isErased=false))
            |> dict



        for c in classes do
            let t = types.[c.Name]
            let nodeField = addCtorWithField(t, "node", typeof<INode>)

            let getNodeArray this (property: Uri) =
                let node = Expr.FieldGet(this, nodeField)
                let propertyUri = property.AbsoluteUri
                <@@ 
                    let n = (%%node : INode) 
                    let p = n.Graph.GetUriNode(Uri propertyUri)
                    n.Graph.GetTriplesWithSubjectPredicate(n, p) |> Seq.map (fun x -> x.Object) |> Seq.toArray
                @@>

            c.Properties
            |> Seq.map (fun p ->
                match p.Value with
                | GraphHelper.PropertyType.Class x -> 
                    let resultElementType = types.[x] :> Type
                    ProvidedProperty(RdfHelper.getName p.Key, resultElementType.MakeArrayType(), getterCode = function
                    | [this] -> 
                        let nodes = getNodeArray this p.Key
                        let x = Var("x", typeof<INode>)
                        let ctor = resultElementType.GetConstructor([| typeof<INode> |])
                        let lambda = Expr.Lambda(x, Expr.NewObject(ctor, [Expr.Var x]))
                        <@@ Array.map (%%lambda) (%%nodes: INode[]) @@>
                    | _ -> failwith "Expected a single parameter")
                | GraphHelper.PropertyType.Literal x ->
                    let knownType = literalType x.AbsoluteUri
                    let resultElementType = TypeProviderHelper.getType knownType
                    ProvidedProperty(RdfHelper.getName p.Key, resultElementType.MakeArrayType(), getterCode = function
                    | [this] -> Expr.Call(getArrayConverterMethod knownType, [getNodeArray this p.Key])
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