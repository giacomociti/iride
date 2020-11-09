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

    let addCtorWithField (providedType: ProvidedTypeDefinition, fieldName, fieldType) =
        let parameter = ProvidedParameter(fieldName, fieldType)
        let field = ProvidedField("_" + fieldName, fieldType)
        // TODO set as private readonly
        providedType.AddMember field
        //TODO add public getter?
        ProvidedConstructor([parameter], invokeCode = function
            | [this; arg] ->
                Expr.FieldSet (this, field, arg)
            | _ -> failwith "wrong ctor params")
        |> providedType.AddMember 
        

    let literalType = function
        | "http://www.w3.org/2001/XMLSchema#string" -> KnownDataType.Literal
        | "http://www.w3.org/2001/XMLSchema#integer" -> KnownDataType.Integer
        | "http://www.w3.org/2001/XMLSchema#date" -> KnownDataType.Date
        | "http://www.w3.org/2001/XMLSchema#dateTime" -> KnownDataType.Time
        | "http://www.w3.org/2001/XMLSchema#decimal" -> KnownDataType.Number
        | "http://www.w3.org/2001/XMLSchema#boolean" -> KnownDataType.Boolean
        | _ -> KnownDataType.Node

    let getValuesMethodInfo elementType =
        let methodInfo = TypeProviderHelper.getMethodInfo <@@ CommandRuntime.GetValues(Unchecked.defaultof<INode>, "", id) @@>
        ProvidedTypeBuilder.MakeGenericMethod(methodInfo.GetGenericMethodDefinition(), [elementType])

    let createTypeWithField (providedAssembly, typeName, fieldName, fieldType) =
        let result = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        addCtorWithField (result, fieldName, fieldType)
        result

    let createType (typeName, sample) =
        let providedAssembly = ProvidedAssembly()
        let providedType = createTypeWithField(providedAssembly, typeName, "graph", typeof<IGraph>)

        let types = 
            sample
            |> RdfHelper.getGraph config.ResolutionFolder 
            |> GraphHelper.sample2classes
            |> Seq.map (fun x -> x.Name, (x, createTypeWithField(providedAssembly, RdfHelper.getName x.Name, "node", typeof<INode>)))
            |> dict

        for entry in types do
            let classDefinition = fst entry.Value
            let typeDefinition = snd entry.Value
            let nodeField = typeDefinition.GetField("_node", BindingFlags.NonPublic ||| BindingFlags.Instance)

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
                        let subject = Expr.FieldGet(this, nodeField)
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
                        let subject = Expr.FieldGet(this, nodeField)
                        Expr.Call(getValuesMethod, [subject; predicateUri; objectConverter])
                    | _ -> failwith "Expected a single parameter"))
            |> Seq.toList
            |> typeDefinition.AddMembers

        for t in types do providedType.AddMember (snd t.Value)

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