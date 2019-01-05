module RdfSharpTests


open MyNamespace
open NUnit.Framework


type Generative2 = RdfSharp.GenerativeProvider<2>
type Generative4 = RdfSharp.GenerativeProvider<4>

[<Test>]
let ``Can access properties of generative provider 2`` () =
    let obj = Generative2()
    Assert.AreEqual(obj.Property1, 1)
    Assert.AreEqual(obj.Property2, 2)

[<Test>]
let ``Can access properties of generative provider 4`` () =
    let obj = Generative4()
    Assert.AreEqual(obj.Property1, 1)
    Assert.AreEqual(obj.Property2, 2)
    Assert.AreEqual(obj.Property3, 3)
    Assert.AreEqual(obj.Property4, 4)

