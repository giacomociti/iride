namespace Iride

open System.IO
open VDS.RDF.Query
open VDS.RDF.Parsing
open VDS.RDF.Parsing.Tokens

module SparqlHelper =

    type KnownDataType = Node | Iri | Literal | Integer | Number | Date | Time

    type Variable =  { VariableName:  string; Type: KnownDataType }
    type Parameter = { ParameterName: string; Type: KnownDataType }

    type ResultVariables = { 
        Variables: Variable list
        OptionalVariables: Variable list }

    type QueryResult = Boolean | Graph | Bindings of ResultVariables

    type QueryDescriptor = { 
        commandText: string
        input: Parameter list
        output: QueryResult }

    let tokens commandText = seq {
        use reader = new StringReader (commandText)
        let tokenizer = SparqlTokeniser (reader, SparqlQuerySyntax.Sparql_1_1)
        let mutable token = tokenizer.GetNextToken()
        while token.TokenType <> Token.EOF do
            yield token
            token <- tokenizer.GetNextToken()
    }
    
    let parameterNames commandText =
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
        | _   -> Node

    let bindings (query: SparqlQuery) parameterNames =
        let variables variableNames =
            variableNames
            |> Seq.except parameterNames
            |> Seq.map (fun x -> { VariableName = x; Type = knownDataType x })
            |> List.ofSeq
        let algebra = query.ToAlgebra()
        Bindings {
            Variables = variables algebra.FixedVariables
            OptionalVariables = variables algebra.FloatingVariables }            

    let queryDescriptor commandText =
        let pars = parameterNames commandText
        { 
            commandText = commandText
            input =
                pars
                |> Seq.map (fun x -> { ParameterName = x; Type = knownDataType x })
                |> List.ofSeq
            output =
                let query = SparqlQueryParser().ParseFromString(commandText)
                match query.QueryType with
                | SparqlQueryType.Ask -> Boolean
                | SparqlQueryType.Construct
                | SparqlQueryType.Describe
                | SparqlQueryType.DescribeAll -> Graph
                | _ -> bindings query pars
        }