namespace Iride

open VDS.RDF
open VDS.RDF.Query
open System.Xml

type CommandRuntime =
    static member NodeFactory = NodeFactory()

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



    static member GetInstances(graph: IGraph, classUri: string, subjectConverter) =
        let typeNode = graph.GetUriNode(UriFactory.Create "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
        let classNode = graph.GetUriNode(UriFactory.Create classUri)
        graph.GetTriplesWithPredicateObject(typeNode, classNode)
        |> Seq.map (fun t -> subjectConverter(t.Subject))

    static member AddInstance(graph: IGraph, subject: INode, classUri: string, subjectConverter) =
        let typeNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
        let classNode = graph.CreateUriNode(UriFactory.Create classUri)
        graph.Assert(subject, typeNode, classNode)
        subjectConverter(subject)

type PropertyValues<'a>(subject: INode, predicateUri: string, objectConverter, nodeExtracor) =
    let predicate = subject.Graph.CreateUriNode(UriFactory.Create predicateUri)
    let getValues() =
        subject.Graph.GetTriplesWithSubjectPredicate(subject, predicate)
        |> Seq.map (fun x -> objectConverter(x.Object))

    interface seq<'a> with
        member _.GetEnumerator() = 
            getValues().GetEnumerator() : System.Collections.Generic.IEnumerator<'a>
        member _.GetEnumerator() = 
            getValues().GetEnumerator() :> System.Collections.IEnumerator

    member _.Add(item: 'a) =
        subject.Graph.Assert(subject, predicate, nodeExtracor item)

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

// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("Iride.DesignTime.dll")>]
do ()
