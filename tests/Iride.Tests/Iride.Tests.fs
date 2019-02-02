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

