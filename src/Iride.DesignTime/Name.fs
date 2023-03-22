namespace Iride

open System

module Name =

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