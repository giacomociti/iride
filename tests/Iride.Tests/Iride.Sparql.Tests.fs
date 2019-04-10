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

type AskString = SparqlCommand<"ASK WHERE {?s ?p $s_text}">

[<Test>]
let ``Can use typed parameters`` () =
    let cmd = AskString(storage)
    cmd.Run("bb") |> Assert.False
    cmd.Run("aa") |> Assert.True

type SelectString = SparqlCommand<"SELECT * WHERE {?u_s ?u_p ?s_text}">
[<Test>]
let ``Can use typed results`` () =
    let cmd = SelectString(storage)
    let result = cmd.Run() |> Seq.exactlyOne
    Assert.AreEqual(Uri "http://example.org/s", result.u_s)
    Assert.AreEqual(Uri "http://example.org/p", result.u_p)
    Assert.AreEqual("aa", result.s_text)

type SelectInt = SparqlCommand<"SELECT * WHERE {?s ?p ?i_num}">
[<Test>]
let ``Can use typed int results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> 5
    }"""
    let cmd = SelectInt(storage)
    let result = cmd.Run() |> Seq.exactlyOne
    Assert.AreEqual(5, result.i_num)
    
type SelectDecimal = SparqlCommand<"SELECT * WHERE {?s ?p ?d_num}">
[<Test>]
let ``Can use typed decimal results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> 5.2
    }"""
    let cmd = SelectDecimal(storage)
    let result = cmd.Run() |> Seq.exactlyOne
    Assert.AreEqual(5.2, result.d_num)

type SelectDate = SparqlCommand<"SELECT * WHERE {?s ?p ?t_date}">
[<Test>]
let ``Can use typed date results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> "2016-12-01T15:31:10-05:00"^^<http://www.w3.org/2001/XMLSchema#dateTime>
    }"""
    let cmd = SelectDate(storage)
    let result = cmd.Run() |> Seq.exactlyOne
    let expected = DateTimeOffset(DateTime(2016, 12, 1, 15, 31, 10), TimeSpan.FromHours -5.)
    Assert.AreEqual(expected, result.t_date)
