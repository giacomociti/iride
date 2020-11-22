module GraphProviderProviderImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Iride.SparqlHelper
open TypeProviderHelper
open VDS.RDF

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

    let literalType = function
        | "http://www.w3.org/2001/XMLSchema#string" -> KnownDataType.Literal
        | "http://www.w3.org/2001/XMLSchema#integer" -> KnownDataType.Integer
        | "http://www.w3.org/2001/XMLSchema#date" -> KnownDataType.Date
        | "http://www.w3.org/2001/XMLSchema#dateTime" -> KnownDataType.Time
        | "http://www.w3.org/2001/XMLSchema#decimal" -> KnownDataType.Number
        | "http://www.w3.org/2001/XMLSchema#boolean" -> KnownDataType.Boolean
        | _ -> KnownDataType.Node

    let makeGenericMethod args expr =
        let mi = TypeProviderHelper.getMethodInfo(expr).GetGenericMethodDefinition()
        ProvidedTypeBuilder.MakeGenericMethod(mi, args)

    let getValuesMethodInfo elementType =
        <@@ CommandRuntime.GetValues(Unchecked.defaultof<INode>, "", id) @@>
        |> makeGenericMethod [elementType]

    let getInstancesMethodInfo elementType =
        <@@ CommandRuntime.GetInstances(Unchecked.defaultof<IGraph>, "", id) @@>
        |> makeGenericMethod [elementType]


    let createTypeForRdfClass (providedAssembly, (classType: GraphHelper.ClassType), propertyName) =
        let typeName = RdfHelper.getName classType.Name
        let propertyType = typeof<INode>
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        let parameter = ProvidedParameter(RdfHelper.lowerInitial propertyName, propertyType)
        let field = ProvidedField("_" + parameter.Name, propertyType) // TODO set as private readonly
        providedType.AddMember field

        ProvidedProperty(propertyName, propertyType, getterCode = function
            | [this] -> Expr.FieldGet(this, field)
            | _ -> failwith "wrong property params")
        |> providedType.AddMember

        ProvidedConstructor([parameter], invokeCode = function
            | [this; arg] ->
                Expr.FieldSet (this, field, arg)
            | _ -> failwith "wrong ctor params")
        |> providedType.AddMember 

        ProvidedMethod("Get", 
            parameters = [ProvidedParameter("graph", typeof<IGraph>)], 
            returnType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [providedType]), 
            invokeCode = (function
                | [graph] -> 
                    let x = Var("x", typeof<INode>)
                    let ctor = providedType.GetConstructor([| typeof<INode> |])
                    let converter = Expr.Lambda(x, Expr.NewObject(ctor, [Expr.Var x]))
                    let getInstancesMethod = getInstancesMethodInfo providedType
                    let classUri = Expr.Value classType.Name.AbsoluteUri
                    Expr.Call(getInstancesMethod, [graph; classUri; converter])
                | _ -> failwith "wrong method params"), 
            isStatic = true)
        |> providedType.AddMember
        providedType

    let createType (typeName, sample, schema) =
        
        let providedAssembly = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        let nodePropertyName = "Node"
        let f = RdfHelper.getGraph config.ResolutionFolder 
        let types = 
            match sample, schema with
            | sample, "" -> f sample |> GraphHelper.sample2classes 
            | "", schema -> f schema |> GraphHelper.schema2classes
            | _ -> failwith "Need either Sample or Schema"
            |> Seq.map (fun x -> x.Name, (x, createTypeForRdfClass(providedAssembly, x, nodePropertyName)))
            |> dict

        for entry in types do
            let classDefinition = fst entry.Value
            let typeDefinition = snd entry.Value
            let nodeProperty = typeDefinition.GetProperty(nodePropertyName)

            classDefinition.Properties
            |> Seq.map (fun p ->
                match p.Value with
                | GraphHelper.PropertyType.Class x -> 
                    let elementType = snd types.[x] :> Type
                    let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [elementType])
                    let getValuesMethod = getValuesMethodInfo elementType
                    let predicateUri = Expr.Value p.Key.AbsoluteUri
                    let x = Var("x", typeof<INode>)
                    let ctor = elementType.GetConstructor([| typeof<INode> |])
                    let objectConverter = Expr.Lambda(x, Expr.NewObject(ctor, [Expr.Var x]))
                    ProvidedProperty(RdfHelper.getName p.Key, resultType, getterCode = function
                    | [this] -> 
                        let subject = Expr.PropertyGet(this, nodeProperty)
                        Expr.Call(getValuesMethod, [subject; predicateUri; objectConverter])
                    | _ -> failwith "Expected a single parameter")
                | GraphHelper.PropertyType.Literal x ->
                    let knownDataType = literalType x.AbsoluteUri
                    let elementType = TypeProviderHelper.getType knownDataType
                    let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [elementType])
                    let getValuesMethod = getValuesMethodInfo elementType
                    let predicateUri = Expr.Value p.Key.AbsoluteUri
                    let converterMethodInfo = getConverterMethod knownDataType
                    let x = Var("x", typeof<INode>)
                    let objectConverter = Expr.Lambda(x, Expr.Call(converterMethodInfo, [Expr.Var x]))
                    ProvidedProperty(RdfHelper.getName p.Key, resultType, getterCode = function
                    | [this] -> 
                        let subject = Expr.PropertyGet(this, nodeProperty)
                        Expr.Call(getValuesMethod, [subject; predicateUri; objectConverter])
                    | _ -> failwith "Expected a single parameter"))
            |> Seq.toList
            |> typeDefinition.AddMembers

        for t in types do providedType.AddMember (snd t.Value)

        providedAssembly.AddTypes [providedType]
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphProvider", Some typeof<obj>, isErased=false)
        let sample = ProvidedStaticParameter("Sample", typeof<string>, "")
        let schema = ProvidedStaticParameter("Schema", typeof<string>, "")
        
        result.DefineStaticParameters([sample;schema], fun typeName args -> 
            createType(typeName, string args.[0], string args.[1]))

        result.AddXmlDoc """<summary>Sample RDF.</summary>
           <param name='Sample'>Sample RDF as turtle.</param>
           <param name='Schema'>RDFS schema as turtle.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])