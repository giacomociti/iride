#I "../src/Iride.DesignTime/bin/Release/net45/"
#I "../src/Iride/bin/Release/net45/"
#r "Iride.dll"
#r "dotNetRDF.dll"
#r "netstandard.dll"

open Iride

// type Rdf = UriProvider<"http://www.w3.org/1999/02/22-rdf-syntax-ns">

// type Rdfs = UriProvider<"http://www.w3.org/2000/01/rdf-schema">

// type Owl = UriProvider<"""http://www.w3.org/2002/07/owl""", SparqlQuery = """
//         PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
//         PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

//         SELECT ?uri ?label ?comment WHERE {
//           ?uri a rdf:Property ;
//                rdfs:label ?label ;
//                rdfs:comment ?comment .
//         }
//         """>

// type Book = UriProvider<"https://schema.org/Book.ttl">

// Rdf.subject.Fragment
// Rdfs.label.Fragment
// Owl.cardinality.Fragment
// Book.bookFormat.ToString()

type CMD = SparqlCommand<"""
select ?s ?p
#ask
WHERE {
          ?s ?p $o
        }
""">

// open System
open VDS.RDF
open VDS.RDF.Storage
let storage = new InMemoryManager()

let c = CMD(storage)
let lit = NodeFactory().CreateLiteralNode("aa")
let x = c.Run(lit)

