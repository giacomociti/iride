#I "../src/RdfSharp.DesignTime/bin/Release/netstandard2.0/"
#I "../src/RdfSharp.Runtime/bin/Release/netstandard2.0/"
#r "RdfSharp.Runtime.dll"

open RdfSharp

type Rdfs = RdfPropertyProvider<"""http://www.w3.org/2000/01/rdf-schema""">
Rdfs.label.ToString()
