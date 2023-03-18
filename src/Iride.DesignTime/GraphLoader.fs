namespace Iride

open System
open VDS.RDF
open VDS.RDF.Parsing

module GraphLoader =
    let tryUri source =
        try Some (Uri source)
        with _ -> None

    let tryFile resolutionFolder source =
        [ source; IO.Path.Combine(resolutionFolder, source)]
        |> List.tryFind IO.File.Exists

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

    let loadFromFile file =
        let graph = new Graph()
        FileLoader.Load(graph, file)
        graph

    let loadFromText text =
        let graph = new Graph()
        StringParser.Parse(graph, text)
        graph

    let load resolutionFolder source =
        match tryUri source with
        | Some uri -> loadFromUri uri
        | None ->
            match tryFile resolutionFolder source with
            | Some file -> loadFromFile file
            | None -> loadFromText source
