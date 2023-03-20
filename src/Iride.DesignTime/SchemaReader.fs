namespace Iride

open System
open VDS.RDF
open VDS.RDF.Query
open Iride.Common

type SchemaReader(graph: IGraph, schemaQuery: string) =

    let classes =
        graph.ExecuteQuery schemaQuery :?> SparqlResultSet
        |> Seq.filter (fun x -> x["t1"].NodeType = NodeType.Uri)
        |> Seq.groupBy (fun x -> x["t1"].Uri)
        |> dict

    member _.GetComment (subject: Uri) =
        let commentNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#comment")
        graph.GetTriplesWithSubjectPredicate(graph.CreateUriNode(subject), commentNode)
        |> Seq.tryHead
        |> Option.map (fun t -> (t.Object :?> ILiteralNode).Value)
        |> Option.defaultValue ""
        
    member _.GetClasses () =
        // TODO: handle duplicate labels
        classes |> Seq.map (fun x -> {| Uri = x.Key; Label = getName x.Key |})

    member _.GetProperties (classUri: Uri) =
        classes[classUri]
        |> Seq.groupBy (fun x -> x["p"].Uri)
        |> Seq.map (fun (p, ranges) ->
            let range = 
                match Array.ofSeq ranges with
                | [| r |]  -> r["t2"].Uri 
                | _ -> UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#Resource"
            {| Uri = p; Label = getName p; Range = range |})
 