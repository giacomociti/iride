#I "../src/RdfSharp.DesignTime/bin/Release/netstandard2.0/"
#I "../src/RdfSharp.Runtime/bin/Release/netstandard2.0/"
#r "RdfSharp.Runtime.dll"

open RdfSharp

type Rdf = RdfPropertyProvider<"""http://www.w3.org/1999/02/22-rdf-syntax-ns""">

type Rdfs = RdfPropertyProvider<"""http://www.w3.org/2000/01/rdf-schema""">

type Owl = RdfPropertyProvider<"""http://www.w3.org/2002/07/owl""">

type Book = RdfPropertyProvider<"""https://schema.org/Book.ttl""">

Rdf.subject.Fragment
Rdfs.label.Fragment
Owl.cardinality.Fragment
Book.bookFormat.ToString()

