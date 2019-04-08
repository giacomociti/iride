namespace Iride

open System.IO
open VDS.RDF
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
    
    type Parameter = { ParameterName: string; Type: System.Type }
    type Variable = { VariableName: string; Type: System.Type }

    let getType (name: string) =
        if   name.StartsWith "u_" then typeof<System.Uri>
        elif name.StartsWith "s_" then typeof<string>
        elif name.StartsWith "i_" then typeof<int>
        elif name.StartsWith "d_" then typeof<decimal>
        elif name.StartsWith "t_" then typeof<System.DateTimeOffset>
        else typeof<INode>

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



    type ResultType = 
        | Boolean 
        | Graph 
        | Bindings of variables: Variable list * optionalVariables: Variable list

    type QueryDescriptor = {
        commandText: string
        parameters: Parameter list
        resultType: ResultType
    }

    let getQueryDescriptor commandText =
        let pars = getParameterNames commandText
        { 
            commandText = commandText
            parameters =
                pars
                |> Seq.map (fun x -> {ParameterName = x; Type = getType x})
                |> List.ofSeq
            resultType =
                let query = SparqlQueryParser().ParseFromString(commandText)
                match query.QueryType with
                | SparqlQueryType.Ask -> Boolean
                | SparqlQueryType.Construct
                | SparqlQueryType.Describe
                | SparqlQueryType.DescribeAll -> Graph
                | _ ->
                    let algebra = query.ToAlgebra()
                    let variables =
                        algebra.FixedVariables
                        |> Seq.except pars
                        |> Seq.map (fun x -> {VariableName = x; Type = getType x})
                        |> List.ofSeq
                    let optionalVariables =
                        algebra.FloatingVariables
                        |> Seq.except pars
                        |> Seq.map (fun x -> {VariableName = x; Type = getType x})
                        |> List.ofSeq

                    Bindings (variables, optionalVariables)
        }