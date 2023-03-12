module IrideSchemaReaderTests

open System
open NUnit.Framework
open Iride.Erased
open VDS.RDF
open VDS.RDF.Parsing
open VDS.RDF.Storage
open ErasedGraphProviderImplementation

let schema = """
@prefix : <http://example.org/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

:Person a rdfs:Class .
"""


[<Test>]
let ``class from schema`` () =
    let graph = GraphLoader.loadFromText schema
    let reader = SchemaReader(graph, "", "")
    let person = reader.GetClasses() |> Seq.exactlyOne
    
    Assert.AreEqual(Uri "http://example.org/Person", person.Uri)
    Assert.AreEqual("Person", person.Label) // label from URI
    Assert.AreEqual("http://example.org/Person", person.Comment) // use URI for comment


let sample = """
@prefix : <http://example.org/> .

:p a :Person .
"""
[<Test>]
let ``class from sample`` () =
    let graph = GraphLoader.loadFromText sample
    let reader = SchemaReader(graph, classesFromSampleQuery, propertiesFromSampleQuery)
    let person = reader.GetClasses() |> Seq.exactlyOne
    
    Assert.AreEqual(Uri "http://example.org/Person", person.Uri)
    Assert.AreEqual("Person", person.Label) // label from URI
    Assert.AreEqual("http://example.org/Person", person.Comment) // use URI for comment


let schemaWithLabelAndComment = """
@prefix : <http://example.org/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .

:Person a rdfs:Class ;
    rdfs:label "Person label" ;
    rdfs:comment "This is a person class" .
"""

[<Test>]
let ``class from schema with label and comment`` () =
    let graph = GraphLoader.loadFromText schemaWithLabelAndComment
    let reader = SchemaReader(graph, "", "")
    let person = reader.GetClasses() |> Seq.exactlyOne
    
    Assert.AreEqual(Uri "http://example.org/Person", person.Uri)
    Assert.AreEqual("Person label", person.Label)
    Assert.AreEqual("This is a person class", person.Comment)

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
    let reader = SchemaReader(graph, "", "")
    let person = reader.GetClasses() |> Seq.exactlyOne
    let age = reader.GetProperties (person.Uri) |> Seq.exactlyOne
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
    let reader = SchemaReader(graph, classesFromSampleQuery, propertiesFromSampleQuery)
    let person = reader.GetClasses() |> Seq.exactlyOne
    let properties = reader.GetProperties(person.Uri)
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

:Person a rdfs:Class .
:age a rdf:Property ;
    rdfs:domain :Person ;
    rdfs:range :A, :B .
"""

[<Test>]
let ``property from schema with multiple ranges`` () =
    let graph = GraphLoader.loadFromText schemaWithPropertyWithMoreRanges
    let reader = SchemaReader(graph, "", "")
    let person = reader.GetClasses() |> Seq.exactlyOne
    let age = reader.GetProperties (person.Uri) |> Seq.exactlyOne
    Assert.AreEqual("http://example.org/age", age.Uri.AbsoluteUri)
    Assert.AreEqual("http://www.w3.org/2000/01/rdf-schema#Resource", age.Range.AbsoluteUri)
