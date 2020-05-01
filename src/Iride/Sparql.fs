namespace Iride

open VDS.RDF
open VDS.RDF.Query
open System.Xml

type CommandRuntime =
    static member NodeFactory = NodeFactory()        

    static member GetCmdText(commandText: string, parameterNames: string list, parameterValues: INode array) =
        let sps = SparqlParameterizedString commandText
        Seq.zip parameterNames parameterValues
        |> Seq.iter sps.SetVariable
        sps.ToString()

    static member ToNode(n: INode) = n
    static member ToNode(u: System.Uri) = CommandRuntime.NodeFactory.CreateUriNode(u) :> INode
    static member ToNode(s: string) = CommandRuntime.NodeFactory.CreateLiteralNode(s) :> INode
    static member ToNode(n: int) = n.ToLiteral(CommandRuntime.NodeFactory) :> INode
    static member ToNode(d: decimal) = d.ToLiteral(CommandRuntime.NodeFactory) :> INode
    static member ToNode(t: System.DateTime) : INode = CommandRuntime.NodeFactory.CreateLiteralNode(t.ToString("yyyy-MM-dd")) :> INode
    static member ToNode(t: System.DateTimeOffset) : INode = t.ToLiteral(CommandRuntime.NodeFactory) :> INode
    static member ToNode(t: bool) : INode = t.ToLiteral(CommandRuntime.NodeFactory) :> INode

    static member AsNode(n: INode) = n
    static member AsUri(n: INode) = (n :?> IUriNode).Uri
    static member AsString(n: INode) = (n :?> ILiteralNode).Value
    static member AsInt(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToInt32
    static member AsDecimal(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToDecimal
    static member AsDateTime(n: INode) = XmlConvert.ToDateTime((n :?> ILiteralNode).Value, "yyyy-MM-dd")
    static member AsDateTimeOffset(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToDateTimeOffset
    static member AsBoolean(n: INode) = (n :?> ILiteralNode).Value |> XmlConvert.ToBoolean