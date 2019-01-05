module RdfHelper

    open System
    open VDS.RDF
    open VDS.RDF.Parsing
    open VDS.RDF.Query

    type Property = { Uri: Uri; Label: string; Comment: string }
        
    let withGraph f rdfFile =
        use graph = new Graph()
        UriLoader.Load(graph, Uri rdfFile)
        f graph

    let getProperties (graph: IGraph) =
        let results = graph.ExecuteQuery """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdf:Property ;
               rdfs:label ?label ;
               rdfs:comment ?comment .
        }
        """
        [
            for r in results :?> SparqlResultSet do
                yield { Uri = (r.["uri"] :?> IUriNode).Uri
                        Label = (r.["label"] :?> ILiteralNode).Value
                        Comment = (r.["comment"] :?> ILiteralNode).Value }
        
        ]
