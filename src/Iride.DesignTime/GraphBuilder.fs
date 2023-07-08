module GraphBuilderImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Common
open TypeProviderHelper
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
type GraphBuilder (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let executingAssembly = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)

    let constructor (uri: Uri) =
        let parameter = ProvidedParameter("resource", typeof<Resource>)
        let classUri = uri.AbsoluteUri
        ProvidedConstructor([parameter], invokeCode = function
        | [res] -> 
        <@@ 
            let resource = %%res:Resource
            let typeNode = resource.Graph.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
            let classNode = resource.Graph.CreateUriNode(UriFactory.Create classUri)
            resource.Graph.Assert(resource.Node, typeNode, classNode)
            resource
        @@>
        | _ -> failwith "wrong ctor params")

    let createTypeForRdfClass (classUri: Uri) typeName comment =
        let providedType = ProvidedTypeDefinition(typeName, Some typeof<Resource>, hideObjectMethods = true)
        providedType.AddMembersDelayed (fun () -> [ constructor classUri ])
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
            |> Seq.map (fun x -> x.Uri.AbsoluteUri, createTypeForRdfClass x.Uri x.Label (schemaReader.GetComment(x.Uri)))
            |> dict // use string as key, because Uri equality is too loose
        classes
        |> Seq.iter (fun (KeyValue (classUri, classType)) -> classType.AddMembersDelayed (fun () ->
            schemaReader.GetProperties(classUri)
            |> Seq.map (fun x ->
                let prop =
                    match classes.TryGetValue x.Range.AbsoluteUri with
                    | true, objectType -> 
                        let predicateUri = x.Uri.AbsoluteUri
                        ProvidedMethod(x.Label,
                            parameters = [ProvidedParameter("value", objectType)], 
                            returnType = classType, 
                            invokeCode = (function
                                | [this; value] ->                                 
                                <@@
                                    let resource = %%this : Resource
                                    let other = %%value : Resource
                                    let predicate = resource.Graph.CreateUriNode(UriFactory.Create predicateUri)
                                    resource.Graph.Assert(resource.Node, predicate, other.Node)
                                    resource
                                @@>
                                | _ -> failwith "wrong method params") )
                    | _ -> 
                        let dataType = knownDataType x.Range.AbsoluteUri
                        let elementType = getType dataType
                        let predicateUri = x.Uri.AbsoluteUri
                        let nodeExtractorMethodInfo = getNodeExtractorMethod dataType
                        ProvidedMethod(x.Label,
                            parameters = [ProvidedParameter("value", elementType)], 
                            returnType = classType, 
                            invokeCode = (function
                                | [this; value] -> 
                                let literal = Expr.Call(nodeExtractorMethodInfo, [value])
                                <@@
                                    let resource = %%this : Resource
                                    let predicate = resource.Graph.CreateUriNode(UriFactory.Create predicateUri)
                                    resource.Graph.Assert(resource.Node, predicate, %%literal)
                                    resource
                                @@>
                                | _ -> failwith "wrong method params") )

                prop.AddXmlDoc (sprintf "<summary>%s %s</summary>" x.Uri.AbsoluteUri (schemaReader.GetComment(x.Uri)))
                prop)
            |> Seq.toList))

        Seq.iter providedType.AddMember classes.Values
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "GraphBuilder", Some typeof<obj>, isErased=true)
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
