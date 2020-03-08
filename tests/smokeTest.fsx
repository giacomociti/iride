#I "../src/Iride.DesignTime/bin/Release/net45/"
#I "../src/Iride/bin/Release/net45/"
#r "Iride.dll"
#r "dotNetRDF.dll"
#r "netstandard.dll"

open Iride
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage

type Q = SparqlQueryProvider<"SELECT * WHERE { ?s ?IRI_p $INT }">

let exec: string -> SparqlResultSet = 
    failwith "Use your favourite SPARQL client"

let query = Q.GetText(INT=42)
for r in exec(query) do
    let result = Q.Result(r)
    let subject: VDS.RDF.INode = result.s 
    let predicate: System.Uri = result.IRI_p
    // ....
    
    printfn ""

for result in Q.GetText(INT=42) |> exec |> Seq.map Q.Result do
    let subject: VDS.RDF.INode = result.s 
    let predicate: System.Uri = result.IRI_p
    // ...





let rs: SparqlResultSet = ...
//let results = cmd.Run(INT = 42)

for result in results do
    let subject: VDS.RDF.INode = result.s 
    let predicate: System.Uri = result.IRI_p
    // ...

type Cmd = SparqlCommandProvider<"""
    INSERT DATA {$IRI_person <http://example.org/age> $INT_age}
""">

Cmd.GetText(
    IRI_person = System.Uri "http://example.org/p1",
    INT_age = 25)
|> printfn "%s"
// INSERT DATA {<http://example.org/p1> <http://example.org/age> 25 }

let lit = NodeFactory().CreateLiteralNode("aa")

type CMD = SparqlCommandProvider<"""
insert {?s <http://example.org/bar> $o }
WHERE { ?s ?p $o }
""", RdfSchema = """C:\Repos\oss\iride\tests\Iride.Tests\vocab.ttl""", SchemaQuery = """
     select ?uri where {?uri a ?x}""">

//let cmd = CMD()
let x = CMD.GetText(lit)
printfn "%A" x

type Q = SparqlQueryProvider<"""
    prefix : <http://example.org/>
    select ?s ?p
    WHERE { optional { ?s :Foo $o }}
    """, RdfSchema = """C:\Repos\oss\iride\tests\Iride.Tests\vocab.ttl""", SchemaQuery = """
     select ?uri where {?uri a ?x}
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


