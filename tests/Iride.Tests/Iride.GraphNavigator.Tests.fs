module IrideGraphNavigatorTests

open System
open NUnit.Framework
open Iride
open VDS.RDF
open VDS.RDF.Parsing
open VDS.RDF.Storage

type System.Collections.Generic.IEnumerable<'a> with
    member this.Single = Seq.exactlyOne this

type INode with
    member this.Uri = (this :?> IUriNode).Uri

type Resource with
    member this.Uri = this.Node.Uri

let parseTurtle turtle =
    let graph = new Graph()
    StringParser.Parse(graph, turtle)
    // TurtleParser().Load(graph, new IO.StringReader(turtle))
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

type G1 = GraphNavigator<sample1>

[<Test>]
let ``Can load literals`` () =
    let graph = parseTurtle sample1
    let p = G1.Person.Get(graph).Single
    Assert.AreEqual(Uri "http://example.org/Foo", p.Uri)
    Assert.AreEqual(Uri "http://example.org/Person", p.Type.Single.Uri)
    Assert.AreEqual(100, p.Age.Single)
    Assert.AreEqual(3.4, p.Num.Single)
    Assert.True(p.Nice.Single)
    Assert.AreEqual(DateTime(2000,1,1), p.Dob.Single)
    Assert.AreEqual(DateTimeOffset(DateTime(2016,12,1, 15,31,10), TimeSpan.FromHours(-5.)), p.Time.Single)
    Assert.AreEqual("bob", p.Name.Single)
    Assert.AreEqual(Uri "http://example.org/bar", p.Other.Single.Uri)

[<Literal>]
let sample2 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:Foo a :Person ;
    :livesIn :Bar .
:Bar a :City ;
    :population 10000 .
"""

type G2 = GraphNavigator<sample2>

[<Test>]
let ``Can load objects`` () =
    let graph = parseTurtle sample2
    let c = G2.Person.Get(graph).Single.LivesIn.Single
    Assert.AreEqual(Uri "http://example.org/Bar", c.Uri)
    Assert.AreEqual(Uri "http://example.org/City", c.Type.Single.Uri)
    Assert.AreEqual(10000, c.Population.Single)

[<Test>]
let ``Can load blank nodes`` () =
    let graph = parseTurtle """
    @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
    @prefix : <http://example.org/> .

    _:Foo a :Person ;
        :livesIn [ a :City ; :population 10000 ] .
    """
    let c = G2.Person.Get(graph).Single.LivesIn.Single
    Assert.AreEqual(10000, c.Population.Single)

[<Test>]
let ``Has equality and hash code`` () =
     let graph = parseTurtle sample2
     let c1 = G2.Person.Get(graph).Single.LivesIn.Single
     let c2 = G2.City.Get(graph).Single
     Assert.AreEqual(c1.GetHashCode(), c2.GetHashCode())
     Assert.AreEqual(c1, c2)

type G3 = GraphNavigator<Schema = """
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix : <http://example.org/> .

:Person a rdfs:Class . # should not be necessary
:age a rdf:Property . # should not be necessary
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
    Assert.AreEqual(Uri "http://example.org/p1", p.Uri)
    Assert.AreEqual(10, p.Age.Single)

// type G4 = GraphNavigator<Schema = """
//     @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
//     @prefix schema: <http://schema.org/> .
//     @prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
//     @prefix : <http://example.org/> .

//     schema:Audiobook rdfs:subClassOf schema:Book .
//     schema:isbn schema:domainIncludes schema:Book .
//     schema:isbn schema:rangeIncludes xsd:string .
//     """>

// [<Test>]
// let ``Can use schema org`` () =
//     let graph = parseTurtle """
//     @prefix : <http://example.org/> .
//     @prefix schema: <http://schema.org/> .
//     @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

//     :b1 a schema:Book;
//         schema:isbn "abcd"^^xsd:string . # without type annotation won't work
//     """
//     let b = G4.Book.Get(graph).Single
//     Assert.AreEqual(Uri "http://example.org/b1", b.Uri)
//     Assert.AreEqual("abcd", b.Isbn.Single)

// [<Test>]
// let ``Can use subclass`` () =
//     let graph  = parseTurtle """
//     @prefix : <http://example.org/> .
//     @prefix schema: <http://schema.org/> .
//     @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

//     :b1 a schema:Audiobook;
//         schema:isbn "abcd"^^xsd:string . # without type annotation won't work
//     """
//     let b = G4.Audiobook.Get(graph).Single
//     Assert.AreEqual(Uri "http://example.org/b1", b.Resource.Uri)
//     Assert.AreEqual("abcd", b.Isbn.Single)


[<Literal>]
let sample3 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:foo a :City ;
    :id "1" ;
    :id 2 .
"""

type G5 = GraphNavigator<sample3>

[<Test>]
let ``Property with mixed types`` () =
    let graph = parseTurtle sample3
    let city = G5.City.Get(graph).Single
    let cityIds =
        city.Id
        |> Seq.cast<ILiteralNode>
        |> Seq.map (fun x -> x.Value)
        |> Seq.toArray
    Assert.AreEqual(2, Seq.length city.Id)
    Assert.Contains("1", cityIds)
    Assert.Contains("2", cityIds)


[<Literal>]
let sample4 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:foo a :City ;
    :id :v1 ;
    :id :v2 .
"""

type G6 = GraphNavigator<sample4>
[<Test>]
let ``Property with mixed classes`` () =
    let graph = parseTurtle sample4
    let city = G6.City.Get(graph).Single
    let cityIds =
        city.Id
        |> Seq.cast<IUriNode>
        |> Seq.map (fun x -> x.Uri)
        |> Seq.toArray
    Assert.AreEqual(2, Seq.length city.Id)
    Assert.Contains(Uri "http://example.org/v1", cityIds)
    Assert.Contains(Uri "http://example.org/v2", cityIds)


