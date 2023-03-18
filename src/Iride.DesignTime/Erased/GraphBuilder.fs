// module ErasedGraphProviderImplementation

// open System
// open System.Reflection
// open Microsoft.FSharp.Quotations
// open FSharp.Core.CompilerServices
// open ProviderImplementation.ProvidedTypes
// open Iride
// open Common
// open TypeProviderHelper
// open GraphProviderHelper
// open VDS.RDF
// open VDS.RDF.Parsing
// open VDS.RDF.Query


// type Arguments = {
//     TypeName: string
//     Sample: string
//     Schema: string
//     ClassQuery: string
//     PropertyQuery: string
// }

// let cache = Collections.Concurrent.ConcurrentDictionary<Arguments, ProvidedTypeDefinition>()

// [<TypeProvider>]
// type GraphBuilder (config : TypeProviderConfig) as this =
//     inherit TypeProviderForNamespaces 
//         (config, 
//          assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
//          addDefaultProbingLocation = true)
//     let ns = "Iride.Erased"
//     let executingAssembly = Assembly.GetExecutingAssembly()

//     // check we contain a copy of runtime files, and are not referencing the runtime DLL
//     do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)

//     let constructor () =
//         let parameter = ProvidedParameter("resource", typeof<Resource>)
//         ProvidedConstructor([parameter], invokeCode = function
//         | [res] -> <@@ (%%res:Resource) @@>
//         | _ -> failwith "wrong ctor params")

//     let methodGet (providedType: ProvidedTypeDefinition) (classType: Uri) =
//         ProvidedMethod("Get", 
//             parameters = [ProvidedParameter("graph", typeof<IGraph>)], 
//             returnType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [providedType]), 
//             invokeCode = (function
//                 | [graph] -> 
//                 let classUri = classType.AbsoluteUri
//                 <@@
//                     let g = %%graph :> IGraph
//                     let typeNode = g.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
//                     let classNode = g.CreateUriNode(UriFactory.Create classUri)
//                     g.GetTriplesWithPredicateObject(typeNode, classNode)
//                     |> Seq.map (fun t -> { Node = t.Subject; Graph = g })
//                 @@>
//                 | _ -> failwith "wrong method params for Get"), 
//             isStatic = true)

//     let methodAdd (providedType: ProvidedTypeDefinition) (classType: Uri) =
//         ProvidedMethod("Add", 
//             parameters = [ProvidedParameter("graph", typeof<IGraph>); ProvidedParameter("node", typeof<INode>)], 
//             returnType = providedType, 
//             invokeCode = (function
//                 | [graph; node] -> 
//                     let classUri = classType.AbsoluteUri
//                     <@@
//                         let g = %%graph :> IGraph
//                         let n = %%node :> INode
//                         let typeNode = g.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
//                         let classNode = g.CreateUriNode(UriFactory.Create classUri)
//                         g.Assert(n, typeNode, classNode)
//                         { Node = n; Graph = g }
//                     @@>
//                 | _ -> failwith "wrong method params for Add"), 
//             isStatic = true)

//     let methodAddByUri (providedType: ProvidedTypeDefinition) (classType: Uri) =
//         ProvidedMethod("Add", 
//             parameters = [ProvidedParameter("graph", typeof<IGraph>); ProvidedParameter("uri", typeof<Uri>)], 
//             returnType = providedType, 
//             invokeCode = (function
//                 | [graph; uri] -> 
//                     let classUri = classType.AbsoluteUri
//                     <@@
//                         let g = %%graph :> IGraph
//                         let u = %%uri :> Uri
//                         let n = g.CreateUriNode(u)
//                         let typeNode = g.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
//                         let classNode = g.CreateUriNode(UriFactory.Create classUri)
//                         g.Assert(n, typeNode, classNode)
//                         { Node = n; Graph = g }
//                     @@>
//                 | _ -> failwith "wrong method params for Add"), 
//             isStatic = true)

//     // let getLiteralFactory knownDataType =
//     //     let converterMethodInfo = getConverterMethod knownDataType
//     //     let r = Var("r", typeof<Resource>)
//     //     let e = Expr.Var r
//     //     let n = <@@ (%%e:Resource).Node @@>
//     //     Expr.Lambda(r, Expr.Call(converterMethodInfo, [n]))

//     // let getNodeFactory elementType knownDataType =
//     //     let nodeExtractorMethodInfo = getNodeExtractorMethod knownDataType
//     //     let x = Var("x", elementType)
//     //     Expr.Lambda(x, Expr.Call(nodeExtractorMethodInfo, [Expr.Var x]))


