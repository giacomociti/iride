#I "../src/Iride.DesignTime/bin/Release/netstandard2.0/"
#I "../src/Iride/bin/Release/netstandard2.0/"
#r "Iride.dll"

open Iride

type Rdf = UriProvider<"http://www.w3.org/1999/02/22-rdf-syntax-ns">

type Rdfs = UriProvider<"http://www.w3.org/2000/01/rdf-schema">

type Owl = UriProvider<"""http://www.w3.org/2002/07/owl""", SparqlQuery = """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdf:Property ;
               rdfs:label ?label ;
               rdfs:comment ?comment .
        }
        """>

type Book = UriProvider<"https://schema.org/Book.ttl">

Rdf.subject.Fragment
Rdfs.label.Fragment
Owl.cardinality.Fragment
Book.bookFormat.ToString()

