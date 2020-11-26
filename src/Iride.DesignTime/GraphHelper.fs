namespace Iride

open System
open VDS.RDF
open System.Collections.Generic
open VDS.RDF.Query

module GraphHelper =

    type INode with
        member this.Uri = (this :?> IUriNode).Uri

    type PropertyType = Literal of Uri | Class of Uri

    type ClassType = { Name: Uri; Properties: IDictionary<Uri, PropertyType> }

    let parseClasses (schema: SparqlResultSet) =
        let classes =
            schema.Results
            |> Seq.groupBy (fun x -> x.["t1"].Uri)
            |> dict
        classes
        |> Seq.map (function 
            KeyValue (classUri, properties) ->
              { Name = classUri
                Properties = 
                    properties
                    |> Seq.map (fun x ->
                        let pred = x.["p"].Uri
                        let obj = x.["t2"].Uri
                        let value =
                            if classes.ContainsKey obj 
                            then Class obj
                            else Literal obj
                        pred, value)
                    |> dict })

    let sampleQuery = """
        SELECT ?t1 ?p ?t2
        WHERE {
            ?s a ?t1 .
            ?s ?p ?o .
            OPTIONAL {?o a ?t .}
            BIND (COALESCE(?t, datatype(?o), <http://iride.dummy>) AS ?t2)
        }"""

    let schemaQuery = """
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#> 
        PREFIX schema: <http://schema.org/> 

        SELECT ?t1 ?p ?t2
        WHERE {
            ?t1 rdfs:subClassOf* ?class .
            ?p rdfs:domain|schema:domainIncludes ?class ;
               rdfs:range|schema:rangeIncludes ?t2 .
        }"""

    let parse (query: string) (graph: IGraph) = 
        graph.ExecuteQuery query :?> SparqlResultSet
        |> parseClasses

    let sample2classes sample = parse sampleQuery sample

    let schema2classes schema = parse schemaQuery schema



