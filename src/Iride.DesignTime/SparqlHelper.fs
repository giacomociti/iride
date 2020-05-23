namespace Iride

open System.IO
open VDS.RDF.Query
open VDS.RDF.Parsing
open VDS.RDF.Parsing.Tokens
open VDS.RDF
open System

module SparqlHelper =

    type KnownDataType = Node | Iri | Literal | Integer | Number | Date | Time | Boolean

    type Variable =  { VariableName:  string; Type: KnownDataType }
    type Parameter = { ParameterName: string; Type: KnownDataType }

    type ResultVariables = { Variables: Variable list; OptionalVariables: Variable list }

    let tokens commandText = seq {
        use reader = new StringReader (commandText)
        let tokenizer = SparqlTokeniser (reader, SparqlQuerySyntax.Sparql_1_1)
        let mutable token = tokenizer.GetNextToken()
        while token.TokenType <> Token.EOF do
            yield token
            token <- tokenizer.GetNextToken()
    }
    
    let getParameterNames commandText =
        let isParameter (token: string) = token.StartsWith "$"
        let getName (token: string) = token.Substring 1
        let pars, vars =
            commandText
            |> tokens
            |> Seq.filter (fun x -> x.TokenType = Token.VARIABLE)
            |> Seq.map (fun x -> x.Value)
            |> Set.ofSeq
            |> Set.partition isParameter
        let parNames = Set.map getName pars
        let varNames = Set.map getName vars
        let ambiguous = Set.intersect parNames varNames
        if ambiguous.IsEmpty 
        then parNames
        else ambiguous 
             |> failwithf 
                "Variables %A occur with mixed prefixes.
                This is not allowed by the type provider
                because variables prefixed with '$' are interpreted
                as input parameters"

    let knownDataType (name: string) =
        match (name.Split '_').[0] with
        | "IRI" -> Iri
        | "LIT" -> Literal
        | "INT" -> Integer
        | "NUM" -> Number
        | "DATE" -> Date
        | "TIME" -> Time
        | "BOOL" -> Boolean
        | _   -> Node

    let getBindings (query: SparqlQuery) parameterNames =
        let variables variableNames =
            variableNames
            |> Seq.except parameterNames
            |> Seq.map (fun x -> { VariableName = x; Type = knownDataType x })
            |> List.ofSeq
        let algebra = query.ToAlgebra()
        { Variables = variables algebra.FixedVariables
          OptionalVariables = variables algebra.FloatingVariables }            

    let getParameters parameterNames =
        parameterNames
        |> Seq.map (fun x -> { ParameterName = x; Type = knownDataType x })
        |> List.ofSeq

    let getUnknownUris (namespaceMapper: NamespaceMapper) isUnkown sparql =
        let namespaceUris =
            namespaceMapper.Prefixes
            |> Seq.map namespaceMapper.GetNamespaceUri
            |> Seq.map (fun x -> x.AbsoluteUri)
            |> Set.ofSeq
        [
            for token in tokens sparql do
                match token.TokenType with
                | Token.URI ->
                    if not (namespaceUris.Contains token.Value) && isUnkown token.Value 
                    then yield token.Value
                | Token.QNAME ->
                    match token.Value.Split ':' with
                    | [| prefix; x |] -> 
                        let nsUri = namespaceMapper.GetNamespaceUri prefix
                        if isUnkown (Uri(nsUri, x).AbsoluteUri)
                        then yield token.Value
                    | _ -> ()
                | _ -> ()
        ]

    let checkSchema (namespaceMapper: NamespaceMapper) (sparql: string) (uris: Uri list) =
        let knownUris = uris |> List.map (fun x -> x.AbsoluteUri) |> Set.ofList
        let isUnkown uri = not (knownUris.Contains uri)
        let unknownUris = getUnknownUris namespaceMapper isUnkown sparql
        if unknownUris.Length > 0 then failwithf "Unknown Uris: %A\n Allowed: %A" unknownUris uris