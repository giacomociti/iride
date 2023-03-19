module IrideGraphLoaderTests

open System
open NUnit.Framework
open Iride

let validRDF = """
    @prefix : <http://example.org/> .
    :ann a :Person .
"""

[<Test>]
let ``load from file`` () =
    IO.File.WriteAllText("file.ttl", validRDF)
    let graph = GraphLoader.load "" "file.ttl"
    Assert.AreEqual(1, graph.Triples.Count)

[<Test>]
let ``load from literal`` () =
    let graph = GraphLoader.load "" validRDF
    Assert.AreEqual(1, graph.Triples.Count)