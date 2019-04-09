namespace Iride

open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage
open System.Xml

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

    static member AsNode(n: INode) = n
    static member AsUri(n: INode) = (n :?> IUriNode).Uri
    static member AsString(n: INode) = (n :?> ILiteralNode).Value
    static member AsInt(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToInt32
    static member AsDecimal(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToDecimal
    static member AsDateTimeOffset(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToDateTimeOffset
    

    member __.Ask(parameterValues) =
        ((execute parameterValues) :?> SparqlResultSet).Result

    member __.Construct(parameterValues) =
        (execute parameterValues) :?> IGraph

    member __.Select(parameterValues)  = 
        ((execute parameterValues) :?> SparqlResultSet).Results |> Array.ofSeq