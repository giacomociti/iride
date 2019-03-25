module IrideTests

open NUnit.Framework
open Iride

[<Test>]
let ``Can run tests ¯\_(ツ)_/¯`` () =  ()


type Rdfs = UriProvider<CommonUris.Rdfs>

[<Test>]
let ``Can access rdfs terms`` () =    
   Assert.AreEqual("http://www.w3.org/2000/01/rdf-schema#label", Rdfs.label.ToString())


type OwlProperties = UriProvider<CommonUris.Owl, Query.RdfProperties>

[<Test>]
let ``Can access owl properties`` () =    
   Assert.AreEqual("#cardinality", OwlProperties.cardinality.Fragment)
   

type FoafClasses = UriProvider<"http://xmlns.com/foaf/0.1/", Query.RdfsClasses>

[<Test>]
let ``Can access foaf classes`` () =    
   Assert.AreEqual("Agent", FoafClasses.Agent.Segments |> Seq.last)

// type FileVocab = UriProvider<"Vocab.ttl">

// [<Test>]
// let ``Can access vocabulary in local file`` () =    
//    Assert.AreEqual("http://example.org/Foo", FileVocab.``foo class``.ToString())
   

// type MyProps = UriProvider<"Vocab.ttl", """
//     PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
//     PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

//     SELECT ?uri ?label WHERE {
//         ?uri a rdf:Property .
//         OPTIONAL { ?uri rdfs:label ?label }
//     }
// """, AllValuesMethod="GetValues">

// [<Test>]
// let ``Label fallback to frangment and then to last segment`` () =
//     Assert.AreEqual("http://example.org/bar", MyProps.``bar property``.ToString())
//     Assert.AreEqual("http://example.org/baz", MyProps.baz.ToString())
//     Assert.AreEqual("http://example.org/baz#frag", MyProps.frag.ToString())

// [<Test>]
// let ``Values are collected`` () =
//     let expected = [
//         MyProps.``bar property``
//         MyProps.baz
//         MyProps.frag ]
//     let values = MyProps.GetValues() |> List.ofArray
//     Assert.AreEqual(expected, values)
