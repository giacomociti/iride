module IrideSparqlTests

open NUnit.Framework
open Iride
open VDS.RDF.Storage
open VDS.RDF

type Ask = SparqlCommand<"ASK WHERE {?s ?p $o}">

[<Test>]
let ``Can ask`` () =
    let storage = new InMemoryManager()
    let cmd = Ask(storage)
    let aa = NodeFactory().CreateLiteralNode("aa")
    let bb = NodeFactory().CreateLiteralNode("bb")
    //Assert.True(cmd.Run(aa))
    Assert.False(cmd.Run(bb))

type Construct = SparqlCommand<"CONSTRUCT {?s ?p $o} WHERE {?s ?p $o}">

[<Test>]
let ``Can constuct`` () =
    let storage = new InMemoryManager()
    let cmd = Construct(storage)
    let aa = NodeFactory().CreateLiteralNode("aa")
    Assert.True(cmd.Run(aa).IsEmpty)

type Select = SparqlCommand<"SELECT * WHERE {?s ?p $o}">

[<Test>]
let ``Can select`` () =
    let storage = new InMemoryManager()
    let cmd = Select(storage)
    let aa = NodeFactory().CreateLiteralNode("aa")
    Assert.True(cmd.Run(aa).Length=0)
