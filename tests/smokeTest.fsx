#I "../src/Iride.DesignTime/bin/Release/net45/"
#I "../src/Iride/bin/Release/net45/"
#r "Iride.dll"
#r "dotNetRDF.dll"
#r "netstandard.dll"

open Iride
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage

let lit = NodeFactory().CreateLiteralNode("aa")

type CMD = SparqlParametrizedCommand<"""
insert {?s ?p $o }
WHERE { ?s ?p $o }
""">

//let cmd = CMD()
let x = CMD.GetText(lit)
printfn "%A" x

type Q = SparqlParametrizedQuery<"""
select ?s ?p
WHERE { optional { ?s ?p $o }}
""">

let text = Q.GetText(lit)

let storage = new InMemoryManager()
storage.Update("""INSERT DATA 
{ <http://www.example.com/s1> <http://www.example.com/p1> "aa"}
""")
let rs = storage.Query(text) :?> SparqlResultSet

for r in rs do
    let rr = Q.Result(r)
    match rr.s with
    | Some x -> printfn "%A" rr.s
    | None -> printf "No"

let mapResults mapper (results: SparqlResultSet) =
    results
    |> Seq.map mapper
    |> Seq.toArray

rs
|> mapResults Q.Result
|> Array.iter (fun r ->
       printfn "%A" r.s
       printfn "%A" r.p
    )
// for r in Q.GetResults(rs) do
//    printfn "%A" r.s
//    printfn "%A" r.p


