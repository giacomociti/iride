module IrideSchemaReaderTests

open System
open NUnit.Framework
open Iride
open VDS.RDF
open VDS.RDF.Parsing
open VDS.RDF.Storage
open GraphNavigatorImplementation

let schema = """
@prefix : <http://example.org/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

#:Person rdfs:subClassOf :Person .
:age rdfs:domain :Person .

"""

[<Test>]
let ``class from schema`` () =
    let graph = GraphLoader.loadFromText schema
    let reader = SchemaReader(graph, defaultQueryForSchema)
    let person = reader.GetClasses() |> Seq.exactlyOne
    Assert.AreEqual(Uri "http://example.org/Person", person.Uri)
    Assert.AreEqual("Person", person.Label)


let sample = """
@prefix : <http://example.org/> .

:p a :Person .
"""
[<Test>]
let ``class from sample`` () =
    let graph = GraphLoader.loadFromText sample
    let reader = SchemaReader(graph, defaultQueryForSample)
    let person = reader.GetClasses() |> Seq.exactlyOne
    
    Assert.AreEqual(Uri "http://example.org/Person", person.Uri)
    Assert.AreEqual("Person", person.Label)


let schemaWithComments = """
@prefix : <http://example.org/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

:age rdfs:domain :Person .
:age rdfs:comment "This is the age property" .
:Person rdfs:comment "This is a person class" .
"""

[<Test>]
let ``class from schema with comments`` () =
    let graph = GraphLoader.loadFromText schemaWithComments
    let reader = SchemaReader(graph, defaultQueryForSchema)
    Assert.AreEqual("This is a person class", reader.GetComment(Uri "http://example.org/Person"))
    Assert.AreEqual("This is the age property", reader.GetComment(Uri "http://example.org/age"))


let schemaWithProperty = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

:Person a rdfs:Class .
:age a rdf:Property ;
    rdfs:domain :Person ;
    rdfs:range xsd:integer .
"""

[<Test>]
let ``property from schema`` () =
    let graph = GraphLoader.loadFromText schemaWithProperty
    let reader = SchemaReader(graph, defaultQueryForSchema)
    let person = reader.GetClasses() |> Seq.exactlyOne
    let age = reader.GetProperties (person.Uri.AbsoluteUri) |> Seq.exactlyOne
    Assert.AreEqual("http://example.org/age", age.Uri.AbsoluteUri)
    Assert.AreEqual("http://www.w3.org/2001/XMLSchema#integer", age.Range.AbsoluteUri)
   
let sampleWithProperty = """
@prefix : <http://example.org/> .

:ann a :Person ;
    :age 100 ;
    :likes :ann .
"""
[<Test>]
let ``property from sample`` () =
    let graph = GraphLoader.loadFromText sampleWithProperty
    let reader = SchemaReader(graph, defaultQueryForSample)
    let person = reader.GetClasses() |> Seq.exactlyOne
    let properties = reader.GetProperties(person.Uri.AbsoluteUri)
    let age =
        properties
        |> Seq.filter (fun x -> x.Uri.AbsoluteUri = "http://example.org/age")
        |> Seq.exactlyOne
    Assert.AreEqual("http://www.w3.org/2001/XMLSchema#integer", age.Range.AbsoluteUri)
    let likes =
        properties
        |> Seq.filter (fun x -> x.Uri.AbsoluteUri = "http://example.org/likes")
        |> Seq.exactlyOne
    Assert.AreEqual(person.Uri.AbsoluteUri, likes.Range.AbsoluteUri)


let schemaWithPropertyWithMoreRanges = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

:age rdfs:domain :Person ;
    rdfs:range :A, :B .
"""

[<Test>]
let ``property from schema with multiple ranges`` () =
    let graph = GraphLoader.loadFromText schemaWithPropertyWithMoreRanges
    let reader = SchemaReader(graph, defaultQueryForSchema)
    let person = reader.GetClasses() |> Seq.exactlyOne
    let age = reader.GetProperties (person.Uri.AbsoluteUri) |> Seq.exactlyOne
    Assert.AreEqual("http://example.org/age", age.Uri.AbsoluteUri)
    Assert.AreEqual("http://www.w3.org/2000/01/rdf-schema#Resource", age.Range.AbsoluteUri)


let schemaWithSubClass = """
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix : <http://example.org/> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

:Person rdfs:subClassOf :Animal .
:Animal rdfs:subClassOf :Thing .
:name rdfs:domain :Thing .
"""

[<Test>]
let ``property from superclasses`` () =
    let graph = GraphLoader.loadFromText schemaWithSubClass
    let reader = SchemaReader(graph, defaultQueryForSchema)
    let classes = reader.GetClasses() |> Seq.filter (fun x -> x.Label = "Person") |> Seq.exactlyOne
    let name = reader.GetProperties ("http://example.org/Person") |> Seq.exactlyOne
    Assert.AreEqual("http://example.org/name", name.Uri.AbsoluteUri)
