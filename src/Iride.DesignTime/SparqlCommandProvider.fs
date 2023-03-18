module SparqlCommandProviderImplementation

open System.Reflection
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open Common
open SparqlProviderlHelper
open TypeProviderHelper
open VDS.RDF.Parsing

[<TypeProvider>]
type SparqlCommandProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let executingAssembly = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)

    let createType (typeName, sparqlCommand, rdfSchema, schemaQuery) =
        let providedAssembly = ProvidedAssembly()
        let providedType = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)

        let commandText = getSparqlText config.ResolutionFolder sparqlCommand
        let parameterNames = getParameterNames commandText
        let parameters = getParameters parameterNames
        let defaultValues = parameters |> List.map (fun x -> getDefaultValue x.Type)
        let parsedCommand = 
            CommandRuntime.GetCmdText(commandText, List.ofSeq parameterNames, List.toArray defaultValues)
            |> SparqlUpdateParser().ParseFromString

        if rdfSchema <> "" then
            GraphLoader.load config.ResolutionFolder rdfSchema
            |> getProperties schemaQuery
            |> List.map (fun x -> x.Uri)
            |> checkSchema parsedCommand.NamespaceMap commandText

        createTextMethod commandText parameters
        |> providedType.AddMember

        providedAssembly.AddTypes [providedType]
        providedType

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "SparqlCommandProvider", Some typeof<obj>, isErased=false)
        let commandText = ProvidedStaticParameter("Command", typeof<string>)
        let rdfSchema = ProvidedStaticParameter("Schema", typeof<string>, parameterDefaultValue = "")
        let schemaQuery = ProvidedStaticParameter("SchemaQuery", typeof<string>, parameterDefaultValue = SchemaQuery.RdfPropertiesAndClasses)

        result.DefineStaticParameters([commandText; rdfSchema; schemaQuery], fun typeName args -> 
            createType (typeName, string args.[0], string args.[1], string args.[2]))

        result.AddXmlDoc """<summary>SPARQL parametrized command.</summary>
           <param name='Command'>Command text. Variables prefixed with '$' are treated as input parameters.</param>
           <param name='Schema'>RDF vocabulary to limit allowed IRIs in the command.</param>
           <param name='SchemaQuery'>SPARQL query to extract the vocabulary. Default to classes and properties.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])