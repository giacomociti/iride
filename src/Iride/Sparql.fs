namespace Iride

open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage

type QueryRuntime(storage: IQueryableStorage, commandText, parameterNames) =
    let sps = SparqlParameterizedString(CommandText = commandText)

    let getCommandText(parameterValues: INode array) =
        sps.ClearVariables()
        Seq.zip parameterNames parameterValues
        |> Seq.iter sps.SetVariable
        sps.ToString()

    let execute parameterValues =
        storage.Query(getCommandText(parameterValues))

    static member ToNode(n: INode) = n
    static member ToNode(u: System.Uri) = NodeFactory().CreateUriNode(u) :> INode
    static member ToNode(s: string) = NodeFactory().CreateLiteralNode(s) :> INode
    static member ToNode(n: int) = n.ToLiteral(NodeFactory()) :> INode
    static member ToNode(d: decimal) = d.ToLiteral(NodeFactory()) :> INode
    static member ToNode(t: System.DateTimeOffset) : INode = t.ToLiteral(NodeFactory()) :> INode

    member __.Ask(parameterValues) =
        ((execute parameterValues) :?> SparqlResultSet).Result

    member __.Construct(parameterValues) =
        (execute parameterValues) :?> IGraph

    member __.Select(parameterValues)  = 
        ((execute parameterValues) :?> SparqlResultSet).Results |> Array.ofSeq