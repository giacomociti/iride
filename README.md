
This is a simple F# type provider to create Uri properties
from RDF properties in ontologies.
It has separate design-time and runtime assemblies.

Paket is used to acquire the type provider SDK and build the nuget package (you can remove this use of paket if you like)

Building:

    .paket/paket.exe update

    dotnet build -c release

    .paket/paket.exe pack src/RdfSharp.Runtime --version 0.0.1