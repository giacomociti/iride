#I "../src/RdfSharp.DesignTime/bin/Release/netstandard2.0/"
#I "../src/RdfSharp.Runtime/bin/Release/netstandard2.0/"
#r "RdfSharp.Runtime.dll"

open RdfSharp

type T = GenerativeProvider<2>

T.StaticMethod()
