#I "../src/Iride.DesignTime/bin/Release/netcoreapp2.0/"
#I "../src/Iride/bin/Release/netstandard2.0/"
#r "Iride.dll"
#r "dotNetRDF.dll"


open Iride

[<Literal>]
let schema = """
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix : <http://example.org/> .

:foo a rdf:Property .
:Bar a rdfs:Class .

"""

[<Literal>]
let query = """
PREFIX : <http://example.org/> 
SELECT * WHERE { ?s :foo :BarZ }
"""


type Q = SparqlQueryProvider<query, Schema = schema>


type Q = SparqlQueryProvider<"ASK WHER {?s ?p ?o}">


