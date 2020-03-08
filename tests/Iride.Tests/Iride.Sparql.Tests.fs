module IrideSparqlTests

open NUnit.Framework
open VDS.RDF
open VDS.RDF.Storage
open Iride
open System
open VDS.RDF.Query

let nodeFactory = NodeFactory()
let literal = nodeFactory.CreateLiteralNode

let storage =
    let inMemoryManager = new InMemoryManager()
    inMemoryManager.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> "aa"
    }"""
    inMemoryManager :> IQueryableStorage

let runSelect (storage: IQueryableStorage) sparql =
    (storage.Query sparql) :?> SparqlResultSet


type Ask = SparqlQueryProvider<"ASK WHERE {?s ?p $o}">

[<Test>]
let ``Can ask`` () =
    let actual = Ask.GetText(literal "aa")
    Assert.AreEqual("""ASK WHERE {?s ?p "aa"}""", actual)
    

type Construct = SparqlQueryProvider<"CONSTRUCT {?s ?p $o} WHERE {?s ?p $o}">

[<Test>]
let ``Can constuct`` () =
    let actual = Construct.GetText(literal "aa")
    Assert.AreEqual("""CONSTRUCT {?s ?p "aa"} WHERE {?s ?p "aa"}""", actual)


type Select = SparqlQueryProvider<"SELECT * WHERE {?s ?p $o}">

[<Test>]
let ``Can select`` () =
    let actual = Select.GetText(literal "aa")
    Assert.AreEqual("""SELECT * WHERE {?s ?p "aa"}""", actual)
    let result = runSelect storage actual |> Seq.exactlyOne |> Select.Result
    Assert.AreEqual(Uri "http://example.org/s", (result.s :?> IUriNode).Uri)
    Assert.AreEqual(Uri "http://example.org/p", (result.p :?> IUriNode).Uri)
    

type AskString = SparqlQueryProvider<"ASK WHERE {?s ?p $LIT}">

[<Test>]
let ``Can use typed parameters`` () =
    let actual = AskString.GetText("aa")
    Assert.AreEqual("""ASK WHERE {?s ?p "aa"^^<http://www.w3.org/2001/XMLSchema#string>}""", actual)


type SelectString = SparqlQueryProvider<"SELECT * WHERE {?IRI_s ?IRI_p ?LIT}">
[<Test>]
let ``Can use typed results`` () =
    let result = SelectString.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectString.Result
    Assert.AreEqual(Uri "http://example.org/s", result.IRI_s)
    Assert.AreEqual(Uri "http://example.org/p", result.IRI_p)
    Assert.AreEqual("aa", result.LIT)

type SelectInt = SparqlQueryProvider<"SELECT * WHERE {?s ?p ?INT}">
[<Test>]
let ``Can use typed int results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> 5
    }"""
    let result = SelectInt.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectInt.Result
    Assert.AreEqual(5, result.INT)
    
type SelectDecimal = SparqlQueryProvider<"SELECT * WHERE {?s ?p ?NUM}">
[<Test>]
let ``Can use typed decimal results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> 5.2
    }"""
    let result = SelectDecimal.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectDecimal.Result
    Assert.AreEqual(5.2, result.NUM)

type SelectDate = SparqlQueryProvider<"SELECT * WHERE {?s ?p ?DATE}">
[<Test>]
let ``Can use typed date results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> "2016-12-01"^^<http://www.w3.org/2001/XMLSchema#date>
    }"""
    let result = SelectDate.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectDate.Result
    let expected = DateTime(2016, 12, 1)
    Assert.AreEqual(expected, result.DATE)

type SelectTime = SparqlQueryProvider<"SELECT * WHERE {?s ?p ?TIME}">
[<Test>]
let ``Can use typed time results`` () =
    let storage = new InMemoryManager()
    storage.Update """INSERT DATA {
        <http://example.org/s> <http://example.org/p> "2016-12-01T15:31:10-05:00"^^<http://www.w3.org/2001/XMLSchema#dateTime>
    }"""
    let result = SelectTime.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectTime.Result
    let expected = DateTimeOffset(DateTime(2016, 12, 1, 15, 31, 10), TimeSpan.FromHours -5.)
    Assert.AreEqual(expected, result.TIME)

type SelectOptional = SparqlQueryProvider<"""SELECT * WHERE  
    { { ?s1 ?p1 "aa" } UNION { ?s2 ?p2 "bb" } }  """>
[<Test>]
let ``Can use optional results`` () =
    let result = SelectOptional.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectOptional.Result
    Assert.IsTrue(result.s1.IsSome)
    Assert.IsTrue(result.s2.IsNone)

type SelectTypedOptional = SparqlQueryProvider<"""SELECT * WHERE  
    { { ?s1 <http://example.org/p> ?LIT_1 } UNION { ?s2 <http://example.org/q> ?LIT_2} }  """>
[<Test>]
let ``Can use optional typed results`` () =
    let result = SelectTypedOptional.GetText() |> runSelect storage |> Seq.exactlyOne |> SelectTypedOptional.Result
    Assert.IsTrue(result.s1.IsSome)
    Assert.IsTrue(result.s2.IsNone)
    Assert.AreEqual(Some "aa", result.LIT_1)
    Assert.AreEqual(None, result.LIT_2)

type Insert = SparqlCommandProvider<"INSERT DATA {$IRI_person <http://example.org/age> $INT_age}">
[<Test>]
let ``Can insert`` () =
    let actual = 
        Insert.GetText(
            IRI_person = System.Uri "http://example.org/p1",
            INT_age = 25)
    Assert.AreEqual("INSERT DATA {<http://example.org/p1> <http://example.org/age> 25 }", actual)