//     let literalProperty (propertyUri: Uri) (propertyTypeUri: Uri) =
//         let dataType = knownDataType propertyTypeUri.AbsoluteUri
//         let elementType = getType dataType
//         let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [elementType])
//         let propertyUriText = propertyUri.AbsoluteUri
//         let valuesMethodInfo = getValuesMethod dataType
//         ProvidedProperty(getName propertyUri, resultType, getterCode = function
//         | [this] -> 
//             Expr.Call(valuesMethodInfo, [this; Expr.Value propertyUriText])
//         | _ -> failwith "Expected a single parameter")

//     let objectProperty (propertyUri: Uri) (propertyType: ProvidedTypeDefinition) =
//         let elementType = propertyType :> Type
//         let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [elementType])
//         let propertyUriText = propertyUri.AbsoluteUri
//         ProvidedProperty(getName propertyUri, resultType, getterCode = function
//         | [this] -> 
//             <@@ 
//                 let subject = %%this: Resource
//                 let predicate = subject.Graph.CreateUriNode(UriFactory.Create propertyUriText)
//                 subject.Graph.GetTriplesWithSubjectPredicate(subject.Node, predicate)
//                 |> Seq.map (fun x -> { Node = x.Object; Graph = subject.Graph } )
//             @@>
//         | _ -> failwith "Expected a single parameter")

//     let createMembersForRdfClass providedType uri =
//         let ctor = constructor ()
//         let get = methodGet providedType uri
//         let add = methodAdd providedType uri
//         let addByUri = methodAddByUri providedType uri
//         [ ctor :> MemberInfo; add :> MemberInfo; get :> MemberInfo; addByUri :> MemberInfo ]

//     let createTypeForRdfClass classUri label comment =
//         let typeName = getName classUri
//         let providedType = ProvidedTypeDefinition(typeName, Some typeof<Resource>, hideObjectMethods = true)
//         providedType.AddXmlDoc (sprintf "<summary>%s %s %s</summary>" label classUri.AbsoluteUri comment)
//         providedType.AddMembersDelayed (fun () -> createMembersForRdfClass providedType classUri)
//         providedType

//     let createSchemaReader args =
//         match args.Schema, args.Sample with
//         | schema, "" -> 
//             let graph = GraphLoader.load config.ResolutionFolder schema
//             SchemaReader(graph, args.ClassQuery, args.PropertyQuery)
//         | "", sample -> 
//             let graph = GraphLoader.load config.ResolutionFolder sample
//             let classQuery = if args.ClassQuery = "" then classesFromSampleQuery else args.ClassQuery
//             let propertyQuery = if args.PropertyQuery = "" then propertiesFromSampleQuery else args.PropertyQuery
//             SchemaReader(graph, classQuery, propertyQuery)
//         | _ -> failwith "Need either Schema or Sample (not both)"

//     let createType args =
//         let providedType = ProvidedTypeDefinition(executingAssembly, ns, args.TypeName, Some typeof<obj>, isErased=true)
//         let schemaReader = createSchemaReader args
//         let classes =
//             schemaReader.GetClasses()
//             |> Seq.map (fun x -> x.Uri, createTypeForRdfClass x.Uri x.Label x.Comment)
//             |> dict
//         classes
//         |> Seq.iter (fun (KeyValue (classUri, classType)) -> classType.AddMembersDelayed (fun () ->
//             schemaReader.GetProperties(classUri)
//             |> Seq.map (fun x ->
//                 let prop =
//                     match classes.TryGetValue x.Range with
//                     | true, classType -> objectProperty x.Uri classType
//                     | _ -> literalProperty x.Uri x.Range
//                 prop.AddXmlDoc (sprintf "<summary>%s %s %s</summary>" x.Label x.Uri.AbsoluteUri x.Comment)
//                 prop)

//             |> Seq.toList))

//         Seq.iter providedType.AddMember classes.Values
//         providedType

//     let providerType = 
//         let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphBuilder", Some typeof<obj>, isErased=true)
//         let parameters = [
//             ProvidedStaticParameter("Sample", typeof<string>, "")
//             ProvidedStaticParameter("Schema", typeof<string>, "")
//             ProvidedStaticParameter("ClassQuery", typeof<string>, "")
//             ProvidedStaticParameter("PropertyQuery", typeof<string>, "")
//         ]
//         result.DefineStaticParameters(parameters, fun typeName args ->
//             let arguments = { 
//                 TypeName = typeName
//                 Sample = string args[0]
//                 Schema = string args[1]
//                 ClassQuery = string args[2]
//                 PropertyQuery = string args[3] }
//             cache.GetOrAdd(arguments, createType))

//         result.AddXmlDoc """<summary>Type provider of RDF classes.</summary>
//            <param name='Sample'>RDF sample (URL, file or literal).</param>
//            <param name='Schema'>RDF schema (URL, file or literal).</param>
//            <param name='ClassQuery'>SPARQL query for classes.</param>
//            <param name='PropertyQuery'>SPARQL query for properties.</param>
//          """
//         result

//     do this.AddNamespace(ns, [providerType])
