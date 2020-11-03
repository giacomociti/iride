namespace Iride

open System

type Property = { Uri: Uri; Label: string; Comment: string }

module RdfHelper =

    open VDS.RDF
    open VDS.RDF.Parsing
    open VDS.RDF.Query

    let getName (uri: Uri) =
        let upperInitial (x: string) =
            let head, tail = x.Substring(0, 1), x.Substring(1)
            head.ToUpperInvariant() + tail;

        if uri.Fragment.StartsWith "#"
        then uri.Fragment.Substring 1
        else Seq.last uri.Segments
        |> upperInitial

    let getProperties (query: string) (graph: IGraph) =
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

                yield { Uri = uri; Label = label; Comment = comment }
        ]


    let tryParseTurtle (schema: string) =
        let graph = new Graph()
        try 
            TurtleParser().Load(graph, new IO.StringReader(schema))
            Some graph
        with _ -> None
     
    let getGraph resolutionFolder schema = 
        match tryParseTurtle schema with
        | Some graph -> graph
        | None ->
            let graph = new Graph()
            let path = IO.Path.Combine(resolutionFolder, schema)
            if IO.File.Exists path
            then FileLoader.Load(graph, path)
            else UriLoader.Load(graph, Uri schema)
            graph

    let getGraphProperties resolutionFolder schema query =
        use graph = getGraph resolutionFolder schema
        getProperties query graph