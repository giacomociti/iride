module ErasedGraphProviderImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Common
open TypeProviderHelper
open GraphProviderHelper
open VDS.RDF
open VDS.RDF.Parsing
open VDS.RDF.Query

type Arguments = {
    TypeName: string
    Source: string
    ClassQuery: string
    PropertyQuery: string
}

let cache = Collections.Concurrent.ConcurrentDictionary<Arguments, ProvidedTypeDefinition>()

[<TypeProvider>]
type GraphProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride.Erased"
    let executingAssembly = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)

    let constructor () =
        let parameter = ProvidedParameter("resource", typeof<Resource>)
        ProvidedConstructor([parameter], invokeCode = function
        | [res] -> <@@ (%%res:Resource) @@>
        | _ -> failwith "wrong ctor params")

    let methodGet (providedType: ProvidedTypeDefinition) (classType: Uri) =
        ProvidedMethod("Get", 
            parameters = [ProvidedParameter("graph", typeof<IGraph>)], 
            returnType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [providedType]), 
            invokeCode = (function
                | [graph] -> 
                let classUri = classType.AbsoluteUri
                <@@
                    let g = %%graph :> IGraph
                    let typeNode = g.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
                    let classNode = g.CreateUriNode(UriFactory.Create classUri)
                    g.GetTriplesWithPredicateObject(typeNode, classNode)
                    |> Seq.map (fun t -> { Node = t.Subject; Graph = g })
                @@>
                | _ -> failwith "wrong method params for Get"), 
            isStatic = true)

    let methodAdd (providedType: ProvidedTypeDefinition) (classType: Uri) =
        ProvidedMethod("Add", 
            parameters = [ProvidedParameter("graph", typeof<IGraph>); ProvidedParameter("node", typeof<INode>)], 
            returnType = providedType, 
            invokeCode = (function
                | [graph; node] -> 
                    let classUri = classType.AbsoluteUri
                    <@@
                        let g = %%graph :> IGraph
                        let n = %%node :> INode
                        let typeNode = g.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
                        let classNode = g.CreateUriNode(UriFactory.Create classUri)
                        g.Assert(n, typeNode, classNode)
                        { Node = n; Graph = g }
                    @@>
                | _ -> failwith "wrong method params for Add"), 
            isStatic = true)

    let methodAddByUri (providedType: ProvidedTypeDefinition) (classType: Uri) =
        ProvidedMethod("Add", 
            parameters = [ProvidedParameter("graph", typeof<IGraph>); ProvidedParameter("uri", typeof<Uri>)], 
            returnType = providedType, 
            invokeCode = (function
                | [graph; uri] -> 
                    let classUri = classType.AbsoluteUri
                    <@@
                        let g = %%graph :> IGraph
                        let u = %%uri :> Uri
                        let n = g.CreateUriNode(u)
                        let typeNode = g.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
                        let classNode = g.CreateUriNode(UriFactory.Create classUri)
                        g.Assert(n, typeNode, classNode)
                        { Node = n; Graph = g }
                    @@>
                | _ -> failwith "wrong method params for Add"), 
            isStatic = true)

    let getResources (query: string) (graph: IGraph) =
        let results = graph.ExecuteQuery query
        [
            for r in results :?> SparqlResultSet do
                let uri = (r.["uri"] :?> IUriNode).Uri
                let label =
                    match r.TryGetBoundValue "label" with
                    | true, x -> (x :?> ILiteralNode).Value
                    | false, _ -> getName uri
                let comment =
                    match r.TryGetBoundValue "comment" with
                    | true, x -> (x :?> ILiteralNode).Value
                    | false, _ -> uri.ToString()

                yield {| Uri = uri; Label = label; Comment = comment |}
        ]

    let tryUri source =
        try Some (Uri source)
        with _ -> None

    let tryFile resolutionFolder source =
        [ source; IO.Path.Combine(resolutionFolder, source)]
        |> List.tryFind IO.File.Exists

    let loadGraph (graph: Graph, uri: Uri) =
        let ext = IO.Path.GetExtension(uri.AbsoluteUri).TrimStart('.')
        match MimeTypesHelper.GetDefinitionsByFileExtension(ext) |> Seq.toList with
        | mimeType::_ ->
            if mimeType.CanParseRdf 
            then graph.LoadFromUri(uri, mimeType.GetRdfParser())
            elif mimeType.CanParseRdfDatasets
            then
                let ts = new TripleStore()
                ts.LoadFromUri(uri, mimeType.GetRdfDatasetParser())
                ts.Graphs |> Seq.iter graph.Merge
            else graph.LoadFromUri(uri)
        | _ -> graph.LoadFromUri(uri)

    let sourceGraph resolutionFolder source =
        let graph = new Graph()
        match tryUri source with
        | Some uri ->
            loadGraph(graph, uri)
        | None ->
            match tryFile resolutionFolder source with
            | Some file -> FileLoader.Load(graph, file)
            | None -> StringParser.Parse(graph, source)
        graph

    let getLiteralFactory knownDataType =
        let converterMethodInfo = getConverterMethod knownDataType
        let r = Var("r", typeof<Resource>)
        let e = Expr.Var r
        let n = <@@ (%%e:Resource).Node @@>
        Expr.Lambda(r, Expr.Call(converterMethodInfo, [n]))

    let getNodeFactory elementType knownDataType =
        let nodeExtractorMethodInfo = getNodeExtractorMethod knownDataType
        let x = Var("x", elementType)
        Expr.Lambda(x, Expr.Call(nodeExtractorMethodInfo, [Expr.Var x]))

    let getObjectFactory (providedType: Type) =
        let r = Var("r", typeof<Resource>)
        let e = Expr.Var r
        let ctor = providedType.GetConstructor [| typeof<Resource> |]
        Expr.Lambda(r, Expr.NewObject(ctor, [e]))

    let defaultClassQuery = """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE { 
            ?uri a rdfs:Class
            OPTIONAL { ?uri rdfs:label ?label }
            OPTIONAL { ?uri rdfs:comment ?comment }
        }
        """

    let defaultPropertyQuery = """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
        PREFIX schema: <https://schema.org/>

        SELECT ?property ?range WHERE {
        ?property a rdf:Property ;
            schema:domainIncludes @domain ;
            schema:rangeIncludes ?range ;
        }
        """

    let literalProperty (propertyUri: Uri) (propertyTypeUri: Uri) =
        let dataType = knownDataType propertyTypeUri.AbsoluteUri
        let elementType = getType dataType
        let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<PropertyValues<_>>, [elementType])
        let predicateUri = Expr.Value propertyTypeUri.AbsoluteUri
        let objectFactory = getLiteralFactory dataType
        let nodeFactory = getNodeFactory elementType dataType
        ProvidedProperty(getName propertyUri, resultType, getterCode = function
        | [this] -> 
            let ctor = resultType.GetConstructors() |> Seq.exactlyOne
            Expr.NewObject(ctor, [this; predicateUri; objectFactory; nodeFactory])
        | _ -> failwith "Expected a single parameter")

    let objectProperty (propertyUri: Uri) (propertyTypeUri: Uri) (propertyType: ProvidedTypeDefinition) =
        let elementType = propertyType :> Type
        let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<PropertyValues<_>>, [elementType])
        let predicateUri = Expr.Value propertyTypeUri.AbsoluteUri
        let objectFactory = getObjectFactory elementType
        let nodeFactory = <@@ unbox @@>
        ProvidedProperty(getName propertyUri, resultType, getterCode = function
        | [this] -> 
            let ctor = resultType.GetConstructors() |> Seq.exactlyOne
            Expr.NewObject(ctor, [this; predicateUri; objectFactory; nodeFactory])
        | _ -> failwith "Expected a single parameter")

    let prop (graph: IGraph) (classes: Collections.Generic.IDictionary<Uri, ProvidedTypeDefinition>) (propertyQuery: SparqlParameterizedString) (classType:Uri) () =
        propertyQuery.SetUri("domain", classType)
        graph.ExecuteQuery(propertyQuery) :?> SparqlResultSet
        |> Seq.map (fun x -> x["property"].Uri, x["range"].Uri)
        |> Seq.groupBy fst
        |> Seq.map (fun (property, ranges) ->
            match Seq.toList ranges with 
            | [(_, r)] -> 
                if classes.ContainsKey r
                then objectProperty property r (classes[r])
                else literalProperty property r 
            | _ -> literalProperty property (Uri "http://dummy.org")) // fallback to Node
        |> Seq.toList

    let createMembersForRdfClass providedType uri =
        let ctor = constructor ()
        let get = methodGet providedType uri
        let add = methodAdd providedType uri
        let addByUri = methodAddByUri providedType uri
        [ ctor :> MemberInfo; add :> MemberInfo; get :> MemberInfo; addByUri :> MemberInfo ]

    let createTypeForRdfClass uri label comment =
        let typeName = getName uri
        let providedType = ProvidedTypeDefinition(typeName, Some typeof<Resource>, hideObjectMethods = true)
        providedType.AddMembersDelayed (fun () -> createMembersForRdfClass providedType uri)
        providedType.AddXmlDocDelayed (fun () -> sprintf "%s %s %s" label uri.AbsoluteUri comment)
        providedType

    let createType (args: Arguments) =
        let providedType = ProvidedTypeDefinition(executingAssembly, ns, args.TypeName, Some typeof<obj>, isErased=true)
        let graph = sourceGraph config.ResolutionFolder args.Source
        let classes =
            getResources args.ClassQuery graph
            |> List.map (fun x -> x.Uri, createTypeForRdfClass x.Uri x.Label x.Comment)
            |> dict
        let propQuery = SparqlParameterizedString args.PropertyQuery
        classes
        |> Seq.iter (fun (KeyValue (classUri, classType)) -> classType.AddMembersDelayed (prop graph classes propQuery classUri))
        classes.Values
        |> Seq.iter providedType.AddMember
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphProvider", Some typeof<obj>, isErased=true)
        let parameters = [
            ProvidedStaticParameter("Source", typeof<string>, "")
            ProvidedStaticParameter("ClassQuery", typeof<string>, defaultClassQuery)
            ProvidedStaticParameter("PropertyQuery", typeof<string>, defaultPropertyQuery)
        ]
        result.DefineStaticParameters(parameters, fun typeName args ->
            let arguments = { 
                TypeName = typeName
                Source = string args[0]
                ClassQuery = string args[1]
                PropertyQuery = string args[2] }
            cache.GetOrAdd(arguments, createType))

        result.AddXmlDoc """<summary>Type provider of RDF classes.</summary>
           <param name='Source'>RDF schema or sample (URL, file or text).</param>
           <param name='ClassQuery'>SPARQL query for classes.</param>
           <param name='PropertyQuery'>SPARQL query for properties.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])