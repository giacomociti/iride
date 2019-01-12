namespace Iride

open System

type Property = { Uri: Uri; Label: string; Comment: string }

module Query =
    let RdfProperties = """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdf:Property ;
               rdfs:label ?label ;
               rdfs:comment ?comment .
        }
        """

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
