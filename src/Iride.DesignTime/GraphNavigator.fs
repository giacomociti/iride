module GraphNavigatorImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Iride.Common
open TypeProviderHelper
open GraphProviderHelper
open VDS.RDF
open VDS.RDF.Parsing

let defaultQueryForSchema = """
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?t1 ?p ?t2
WHERE {
    ?p rdfs:domain ?d .
    ?t1 rdfs:subClassOf* ?d.
    OPTIONAL { ?p rdfs:range ?r }
  	BIND(COALESCE(?r, rdfs:Resource) AS ?t2)
}
"""

let defaultQueryForSample = """
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?t1 ?p ?t2
WHERE {
    ?s a ?t1 ;
        ?p ?o .
    OPTIONAL { ?o a ?r }
    BIND (COALESCE(?r, DATATYPE(?o), rdfs:Resource) AS ?t2)
}
"""

type Arguments = {
    TypeName: string
    Sample: string
    Schema: string
    SchemaQuery: string
}

let cache = Collections.Concurrent.ConcurrentDictionary<Arguments, ProvidedTypeDefinition>()

[<TypeProvider>]
type GraphNavigator (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
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

    let literalProperty (propertyUri: Uri) (propertyName: string) (propertyTypeUri: Uri) =
        let dataType = knownDataType propertyTypeUri.AbsoluteUri
        let elementType = getType dataType
        let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [elementType])
        let propertyUriText = propertyUri.AbsoluteUri
        let valuesMethodInfo = getValuesMethod dataType
        ProvidedProperty(propertyName, resultType, getterCode = function
        | [this] -> 
            Expr.Call(valuesMethodInfo, [this; Expr.Value propertyUriText])
        | _ -> failwith "Expected a single parameter")

    let objectProperty (propertyUri: Uri) (propertyName: string) (propertyType: ProvidedTypeDefinition) =
        let elementType = propertyType :> Type
        let resultType = ProvidedTypeBuilder.MakeGenericType(typedefof<seq<_>>, [elementType])
        let propertyUriText = propertyUri.AbsoluteUri
        ProvidedProperty(propertyName, resultType, getterCode = function
        | [this] -> 
            <@@ 
                let subject = %%this: Resource
                let predicate = subject.Graph.CreateUriNode(UriFactory.Create propertyUriText)
                subject.Graph.GetTriplesWithSubjectPredicate(subject.Node, predicate)
                |> Seq.map (fun x -> { Node = x.Object; Graph = subject.Graph } )
            @@>
        | _ -> failwith "Expected a single parameter")

    let createMembersForRdfClass providedType uri =
        let ctor = constructor ()
        let get = methodGet providedType uri
        [ ctor :> MemberInfo; get :> MemberInfo ]

    let createTypeForRdfClass classUri typeName comment =
        let providedType = ProvidedTypeDefinition(typeName, Some typeof<Resource>, hideObjectMethods = true)
        providedType.AddMembersDelayed (fun () -> createMembersForRdfClass providedType classUri)
        providedType.AddXmlDoc (sprintf "<summary>%s %s</summary>" classUri.AbsoluteUri comment)
        providedType

    let createSchemaReader args =
        match args.Schema, args.Sample with
        | schema, "" -> 
            let graph = GraphLoader.load config.ResolutionFolder schema
            SchemaReader(graph, if args.SchemaQuery = "" then defaultQueryForSchema else args.SchemaQuery)
        | "", sample -> 
            let graph = GraphLoader.load config.ResolutionFolder sample
            SchemaReader(graph, if args.SchemaQuery = "" then defaultQueryForSample else args.SchemaQuery)
        | _ -> failwith "Need either Schema or Sample (not both)"

    let createType args =
        let providedType = ProvidedTypeDefinition(executingAssembly, ns, args.TypeName, Some typeof<obj>, isErased=true)
        let schemaReader = createSchemaReader args
        let classes =
            schemaReader.GetClasses()
            |> Seq.map (fun x -> x.Uri, createTypeForRdfClass x.Uri x.Label (schemaReader.GetComment(x.Uri)))
            |> dict
        classes
        |> Seq.iter (fun (KeyValue (classUri, classType)) -> classType.AddMembersDelayed (fun () ->
            schemaReader.GetProperties(classUri)
            |> Seq.map (fun x ->
                let prop =
                    match classes.TryGetValue x.Range with
                    | true, classType -> objectProperty x.Uri x.Label classType
                    | _ -> literalProperty x.Uri x.Label x.Range
                prop.AddXmlDoc (sprintf "<summary>%s %s</summary>" x.Uri.AbsoluteUri (schemaReader.GetComment(x.Uri)))
                prop)
            |> Seq.toList))

        Seq.iter providedType.AddMember classes.Values
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphNavigator", Some typeof<obj>, isErased=true)
        let parameters = [
            ProvidedStaticParameter("Sample", typeof<string>, "")
            ProvidedStaticParameter("Schema", typeof<string>, "")
            ProvidedStaticParameter("SchemaQuery", typeof<string>, "")
        ]
        result.DefineStaticParameters(parameters, fun typeName args ->
            let arguments = { 
                TypeName = typeName
                Sample = string args[0]
                Schema = string args[1]
                SchemaQuery = string args[2] }
            cache.GetOrAdd(arguments, createType))

        result.AddXmlDoc """<summary>Type provider of RDF classes.</summary>
           <param name='Sample'>RDF sample (URL, file or literal).</param>
           <param name='Schema'>RDF schema (URL, file or literal).</param>
           <param name='SchemaQuery'>SPARQL query for schema.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])
