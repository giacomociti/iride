module IrideTests

open NUnit.Framework
open Iride

[<Literal>]
let vocabulary = """
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

type Resources = UriProvider<vocabulary>

[<Test>]
let ``Can access vocabulary`` () =    
    Assert.AreEqual("http://example.org/Foo", Resources.``foo class``.AbsoluteUri)
   

type Properties = UriProvider<vocabulary, """
     PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
     PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

     SELECT ?uri ?label WHERE {
         ?uri a rdf:Property .
         OPTIONAL { ?uri rdfs:label ?label }
     }
 """, AllValuesMethod="GetValues">

[<Test>]
let ``Label fallback to fragment and then to last segment`` () =
    Assert.AreEqual("http://example.org/bar", Properties.``bar property``.AbsoluteUri)
    Assert.AreEqual("http://example.org/baz", Properties.Baz.AbsoluteUri)
    Assert.AreEqual("http://example.org/baz#frag", Properties.Frag.AbsoluteUri)

[<Test>]
let ``Values are collected`` () =
    let expected = [
        Properties.``bar property``
        Properties.Baz
        Properties.Frag ]
    let values = Properties.GetValues() |> List.ofArray
    Assert.AreEqual(expected, values)