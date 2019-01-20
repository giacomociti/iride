
This is a simple F# type provider to create `System.Uri` properties
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

## Building
The type provider has separate design-time and runtime assemblies.

Paket is used to acquire the type provider SDK and build the nuget package.



    .paket/paket.exe update

    dotnet build -c release

    .paket/paket.exe pack src/Iride --version 0.0.1
