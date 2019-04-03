module IrideSparqlTests

open NUnit.Framework
open VDS.RDF
open VDS.RDF.Storage
open Iride
open System

let nodeFactory = NodeFactory()
let literal = nodeFactory.CreateLiteralNode

let storage =
    let inMemoryManager = new InMemoryManager()
    inMemoryManager.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> "aa"
    }"""
    inMemoryManager :> IQueryableStorage


type Ask = SparqlCommand<"ASK WHERE {?s ?p $o}">

[<Test>]
let ``Can ask`` () =
    let cmd = Ask(storage)
    cmd.Run(literal "bb") |> Assert.False
    cmd.Run(literal "aa") |> Assert.True

type Construct = SparqlCommand<"CONSTRUCT {?s ?p $o} WHERE {?s ?p $o}">

[<Test>]
let ``Can constuct`` () =
    let cmd = Construct(storage)
    cmd.Run(literal "bb").IsEmpty |> Assert.True
    
    let graph = cmd.Run(literal "aa")
    graph.IsEmpty |> Assert.False
    let triple = graph.Triples |> Seq.exactlyOne
    let s = (triple.Subject :?> IUriNode).Uri
    let p = (triple.Predicate :?> IUriNode).Uri
    let o = (triple.Object :?> ILiteralNode).Value
    Assert.AreEqual(Uri "http://example.org/s", s)
    Assert.AreEqual(Uri "http://example.org/p", p)
    Assert.AreEqual("aa", o)


type Select = SparqlCommand<"SELECT * WHERE {?s ?p $o}">

[<Test>]
let ``Can select`` () =
    let cmd = Select(storage)
    let result = cmd.Run(literal "aa") |> Seq.exactlyOne
    let s = (result.s :?> IUriNode).Uri
    let p = (result.p :?> IUriNode).Uri
    Assert.AreEqual(Uri "http://example.org/s", s)
    Assert.AreEqual(Uri "http://example.org/p", p)
