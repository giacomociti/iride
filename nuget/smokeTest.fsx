// #i "nuget: C:/Repos/github/iride/nuget"
#r "nuget: Iride, 0.5.0"

open Iride
open VDS.RDF.Parsing

[<Literal>]
let Sample = """
@prefix : <http://example.org/> .

:ann a :Person ;
    :age 100 .
"""

type G = GraphProvider<Sample>

let graph = new VDS.RDF.Graph()
TurtleParser().Load(graph, new System.IO.StringReader(Sample))

G.Person.Get(graph)
