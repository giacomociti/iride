namespace Iride

open System
open System.Collections.Generic
open VDS.RDF
open VDS.RDF.Query
open Common
open Extensions

module GraphProviderHelper =

    type PropertyType = Literal of KnownDataType | Class of Uri

    type ClassType = { Name: Uri; Properties: IDictionary<Uri, PropertyType> }

    let mergeDuplicates reduction keyValuePairs =
        keyValuePairs
        |> Seq.groupBy fst
        |> Seq.map (fun (key, vals) -> key, vals |> Seq.map snd |> Seq.reduce reduction)
        |> dict

    let mergePropertyType _ _ = // don't bother with merging types
        Literal Node // will fallback to INode property

    let parseClasses (schema: SparqlResultSet) =
        let classes =
            schema.Results
            |> Seq.groupBy (fun x -> x["t1"].Uri.AbsoluteUri)
            |> dict
        classes
        |> Seq.map (function 
            KeyValue (classUri, properties) ->
              { Name = Uri classUri
                Properties = 
                    properties
                    |> Seq.map (fun x ->
                        let propertyUri = x["p"].Uri
                        let propertyTypeUri = x["t2"].Uri
                        let propertyType =
                            if classes.ContainsKey propertyTypeUri.AbsoluteUri 
                            then Class propertyTypeUri
                            else Literal (knownDataType propertyTypeUri.AbsoluteUri)
                        propertyUri, propertyType)
                    |> mergeDuplicates mergePropertyType })

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



