namespace Iride

open System
open System.IO
open VDS.RDF.Parsing
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage
open VDS.RDF.Query.Algebra
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
        
    let variables tokens =
        tokens
        |> Seq.filter (fun (x: IToken) -> x.TokenType = Token.VARIABLE)
        |> Seq.map (fun x -> x.Value)

    let getParameters commandText =
        commandText
        |> tokens
        |> variables
        |> Seq.distinct
        |> Seq.filter (fun x -> x.[0] = '$')

    type ResultType = 
        | Boolean 
        | Graph 
        | Bindings of variables: string list * optionalVariables: string list

    type QueryDescriptor = {
        commandText: string
        parameterNames: string list
        resultType: ResultType
    }

    let getQueryDescriptor (commandText: string) =
        let query = SparqlQueryParser().ParseFromString(commandText)
        let algebra = query.ToAlgebra()
        let parameters = getParameters commandText
        { 
            commandText = commandText
            parameterNames = List.ofSeq parameters
            resultType =
                match query.QueryType with
                | SparqlQueryType.Ask -> Boolean
                | SparqlQueryType.Construct
                | SparqlQueryType.Describe
                | SparqlQueryType.DescribeAll -> Graph
                | _ -> 
                    let variables = algebra.FixedVariables |> Seq.except parameters
                    let optionalVariables = algebra.FloatingVariables |> Seq.except parameters
                    Bindings (List.ofSeq variables, List.ofSeq optionalVariables)
        }        