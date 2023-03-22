namespace Iride

open VDS.RDF
open VDS.RDF.Query
open System.Xml
open VDS.RDF.Parsing

type Resource = { Node: INode; Graph: IGraph }

type CommandRuntime =
    static member NodeFactory = NodeFactory(NodeFactoryOptions())

    static member GetCmdText(commandText: string, parameterNames: string list, parameterValues: INode array) =
        let sps = SparqlParameterizedString commandText
        Seq.zip parameterNames parameterValues
        |> Seq.iter sps.SetVariable
        sps.ToString()

    static member ToNode(n: INode) = n
    static member ToNode(u: System.Uri) = CommandRuntime.NodeFactory.CreateUriNode(u) :> INode
    static member ToNode(s: string) = CommandRuntime.NodeFactory.CreateLiteralNode(s) :> INode
    static member ToNode(n: int) = n.ToLiteral(CommandRuntime.NodeFactory) :> INode
    static member ToNode(d: decimal) = d.ToLiteral(CommandRuntime.NodeFactory) :> INode
    static member ToNode(t: System.DateTime) : INode = CommandRuntime.NodeFactory.CreateLiteralNode(t.ToString("yyyy-MM-dd")) :> INode
    static member ToNode(t: System.DateTimeOffset) : INode = t.ToLiteral(CommandRuntime.NodeFactory) :> INode
    static member ToNode(t: bool) : INode = t.ToLiteral(CommandRuntime.NodeFactory) :> INode

    static member AsNode(n: INode) = n
    static member AsUri(n: INode) = (n :?> IUriNode).Uri
    static member AsString(n: INode) = (n :?> ILiteralNode).Value
    static member AsInt(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToInt32
    static member AsDecimal(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToDecimal
    static member AsDateTime(n: INode) = XmlConvert.ToDateTime((n :?> ILiteralNode).Value, "yyyy-MM-dd")
    static member AsDateTimeOffset(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToDateTimeOffset
    static member AsBoolean(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToBoolean

    static member GetValues(r: Resource, propertyUriText) =
        let predicate = r.Graph.CreateUriNode(UriFactory.Create propertyUriText)
        r.Graph.GetTriplesWithSubjectPredicate(r.Node, predicate)
        |> Seq.map (fun x -> x.Object )
    static member GetUriValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsUri
    static member GetStringValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsString
    static member GetIntValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsInt
    static member GetDecimalValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsDecimal
    static member GetDateTimeValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsDateTime
    static member GetDateTimeOffsetValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsDateTimeOffset
    static member GetBooleanValues(r: Resource, propertyUriText) =
        CommandRuntime.GetValues(r, propertyUriText) 
        |> Seq.map CommandRuntime.AsBoolean

    static member GetInstances(graph: IGraph, classUri: string, factory) =
        let typeNode = graph.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
        let classNode = graph.CreateUriNode(UriFactory.Create classUri)
        graph.GetTriplesWithPredicateObject(typeNode, classNode)
        |> Seq.map (fun t -> factory { Node = t.Subject; Graph = graph })

    static member AddInstance(graph: IGraph, subject: INode, classUri: string, factory) =
        let typeNode = graph.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
        let classNode = graph.CreateUriNode(UriFactory.Create classUri)
        graph.Assert(subject, typeNode, classNode)
        factory { Node = subject; Graph = graph }

    static member AddInstance(graph: IGraph, subject: System.Uri, classUri: string, factory) =
        CommandRuntime.AddInstance(graph, graph.CreateUriNode subject, classUri, factory)
       

type PropertyValues<'a>(subject: Resource, predicateUri: string, objectFactory, nodeFactory) =
    let predicate = subject.Graph.CreateUriNode(UriFactory.Create predicateUri)
    let getValues() =
        subject.Graph.GetTriplesWithSubjectPredicate(subject.Node, predicate)
        |> Seq.map (fun x -> objectFactory { Node = x.Object; Graph = subject.Graph } )

    interface seq<'a> with
        member _.GetEnumerator() = 
            getValues().GetEnumerator() : System.Collections.Generic.IEnumerator<'a>
        member _.GetEnumerator() = 
            getValues().GetEnumerator() :> System.Collections.IEnumerator

    member _.Add(item: 'a) =
        subject.Graph.Assert(subject.Node, predicate, nodeFactory item)

    member _.Add(item: 'a, graph: IGraph) =
        graph.Assert(subject.Node, predicate, nodeFactory item)

    member _.Remove(item: 'a) =
        subject.Graph.Retract(subject.Node, predicate, nodeFactory item)

    member _.Remove(item: 'a, graph: IGraph) =
        graph.Retract(subject.Node, predicate, nodeFactory item)        

module SchemaQuery =

    [<Literal>]
    let RdfResources = """
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri rdfs:label ?label ;
               rdfs:comment ?comment .
        }
        """

    [<Literal>]
    let RdfProperties = """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdf:Property
          OPTIONAL { ?uri rdfs:label ?label }
          OPTIONAL { ?uri rdfs:comment ?comment }
        }
        """

    [<Literal>]
    let RdfsClasses = """
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdfs:Class
          OPTIONAL { ?uri rdfs:label ?label }
          OPTIONAL { ?uri rdfs:comment ?comment }
        }
        """

    [<Literal>]
    let RdfPropertiesAndClasses = """
       PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
       PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

       SELECT ?uri ?label ?comment WHERE {
           ?uri a ?x
           VALUES (?x) { (rdf:Property) (rdfs:Class) }
           OPTIONAL { ?uri rdfs:label ?label }
           OPTIONAL { ?uri rdfs:comment ?comment }
       }
       """

module Extensions =
    type System.Collections.Generic.IEnumerable<'a> with 
        member this.Single = Seq.exactlyOne this
        member this.Option = Seq.tryExactlyOne this

    type INode with 
        member this.Uri = (this :?> IUriNode).Uri

// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("Iride.DesignTime.dll")>]
do ()
