namespace Iride

open System

type Property = { Uri: Uri; Label: string; Comment: string }

module RdfHelper =

    open VDS.RDF
    open VDS.RDF.Parsing
    open VDS.RDF.Query
        
    let getProperties (query: string) (graph: IGraph) =
        let results = graph.ExecuteQuery query
        [
            for r in results :?> SparqlResultSet do
                yield { Uri = (r.["uri"] :?> IUriNode).Uri
                        Label = (r.["label"] :?> ILiteralNode).Value
                        Comment = (r.["comment"] :?> ILiteralNode).Value }
        ]

    let getGraphProperties resolutionFolder schema query =
        use graph = new Graph()
        let path = IO.Path.Combine(resolutionFolder, schema)
        if IO.File.Exists path
        then FileLoader.Load(graph, path)
        else UriLoader.Load(graph, Uri schema)
        getProperties query graph