namespace Iride

open System
open VDS.RDF
open VDS.RDF.Parsing

module GraphLoader =

    let tryPath resolutionFolder source =
        [ source; IO.Path.Combine(resolutionFolder, source)]
        |> List.tryFind IO.File.Exists

    let loadFromFile file =
        let ts = new TripleStore()
        ts.LoadFromFile(file)
        let graph = new Graph()
        ts.Graphs |> Seq.iter graph.Merge
        graph

    let tryUri x =
        try Some (Uri x)
        with _ -> None

    let loadFromUri (uri: Uri) =
        let graph = new Graph()
        let ext = IO.Path.GetExtension(uri.AbsoluteUri).TrimStart('.')
        match MimeTypesHelper.GetDefinitionsByFileExtension(ext) |> Seq.toList with
        | mimeType::_ ->
            if mimeType.CanParseRdf 
            then graph.LoadFromUri(uri, mimeType.GetRdfParser())
            elif mimeType.CanParseRdfDatasets
            then
                let ts = new TripleStore()
                ts.LoadFromUri(uri, mimeType.GetRdfDatasetParser())
                ts.Graphs |> Seq.iter graph.Merge
            else graph.LoadFromUri(uri)
        | _ -> graph.LoadFromUri(uri)
        graph

    let loadFromText text =
        let graph = new Graph()
        StringParser.Parse(graph, text)
        graph

    let load resolutionFolder source =
        match tryPath resolutionFolder source |> Option.map loadFromFile with
        | Some graph -> graph
        | None ->
            match tryUri source |> Option.map loadFromUri with
            | Some graph -> graph
            | None -> 
                try loadFromText source 
                with error ->
                    failwithf """Could not load RDF neither from file, URL nor literal.
Current directory is %s, resolution folder is %s.
Attempting to parse the parameter as literal failed with error: %A""" 
                        Environment.CurrentDirectory resolutionFolder error
