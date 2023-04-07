module GraphProviderProviderImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Name
open TypeProviderHelper
open GraphProviderHelper
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

    let makeGenericMethod args expr =
        let mi = getMethodInfo(expr).GetGenericMethodDefinition()
        ProvidedTypeBuilder.MakeGenericMethod(mi, args)

    let getInstancesMethodInfo elementType =
        <@@ CommandRuntime.GetInstances(Unchecked.defaultof<IGraph>, "", id) @@>
        |> makeGenericMethod [elementType]

    let addInstanceMethodInfo elementType =
        <@@ CommandRuntime.AddInstance(Unchecked.defaultof<IGraph>, Unchecked.defaultof<INode>, "", id) @@>
        |> makeGenericMethod [elementType]

    let addInstanceByUriMethodInfo elementType =
        <@@ CommandRuntime.AddInstance(Unchecked.defaultof<IGraph>, Unchecked.defaultof<Uri>, "", id) @@>
        |> makeGenericMethod [elementType]

    let overrideEquals (providedType: ProvidedTypeDefinition) property =
        match <@@ "".GetHashCode() @@> with
        | Patterns.Call(_, m, _) -> 
            let getHashCode = ProvidedMethod(m.Name, [], m.ReturnType, invokeCode = function
                | [this] -> 
                    let resource = Expr.PropertyGet(this, property)
                    <@@ (%%resource: Resource).GetHashCode() @@>
                | _ -> failwith "unexpected args for GetHashCode")
            getHashCode.AddMethodAttrs MethodAttributes.Virtual
            providedType.AddMember getHashCode
            providedType.DefineMethodOverride(getHashCode, m)
        | _ -> failwith "unexpected pattern"
        match <@@ this.Equals(0) @@> with
        | Patterns.Call(_, m, _) -> 
            let equals = ProvidedMethod(m.Name, [ProvidedParameter("obj", typeof<obj>)], typeof<bool>, invokeCode = function
                | [this; obj] -> 
                    let other = Expr.Coerce(obj, providedType)
                    let otherResource = Expr.PropertyGet(other, property)
                    let thisResource = Expr.PropertyGet(this, property)
                    <@@ (%%thisResource:Resource).Equals((%%otherResource:Resource)) @@>
                | _ -> failwith "unexpected args for Equals")
            equals.AddMethodAttrs MethodAttributes.Virtual
            providedType.AddMember equals
            providedType.DefineMethodOverride(equals, m)
        | _ -> failwith "unexpected pattern"

    let addConstructor (providedType: ProvidedTypeDefinition) (field: FieldInfo) =
        let parameter = ProvidedParameter(field.Name, field.FieldType)
        let ctor =
            ProvidedConstructor([parameter], invokeCode = function
            | [this; arg] -> Expr.FieldSet (this, field, arg)
            | _ -> failwith "wrong ctor params")
        providedType.AddMember ctor
        ctor

    let addField (providedType: ProvidedTypeDefinition) name =
        let field = ProvidedField(lowerInitial name, typeof<Resource>) // TODO set as private readonly
        providedType.AddMember field
        field

    let addProperty (providedType: ProvidedTypeDefinition) (field: ProvidedField) =
        let property =
            let name = upperInitial field.Name
            ProvidedProperty(name, field.FieldType, getterCode = function
            | [this] -> Expr.FieldGet(this, field)
            | _ -> failwith "wrong property params")
        providedType.AddMember property
        property

    let addGetMethod (providedType: ProvidedTypeDefinition) (classType: ClassType) ctor =
        ProvidedMethod("Get", 
            parameters = [ProvidedParameter("graph", typeof<IGraph>)], 
            returnType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [providedType]), 
            invokeCode = (function
                | [graph] -> 
                    let x = Var("x", typeof<Resource>)
                    let converter = Expr.Lambda(x, Expr.NewObject(ctor, [Expr.Var x]))
                    let getInstancesMethod = getInstancesMethodInfo providedType
                    let classUri = Expr.Value classType.Name.AbsoluteUri
                    Expr.Call(getInstancesMethod, [graph; classUri; converter])
                | _ -> failwith "wrong method params for Get"), 
            isStatic = true)
        |> providedType.AddMember

    let addAddMethod (providedType: ProvidedTypeDefinition) (classType: ClassType) ctor =
        ProvidedMethod("Add", 
             parameters = [ProvidedParameter("graph", typeof<IGraph>); ProvidedParameter("node", typeof<INode>)], 
             returnType = providedType, 
             invokeCode = (function
                 | [graph; node] -> 
                     let x = Var("x", typeof<Resource>)
                     let converter = Expr.Lambda(x, Expr.NewObject(ctor, [Expr.Var x]))
                     let addInstanceMethod = addInstanceMethodInfo providedType
                     let classUri = Expr.Value classType.Name.AbsoluteUri
                     Expr.Call(addInstanceMethod, [graph; node; classUri; converter])
                 | _ -> failwith "wrong method params for Add"), 
             isStatic = true)
         |> providedType.AddMember

    let addAddMethodByUri (providedType: ProvidedTypeDefinition) (classType: ClassType) ctor =
        ProvidedMethod("Add", 
                parameters = [ProvidedParameter("graph", typeof<IGraph>); ProvidedParameter("node", typeof<Uri>)], 
                returnType = providedType, 
                invokeCode = (function
                    | [graph; node] -> 
                        let x = Var("x", typeof<Resource>)
                        let converter = Expr.Lambda(x, Expr.NewObject(ctor, [Expr.Var x]))
                        let addInstanceMethod = addInstanceByUriMethodInfo providedType
                        let classUri = Expr.Value classType.Name.AbsoluteUri
                        Expr.Call(addInstanceMethod, [graph; node; classUri; converter])
                    | _ -> failwith "wrong method params for Add"), 
                isStatic = true)
            |> providedType.AddMember

    let createTypeForRdfClass (providedAssembly, (classType: ClassType), propertyName) =
        let typeName = getName classType.Name
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        let field = addField providedType propertyName
        let property = addProperty providedType field
        let ctor = addConstructor providedType field      
        overrideEquals providedType property
        addGetMethod providedType classType ctor
        addAddMethod providedType classType ctor
        addAddMethodByUri providedType classType ctor
        providedType

    let createType (typeName, sample, schema) =
        
        let providedAssembly = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        let resourcePropertyName = "Resource"
        let load = GraphLoader.load config.ResolutionFolder
        let types = 
            match sample, schema with
            | sample, "" -> load sample |> sample2classes 
            | "", schema -> load schema |> schema2classes
            | _ -> failwith "Need either Sample or Schema"
            |> Seq.map (fun x -> x.Name.AbsoluteUri, (x, createTypeForRdfClass(providedAssembly, x, resourcePropertyName)))
            |> dict

        let getObjectFactory (providedType: Type) =
            let r = Var("r", typeof<Resource>)
            let ctor = providedType.GetConstructor [| typeof<Resource> |]
            Expr.Lambda(r, Expr.NewObject(ctor, [Expr.Var r]))

        let getLiteralFactory knownDataType =
            let converterMethodInfo = getConverterMethod knownDataType
            let r = Var("r", typeof<Resource>)
            let e = Expr.Var r
            let n = <@@ (%%e:Resource).Node @@>
            Expr.Lambda(r, Expr.Call(converterMethodInfo, [n]))

        let nodeAccessor (providedType: Type) =
            let x = Var("x", providedType)
            let propertyInfo = providedType.GetProperty(resourcePropertyName)
            let propertyValue = Expr.PropertyGet(Expr.Var x, propertyInfo)
            Expr.Lambda(x, <@@ (%%propertyValue:Resource).Node @@>)

        let getNodeFactory elementType knownDataType =
            let nodeExtractorMethodInfo = getNodeExtractorMethod knownDataType
            let x = Var("x", elementType)
            Expr.Lambda(x, Expr.Call(nodeExtractorMethodInfo, [Expr.Var x]))

        for (classDefinition, typeDefinition) in types.Values do
            let resourceProperty = typeDefinition.GetProperty(resourcePropertyName)
            classDefinition.Properties
            |> Seq.map (fun p ->
                match p.Value with
                | PropertyType.Class classUri -> 
                    let elementType = snd types[classUri.AbsoluteUri] :> Type
                    let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<PropertyValues<_>>, [elementType])
                    let predicateUri = Expr.Value p.Key.AbsoluteUri
                    let objectFactory = getObjectFactory elementType
                    let nodeFactory = nodeAccessor elementType
                    ProvidedProperty(getName p.Key, resultType, getterCode = function
                    | [this] -> 
                        let subject = Expr.PropertyGet(this, resourceProperty)
                        let ctor = resultType.GetConstructors() |> Seq.exactlyOne
                        Expr.NewObject(ctor, [subject; predicateUri; objectFactory; nodeFactory])
                    | _ -> failwith "Expected a single parameter")
                | PropertyType.Literal knownDataType ->
                    let elementType = getType knownDataType
                    let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<PropertyValues<_>>, [elementType])
                    let predicateUri = Expr.Value p.Key.AbsoluteUri
                    let objectFactory = getLiteralFactory knownDataType
                    let nodeFactory = getNodeFactory elementType knownDataType
                    ProvidedProperty(getName p.Key, resultType, getterCode = function
                    | [this] -> 
                        let subject = Expr.PropertyGet(this, resourceProperty)
                        let ctor = resultType.GetConstructors() |> Seq.exactlyOne
                        Expr.NewObject(ctor, [subject; predicateUri; objectFactory; nodeFactory])
                    | _ -> failwith "Expected a single parameter"))
            |> Seq.toList
            |> typeDefinition.AddMembers

        for (_, t) in types.Values do providedType.AddMember t

        providedAssembly.AddTypes [providedType]
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphProvider", Some typeof<obj>, isErased=false)
        let sample = ProvidedStaticParameter("Sample", typeof<string>, "")
        let schema = ProvidedStaticParameter("Schema", typeof<string>, "")
        
        result.DefineStaticParameters([sample;schema], fun typeName args -> 
            createType(typeName, string args[0], string args[1]))

        result.AddXmlDoc """<summary>Type provider of RDF classes.</summary>
           <param name='Sample'>RDF Sample as Turtle.</param>
           <param name='Schema'>RDF Schema as Turtle.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])