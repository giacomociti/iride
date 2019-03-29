namespace Iride

open System.IO
open VDS.RDF.Query
open VDS.RDF.Parsing
open VDS.RDF.Parsing.Tokens

module SparqlHelper =

    let tokens commandText = seq {
        use reader = new StringReader (commandText)
        let tokenizer = SparqlTokeniser (reader, SparqlQuerySyntax.Sparql_1_1)
        let mutable token = tokenizer.GetNextToken()
        while token.TokenType <> Token.EOF do
            yield token
            token <- tokenizer.GetNextToken()
    }

    let getParameterNames commandText =
        commandText
        |> tokens
        |> Seq.filter (fun x -> x.TokenType = Token.VARIABLE && x.Value.[0] = '$')
        |> Seq.map (fun x -> x.Value.Substring 1)
        |> Seq.distinct
        |> List.ofSeq

    type ResultType = 
        | Boolean 
        | Graph 
        | Bindings of variables: string list * optionalVariables: string list

    type QueryDescriptor = {
        commandText: string
        parameterNames: string list
        resultType: ResultType
    }

    let getQueryDescriptor commandText =
        let parameterNames = getParameterNames commandText
        { 
            commandText = commandText
            parameterNames = parameterNames
            resultType =
                let query = SparqlQueryParser().ParseFromString(commandText)
                match query.QueryType with
                | SparqlQueryType.Ask -> Boolean
                | SparqlQueryType.Construct
                | SparqlQueryType.Describe
                | SparqlQueryType.DescribeAll -> Graph
                | _ -> 
                    let algebra = query.ToAlgebra()
                    let variables = algebra.FixedVariables |> Seq.except parameterNames
                    let optionalVariables = algebra.FloatingVariables |> Seq.except parameterNames
                    Bindings (List.ofSeq variables, List.ofSeq optionalVariables)
        }