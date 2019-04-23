[![NuGet Badge](https://buildstats.info/nuget/Iride)](https://www.nuget.org/packages/Iride)

This library contains two F# type providers built on top of [dotNetRDF](https://github.com/dotnetrdf/dotnetrdf).

## UriProvider

_UriProvider_ is a simple generative provider to create `System.Uri` properties
from IRIs in RDF vocabularies.

    type Book = UriProvider<"https://schema.org/Book.ttl">

    let a: System.Uri = Book.author

The vocabulary can be either a local file or a web resource like in the example above.
The list of IRIs for which a property is generated is obtained with the following SPARQL query:

        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri rdfs:label ?label ;
               rdfs:comment ?comment .
        }

You can provide your own SPARQL query to customize the set of properties.

## SparqlCommand
_SparqlCommand_ provides some type safety around SPARQL queries, in the same vein of [SqlCommandProvider](http://fsprojects.github.io/FSharp.Data.SqlClient/).

    type CMD = SparqlCommand<"SELECT * WHERE { ?s ?IRI_p $INT }">
    let storage = new InMemoryManager()
    // ...
    let cmd = CMD(storage)
    
    let results = cmd.Run(INT = 42)
    
    for result in results do
        let subject: VDS.RDF.INode = result.s 
        let predicate: System.Uri = result.IRI_p
        // ...

In SPARQL, output variables start with either '?' or '$', but in practice only '?' is used.
Hence this library hijacks the prefix '$' to indicate input parameters.

Furthermore, upper case data type hints (e.g. IRI, INT) instruct the type provider to
assign types to parameters and variables. Notice however that triple stores
may return unparseable values due to the schemaless nature of RDF.

## Building
The type provider has separate design-time and runtime assemblies.

Paket is used to acquire the type provider SDK and build the nuget package.



    .paket/paket.exe update

    dotnet build -c release

    .paket/paket.exe pack src/Iride --version 0.0.1
