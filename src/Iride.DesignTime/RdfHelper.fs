namespace Iride

open System

type Property = { Uri: Uri; Label: string; Comment: string }

module RdfHelper =

    open VDS.RDF
    open VDS.RDF.Parsing
    open VDS.RDF.Query
        
    let withGraph f rdfFile =
        use graph = new Graph()
        UriLoader.Load(graph, Uri rdfFile)
        f graph

    let getProperties (query: string) (graph: IGraph) =
        let results = graph.ExecuteQuery query
        [
            for r in results :?> SparqlResultSet do
                yield { Uri = (r.["uri"] :?> IUriNode).Uri
                        Label = (r.["label"] :?> ILiteralNode).Value
                        Comment = (r.["comment"] :?> ILiteralNode).Value }
        ]

    let getGraphProperties schemaUri query = 
        schemaUri |> withGraph (getProperties query)
