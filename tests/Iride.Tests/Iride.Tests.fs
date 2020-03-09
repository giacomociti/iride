module IrideTests

open NUnit.Framework
open Iride

[<Test>]
let ``Can run tests ¯\_(ツ)_/¯`` () =  ()

[<Literal>]
let Vocabulary = """
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix : <http://example.org/> .

:Foo a rdfs:Class ;
    rdfs:label "foo class" ;
    rdfs:comment "the foo class is cool" .

:bar a rdf:Property ;
    rdfs:label "bar property" .

<http://example.org/baz> a rdf:Property ;
	rdfs:comment "a property with no label" .

<http://example.org/baz#frag> a rdf:Property .
"""

type Vocab = UriProvider<Vocabulary>

[<Test>]
let ``Can access vocabulary`` () =    
    Assert.AreEqual("http://example.org/Foo", Vocab.``foo class``.ToString())


//type OwlProperties = UriProvider<CommonUris.Owl, SchemaQuery.RdfProperties>

//[<Test>]
//let ``Can access owl properties`` () =    
//   Assert.AreEqual("#cardinality", OwlProperties.cardinality.Fragment)
   

//type FoafClasses = UriProvider<"http://xmlns.com/foaf/0.1/", SchemaQuery.RdfsClasses>

//[<Test>]
//let ``Can access foaf classes`` () =    
//   Assert.AreEqual("Agent", FoafClasses.Agent.Segments |> Seq.last)

// type FileVocab = UriProvider<"Vocab.ttl">

// [<Test>]
// let ``Can access vocabulary in local file`` () =    
//     Assert.AreEqual("http://example.org/Foo", FileVocab.``foo class``.ToString())
   

type MyProps = UriProvider<Vocabulary, """
     PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
     PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

     SELECT ?uri ?label WHERE {
         ?uri a rdf:Property .
         OPTIONAL { ?uri rdfs:label ?label }
     }
 """, AllValuesMethod="GetValues">

[<Test>]
let ``Label fallback to frangment and then to last segment`` () =
    Assert.AreEqual("http://example.org/bar", MyProps.``bar property``.ToString())
    Assert.AreEqual("http://example.org/baz", MyProps.baz.ToString())
    Assert.AreEqual("http://example.org/baz#frag", MyProps.frag.ToString())

[<Test>]
let ``Values are collected`` () =
    let expected = [
        MyProps.``bar property``
        MyProps.baz
        MyProps.frag ]
    let values = MyProps.GetValues() |> List.ofArray
    Assert.AreEqual(expected, values)
