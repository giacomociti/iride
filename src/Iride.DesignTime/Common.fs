﻿namespace Iride

open System
open VDS.RDF
open VDS.RDF.Query

module Common =

    type KnownDataType = Node | Iri | Literal | Integer | Number | Date | Time | Boolean

    type Property = { Uri: Uri; Label: string; Comment: string }

    let upperInitial (x: string) =
        let head, tail = x.Substring(0, 1), x.Substring(1)
        head.ToUpperInvariant() + tail;

    let lowerInitial (x: string) =
        let head, tail = x.Substring(0, 1), x.Substring(1)
        head.ToLowerInvariant() + tail;

    let getName (uri: Uri) =
        if uri.Fragment.StartsWith "#"
        then uri.Fragment.Substring 1
        else Seq.last uri.Segments
        |> upperInitial

    let getProperties (query: string) (graph: IGraph) =
        let results = graph.ExecuteQuery query
        [
            for r in results :?> SparqlResultSet do
                let uri = (r.["uri"] :?> IUriNode).Uri
                let label =
                    match r.TryGetBoundValue "label" with
                    | true, x -> (x :?> ILiteralNode).Value
                    | false, _ -> getName uri
                let comment =
                    match r.TryGetBoundValue "comment" with
                    | true, x -> (x :?> ILiteralNode).Value
                    | false, _ -> uri.ToString()

                yield { Uri = uri; Label = label; Comment = comment }
        ]
