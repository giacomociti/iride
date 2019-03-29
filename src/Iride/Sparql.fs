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

    member __.Ask(parameterValues) =
        ((execute parameterValues) :?> SparqlResultSet).Result

    member __.Construct(parameterValues) =
        (execute parameterValues) :?> IGraph

    member __.Select(parameterValues)  = 
        ((execute parameterValues) :?> SparqlResultSet).Results |> Array.ofSeq