namespace Iride

open System
open System.IO
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Parsing
open VDS.RDF.Parsing.Tokens
open Common

module SparqlProviderlHelper =

    type Variable =  { VariableName:  string; Type: KnownDataType }
    type Parameter = { ParameterName: string; Type: KnownDataType }

    type ResultVariables = { Variables: Variable list; OptionalVariables: Variable list }

    let getSparqlText resolutionFolder (sparqlParameter: string) =
        if sparqlParameter.EndsWith ".rq"
        then
            System.IO.Path.Combine(resolutionFolder, sparqlParameter)
            |> System.IO.File.ReadAllText
        else sparqlParameter

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
        | "IRI" -> KnownDataType.Iri
        | "LIT" -> KnownDataType.Literal
        | "INT" -> KnownDataType.Integer
        | "NUM" -> KnownDataType.Number
        | "DATE" -> KnownDataType.Date
        | "TIME" -> KnownDataType.Time
        | "BOOL" -> KnownDataType.Boolean
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

    let getUnknownUris (namespaceMapper: INamespaceMapper) isUnkown sparql =
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

    let checkSchema (namespaceMapper: INamespaceMapper) (sparql: string) (uris: Uri list) =
        let knownUris = uris |> List.map (fun x -> x.AbsoluteUri) |> Set.ofList
        let isUnkown uri = not (knownUris.Contains uri)
        let unknownUris = getUnknownUris namespaceMapper isUnkown sparql
        if unknownUris.Length > 0 then failwithf "Unknown Uris: %A\n Allowed: %A" unknownUris uris