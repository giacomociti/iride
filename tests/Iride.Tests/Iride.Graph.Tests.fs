module IrideGraphTests

open System
open NUnit.Framework
open Iride
open VDS.RDF
open VDS.RDF.Parsing

[<Literal>]
let sample1 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:Foo a :Person ;
    :age 100 ;
    :num 3.4 ;
    :nice true ;
    :dob "2000-01-01"^^xsd:date ;
    :time "2016-12-01T15:31:10-05:00"^^xsd:dateTime ;
    :name "bob" ;
    :other :bar .
"""

type G1 = GraphProvider<sample1>

[<Test>]
let ``Can load literals`` () =
    let graph = new Graph()
    TurtleParser().Load(graph, new IO.StringReader(sample1))
    let foo = graph.GetUriNode(":Foo")
    let p = G1.Person(foo)
    Assert.AreEqual (p.Type.[0], graph.GetUriNode(":Person"))
    Assert.AreEqual(100, p.Age |> Array.exactlyOne)
    Assert.AreEqual(3.4, p.Num |> Array.exactlyOne)
    Assert.True(p.Nice |> Array.exactlyOne)
    Assert.AreEqual(DateTime(2000,1,1), p.Dob |> Array.exactlyOne)
    Assert.AreEqual(DateTimeOffset(DateTime(2016,12,1, 15, 31, 10), TimeSpan.FromHours(-5.)), p.Time |> Array.exactlyOne)
    Assert.AreEqual("bob", p.Name |> Array.exactlyOne)
    Assert.AreEqual(Uri "http://example.org/bar", (p.Other |> Array.exactlyOne :?> IUriNode).Uri)

[<Literal>]
let sample2 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:Foo a :Person ;
    :age 100 ;
    :livesIn :Bar .
:Bar a :City ;
    :population 10000 .
"""

//type G2 = GraphProvider<sample2>

//[<Test>]
//let ``Can load objects`` () =
//    let graph = new Graph()
//    TurtleParser().Load(graph, new IO.StringReader(sample2))
//    let foo = graph.GetUriNode(":Foo")
//    let p = G2.Person(foo)
//    ()
