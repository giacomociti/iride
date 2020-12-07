// #I "../src/Iride.DesignTime/bin/Release/netcoreapp2.0/"
// #I "../src/Iride/bin/Release/netstandard2.0/"
// #r "Iride.dll"
// #r "dotNetRDF.dll"
#r "nuget: Iride, 0.4.13-alpha"

open Iride
open VDS.RDF.Parsing

[<Literal>]
let sample = """
@prefix : <http://example.org/> .

:ann a :Person ;
    :age 100 .
"""

type G = GraphProvider<sample>

let graph = new VDS.RDF.Graph()
TurtleParser().Load(graph, new System.IO.StringReader(sample))

 G.Person.Get(graph)
