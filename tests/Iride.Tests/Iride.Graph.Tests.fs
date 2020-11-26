module IrideGraphTests

open System
open NUnit.Framework
open Iride
open VDS.RDF
open VDS.RDF.Parsing

type System.Collections.Generic.IEnumerable<'a> with
    member this.Single = Seq.exactlyOne this

type INode with
    member this.Uri = (this :?> IUriNode).Uri

let parseTurtle turtle =
    let graph = new Graph()
    TurtleParser().Load(graph, new IO.StringReader(turtle))
    graph



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
    let graph = parseTurtle sample1
    let p = G1.Person.Get(graph).Single
    Assert.AreEqual (p.Type.Single, graph.GetUriNode(":Person"))
    Assert.AreEqual(100, p.Age.Single)
    Assert.AreEqual(3.4, p.Num.Single)
    Assert.True(p.Nice.Single)
    Assert.AreEqual(DateTime(2000,1,1), p.Dob.Single)
    Assert.AreEqual(DateTimeOffset(DateTime(2016,12,1, 15,31,10), TimeSpan.FromHours(-5.)), p.Time.Single)
    Assert.AreEqual("bob", p.Name.Single)
    Assert.AreEqual(Uri "http://example.org/bar", (p.Other.Single :?> IUriNode).Uri)

[<Literal>]
let sample2 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:Foo a :Person ;
    :age 100 ;
    :livesIn :Bar .
:Bar a :City ;
    :name "Pisa" ;
    :population 10000 .
"""

type G2 = GraphProvider<sample2>

[<Test>]
let ``Can load objects`` () =
    let graph = parseTurtle sample2
    let p = G2.Person.Get(graph).Single
    Assert.AreEqual(Uri "http://example.org/Foo", p.Node.Uri)
    let c = p.LivesIn.Single
    Assert.AreEqual("Pisa", c.Name.Single)
    Assert.AreEqual(10000, c.Population.Single)

[<Test>]
let ``Overrides equality and hash code`` () =
     let graph = parseTurtle sample2
     let c1 = G2.Person.Get(graph).Single.LivesIn.Single
     let c2 = G2.City.Get(graph).Single
     Assert.AreEqual(c1.GetHashCode(), c2.GetHashCode())
     Assert.AreEqual(c1, c2)

type G3 = GraphProvider<Schema = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix : <http://example.org/> .

:age rdfs:domain :Person ;
    rdfs:range xsd:integer .
""">

[<Test>]
let ``Can use schema`` () =
    let graph = parseTurtle """
    @prefix : <http://example.org/> .
    :p1 a :Person; :age 10 .
    """
    let p = G3.Person.Get(graph).Single
    Assert.AreEqual(Uri "http://example.org/p1", p.Node.Uri)
    Assert.AreEqual(10, p.Age.Single)

[<Test>]
let ``Can add instance`` () =
    let graph = new Graph()
    let nodeUri = Uri "http://example.org/p1"
    let classUri = Uri "http://example.org/Person"
    let typeUri = Uri "http://www.w3.org/1999/02/22-rdf-syntax-ns#type"
    let p = G3.Person.Add(graph, graph.CreateUriNode(nodeUri))
    Assert.AreEqual(nodeUri, p.Node.Uri)
    let triple = graph.Triples.Single
    Assert.AreEqual(nodeUri, triple.Subject.Uri)
    Assert.AreEqual(typeUri, triple.Predicate.Uri)
    Assert.AreEqual(classUri, triple.Object.Uri)

[<Test>]
let ``Can add literal property`` () =
    let graph = new Graph()
    let nodeUri = Uri "http://example.org/p1"
    let p = G3.Person.Add(graph, graph.CreateUriNode(nodeUri))
    p.Age.Add(25)
    Assert.AreEqual(25, p.Age.Single)
    //Assert.AreEqual(nodeUri, p.Node.Uri)
    //let triple = graph.Triples.Single
    //Assert.AreEqual(nodeUri, triple.Subject.Uri)
    //Assert.AreEqual(typeUri, triple.Predicate.Uri)
    //Assert.AreEqual(classUri, triple.Object.Uri)
