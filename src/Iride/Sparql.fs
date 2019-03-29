namespace Iride

open VDS.RDF.Parsing
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage
open VDS.RDF.Query.Algebra
open VDS.RDF.Parsing.Tokens
open System.IO


type QueryRuntime(storage: IQueryableStorage, commandText, parameterNames) =
    let sps = SparqlParameterizedString(CommandText = commandText)

    let getCommandText(parameterValues: INode array) =
        sps.ClearVariables()
        Seq.zip parameterNames parameterValues
        |> Seq.iter sps.SetVariable
        sps.ToString()

    let execute parameterValues =
        storage.Query(getCommandText(parameterValues))

    member this.Ask(parameterValues) =
        ((execute parameterValues) :?> SparqlResultSet).Result

    member this.Construct(parameterValues) =
        (execute parameterValues) :?> IGraph

    member this.Select(parameterValues)  = 
        ((execute parameterValues) :?> SparqlResultSet).Results |> Array.ofSeq



