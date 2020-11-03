module IrideGraphTests

open NUnit.Framework
open Iride
open VDS.RDF.Parsing

[<Literal>]
let sample = """
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix : <http://example.org/> .

:j0 a :Company .
:j1 a :Company ;
    :parent :j0 ;
    rdfs:comment "big company" .
:Foo a :Person ;
    :age 100 ;
    :workAt :j1 ;
    :name "bob" ;
    rdfs:label "bob" .
"""

type G = GraphProvider<sample>

[<Test>]
let ``Can load graph`` () =
    let graph = new VDS.RDF.Graph()
    TurtleParser().Load(graph, new System.IO.StringReader(sample))
    let actual = G(graph)    
    ()
   
