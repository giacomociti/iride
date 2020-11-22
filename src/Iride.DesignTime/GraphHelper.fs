namespace Iride

open System
open VDS.RDF
open System.Collections.Generic

module GraphHelper =

    type INode with
        member this.Uri = (this :?> IUriNode).Uri

    type PropertyType = Literal of Uri | Class of Uri

    type ClassType = { Name: Uri; Properties: IDictionary<Uri, PropertyType> }

    let schema2classes (schema: IGraph) =
        let classes =
             schema.Triples
             |> Seq.groupBy (fun x -> x.Subject.Uri)
             |> dict
        classes
        |> Seq.map (fun entry ->
            let props =
                entry.Value
                |> Seq.map (fun x ->
                    let pred = x.Predicate.Uri
                    let obj = x.Object.Uri
                    let value =
                        if classes.ContainsKey obj 
                        then Class obj
                        else Literal obj
                    pred, value)
            { Name = entry.Key; Properties = dict props })

    let sample2schema (sample: IGraph) =
        sample.ExecuteQuery """
            CONSTRUCT {
                ?t1 ?p ?t2
            }
            WHERE {
                ?s a ?t1 .
                ?s ?p ?o .
                OPTIONAL {?o a ?t .}
                BIND (COALESCE(?t, datatype(?o), <http://iride.dummy>) AS ?t2)
            }"""
        :?> IGraph

    let sample2classes sample = sample |> sample2schema |> schema2classes

    let parseRdfs (schema: IGraph) =
        let graph =
            schema.ExecuteQuery """
            PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#> 
            PREFIX schema: <http://schema.org/> 

            CONSTRUCT { 
                ?t1 ?p ?t2
            }
            WHERE {
                ?t1 rdfs:subClassOf* ?class .
                ?p rdfs:domain|schema:domainIncludes ?class ;
                   rdfs:range|schema:rangeIncludes ?t2 .
            }
            """
            :?> IGraph
        schema2classes graph
        



