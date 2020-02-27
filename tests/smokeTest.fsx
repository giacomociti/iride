open VDS.RDF.Query.Algebra
#I "../src/Iride.DesignTime/bin/Release/net45/"
#I "../src/Iride/bin/Release/net45/"
#r "Iride.dll"
#r "dotNetRDF.dll"
#r "netstandard.dll"

#r "VDS.Common.dll"

open Iride
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage

let lit = NodeFactory().CreateLiteralNode("aa")

type CMD = SparqlParametrizedCommand<"""
insert {?s ?p $o }
WHERE { ?s ?p $o }
""">

let cmd = CMD()
let x = cmd.GetText(lit)
printfn "%A" x

type Q = SparqlParametrizedQuery<"""
select ?s ?p
WHERE { ?s ?p $o }
""">
let query = Q()
let text = query.GetText(lit)

let storage = new InMemoryManager()
storage.Update("""INSERT DATA 
{ <http://www.example.com/s1> <http://www.example.com/p1> "aa"}
""")
let rs = storage.Query(text) :?> SparqlResultSet


for r in query.GetResults(rs) do
    printfn "%A" r.s
    printfn "%A" r.p


