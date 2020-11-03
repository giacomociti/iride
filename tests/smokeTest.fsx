#I "../src/Iride.DesignTime/bin/Release/netcoreapp2.0/"
#I "../src/Iride/bin/Release/netstandard2.0/"
#r "Iride.dll"
#r "dotNetRDF.dll"

open Iride
open VDS.RDF.Parsing

[<Literal>]
let sample = """
@prefix : <http://example.org/> .

:Foo a :Person ;
    :age 100 ;
    :job :j1 .
"""

type G = GraphProvider<sample>
let graph = new VDS.RDF.Graph()
TurtleParser().Load(graph, new System.IO.StringReader(sample))
let actual = G(graph)
