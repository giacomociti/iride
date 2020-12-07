// TODO
//   When working from a schema:
//   - consider creating also the 'type' property
//   - support also rdfs:Literal
//   - consider making range optional


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
    Assert.AreEqual(Uri "http://example.org/Foo", p.Node.Uri)
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

type G2 = GraphProvider<sample2>

[<Test>]
let ``Can load objects`` () =
    let graph = parseTurtle sample2
    let c = G2.Person.Get(graph).Single.LivesIn.Single
    Assert.AreEqual(Uri "http://example.org/Bar", c.Node.Uri)
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

type G4 = GraphProvider<Schema = """
    @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
    @prefix schema: <http://schema.org/> .
    @prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
    @prefix : <http://example.org/> .

    schema:Audiobook rdfs:subClassOf schema:Book .
    schema:isbn schema:domainIncludes schema:Book .
    schema:isbn schema:rangeIncludes xsd:string .
    """>

[<Test>]
let ``Can use schema org`` () =
    let graph = parseTurtle """
    @prefix : <http://example.org/> .
    @prefix schema: <http://schema.org/> .
    @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

    :b1 a schema:Book;
        schema:isbn "abcd"^^xsd:string . # without type annotation won't work
    """
    let b = G4.Book.Get(graph).Single
    Assert.AreEqual(Uri "http://example.org/b1", b.Node.Uri)
    Assert.AreEqual("abcd", b.Isbn.Single)

[<Test>]
let ``Can use subclass`` () =
    let graph  = parseTurtle """
    @prefix : <http://example.org/> .
    @prefix schema: <http://schema.org/> .
    @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

    :b1 a schema:Audiobook;
        schema:isbn "abcd"^^xsd:string . # without type annotation won't work
    """
    let b = G4.Audiobook.Get(graph).Single
    Assert.AreEqual(Uri "http://example.org/b1", b.Node.Uri)
    Assert.AreEqual("abcd", b.Isbn.Single)


[<Test>]
let ``Can add instance`` () =
    let graph = new Graph()
    Assert.IsEmpty(G3.Person.Get(graph))

    let nodeUri = Uri "http://example.org/p1"

    let p = G3.Person.Add(graph, graph.CreateUriNode(nodeUri))
    Assert.AreEqual(p, G3.Person.Get(graph).Single)
    Assert.AreEqual(nodeUri, p.Node.Uri)
    let triple = graph.Triples.Single
    Assert.AreEqual(nodeUri, triple.Subject.Uri)
    Assert.AreEqual(Uri "http://www.w3.org/1999/02/22-rdf-syntax-ns#type", triple.Predicate.Uri)
    Assert.AreEqual(Uri "http://example.org/Person", triple.Object.Uri)

[<Test>]
let ``Can add literal property`` () =
    let graph = new Graph()
    let nodeUri = Uri "http://example.org/p1"

    let p = G3.Person.Add(graph, graph.CreateUriNode(nodeUri))
    Assert.IsEmpty(p.Age)

    p.Age.Add(25)
    Assert.AreEqual(25, p.Age.Single)

    p.Age.Add(26)
    Assert.AreEqual(set [25; 26], set p.Age)


[<Test>]
let ``Can add property`` () =
    let graph = new Graph()
    let personUri = Uri "http://example.org/p1"
    let p = G2.Person.Add(graph, graph.CreateUriNode(personUri))
    Assert.IsEmpty(p.LivesIn)

    let c1 = G2.City.Add(graph, graph.CreateUriNode(Uri "http://example.org/c1"))
    p.LivesIn.Add(c1)
    Assert.AreEqual(c1, p.LivesIn.Single)

    let c2 = G2.City.Add(graph, graph.CreateUriNode(Uri "http://example.org/c2"))
    p.LivesIn.Add(c2)
    Assert.AreEqual(2,  Seq.length p.LivesIn)
    Assert.Contains(c1, Seq.toArray p.LivesIn)
    Assert.Contains(c2, Seq.toArray p.LivesIn)


[<Literal>]
let sample3 = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .

:foo a :City ;
    :id "1" ;
    :id 2 .
"""

type G5 = GraphProvider<sample3>

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

type G6 = GraphProvider<sample4>
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


