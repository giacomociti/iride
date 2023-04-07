namespace Iride

open System
open VDS.RDF
open VDS.RDF.Query
open Extensions
open Name

type SchemaReader(graph: IGraph, schemaQuery: string) =

    let name = Uri >> getName >> System.Web.HttpUtility.UrlDecode

    // try to use name, fallback to uniqueName if needed
    let avoidDuplicates name uniqueName items =
        items 
        |> Seq.groupBy name
        |> Seq.collect (fun (n, xs) ->
            if Seq.length xs = 1 
            then xs |> Seq.map (fun x -> x, n)
            else xs |> Seq.map (fun x -> x, uniqueName x))

    let classes =
        graph.ExecuteQuery schemaQuery :?> SparqlResultSet
        |> Seq.filter (fun x -> x["t1"].NodeType = NodeType.Uri)
        |> Seq.groupBy (fun x -> x["t1"].Uri.AbsoluteUri)
        |> dict

    member _.GetComment (subject: Uri) =
        let commentNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#comment")
        graph.GetTriplesWithSubjectPredicate(graph.CreateUriNode(subject), commentNode)
        |> Seq.tryHead
        |> Option.map (fun t -> (t.Object :?> ILiteralNode).Value)
        |> Option.defaultValue ""
        
    member _.GetClasses () =
        classes.Keys
        |> avoidDuplicates name id
        |> Seq.map (fun (uri, name) -> {| Uri = Uri uri; Label = name |})

    member _.GetProperties (classUri: string) =
        classes[classUri]
        |> Seq.groupBy (fun x -> x["p"].Uri.AbsoluteUri)
        |> avoidDuplicates (fst >> name) fst
        |> Seq.map (fun ((p, ranges), name) ->
            let range = 
                match Array.ofSeq ranges with
                | [| r |] when r["t2"].NodeType = NodeType.Uri -> r["t2"].Uri 
                | _ -> UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#Resource"
            {| Uri = Uri p; Label = name; Range = range |})
