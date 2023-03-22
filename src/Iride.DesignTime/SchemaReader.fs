namespace Iride

open System
open VDS.RDF
open VDS.RDF.Query
open Extensions
open Name

type SchemaReader(graph: IGraph, schemaQuery: string) =

    // try to use name, fallback to uniqueName if needed
    let avoidDuplicates name uniqueName items =
        items 
        |> Seq.groupBy (name >> System.Web.HttpUtility.UrlDecode)
        |> Seq.collect (fun (n, xs) ->
            if Seq.length xs = 1 
            then xs |> Seq.map (fun x -> x, n)
            else xs |> Seq.map (fun x -> x, uniqueName x))

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
        classes.Keys
        |> avoidDuplicates getName (fun x -> x.AbsoluteUri)
        |> Seq.map (fun (uri, name) -> {| Uri = uri; Label = name |})

    member _.GetProperties (classUri: Uri) =
        classes[classUri]
        |> Seq.groupBy (fun x -> x["p"].Uri)
        |> avoidDuplicates (fst >> getName) (fst >> fun x -> x.AbsoluteUri)
        |> Seq.map (fun ((p, ranges), name) ->
            let range = 
                match Array.ofSeq ranges with
                | [| r |] when r["t2"].NodeType = NodeType.Uri -> r["t2"].Uri 
                | _ -> UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#Resource"
            {| Uri = p; Label = name; Range = range |})
