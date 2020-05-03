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
    Assert.AreEqual("""ASK WHERE {?s ?p "aa"}""", actual)


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

type InsertTyped = SparqlCommandProvider<"""
    PREFIX : <http://example.org/>
    INSERT DATA {
        $IRI_Subject
            :iri $IRI_Object ;
            :lit $LIT ;
            :int $INT ;
            :num $NUM ;
            :date $DATE ;
            :time $TIME ;
            :bool $BOOL ;
            :node $Untyped .
    }""">

type SelectTyped = SparqlQueryProvider<"""
    PREFIX : <http://example.org/>
    SELECT * WHERE {
        $IRI_Subject
            :iri ?IRI_Object ;
            :lit ?LIT ;
            :int ?INT ;
            :num ?NUM ;
            :date ?DATE ;
            :time ?TIME ;
            :bool ?BOOL ;
            :node ?Untyped .
    }""">

[<Test>]
let ``Can insert typed values`` () =
    let actual = 
        InsertTyped.GetText(
            IRI_Subject = (Uri "http://example.org/s"),
            IRI_Object = (Uri "http://example.org/o"),
            LIT = "foo",
            INT = 3,
            NUM = 5.4m,
            DATE = DateTime(2020, 12, 31),
            TIME = DateTimeOffset(2020, 12, 31, 10, 23, 55, TimeSpan.FromHours(2.0)),
            BOOL = false,
            Untyped = (literal "x" :> INode))

    let expected = """
    PREFIX : <http://example.org/>

    INSERT DATA {
        <http://example.org/s>
            :iri <http://example.org/o> ;
            :lit "foo" ;
            :int 3  ;
            :num 5.4 ;
            :date "2020-12-31" ;
            :time "2020-12-31T10:23:55.000000+02:00"^^<http://www.w3.org/2001/XMLSchema#dateTime> ;
            :bool false ;
            :node "x" .
    }"""

    Assert.AreEqual(expected.Trim(), actual.Trim())


type TypedRecord = {
    subject: Uri
    iri : Uri
    untyped: INode
    literal: string
    integer: int
    number: decimal
    date: DateTime
    time: DateTimeOffset
    boolean: bool }

let roundtrip record =
    let storage = new InMemoryManager()

    InsertTyped.GetText(
        IRI_Subject = record.subject,
        IRI_Object = record.iri,
        Untyped = record.untyped,
        LIT = record.literal,
        INT = record.integer,
        NUM = record.number,
        DATE = record.date,
        TIME = record.time,
        BOOL = record.boolean)
    |> storage.Update

    SelectTyped.GetText(record.subject) 
    |> runSelect storage
    |> Seq.exactlyOne 
    |> SelectTyped.Result
    |> fun x -> {
        subject = record.subject
        iri = x.IRI_Object
        untyped = x.Untyped
        literal = x.LIT
        integer = x.INT
        number = x.NUM
        date = x.DATE
        time = x.TIME
        boolean = x.BOOL }


[<Test>]
let ``Can select typed values`` () =
    let record = {
        subject = Uri "http://example.org/s"
        iri = Uri "http://example.org/o"
        untyped = literal "x" :> INode
        literal = "foo"
        integer = 5
        number = 5.4m
        date = DateTime(2020, 12, 31)
        time = DateTimeOffset(2020, 12, 31, 10, 23, 55, TimeSpan.FromHours(2.0))
        boolean = true }

    Assert.AreEqual (record, roundtrip record)
   
