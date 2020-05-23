module UriProviderImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride
open VDS.RDF

[<TypeProvider>]
type UriProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let executingAssembly = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<CommandRuntime>.Assembly.GetName().Name = executingAssembly.GetName().Name)  

    let createType (typeName, schema, schemaQuery, allValuesMethod) =
        let providedAssembly = ProvidedAssembly()
        let result = ProvidedTypeDefinition(providedAssembly, ns, typeName, Some typeof<obj>, isErased=false)
        
        let providedProperties = [
            for property in RdfHelper.getGraphProperties config.ResolutionFolder schema schemaQuery do
                let uri = property.Uri.AbsoluteUri
                let providedProperty = 
                    ProvidedProperty(
                        propertyName = property.Label, 
                        propertyType = typeof<Uri>,
                        getterCode = (fun _ -> <@@ UriFactory.Create uri @@>),
                        isStatic = true)
                providedProperty.AddXmlDoc property.Comment
                yield providedProperty
        ]

        result.AddMembers providedProperties

        if not (String.IsNullOrWhiteSpace allValuesMethod) then
            let providedMethod =
                ProvidedMethod(
                    methodName = allValuesMethod,
                    parameters = [],
                    returnType = typeof<Uri[]>,
                    invokeCode = (fun _ ->
                        let items = providedProperties |> List.map Expr.PropertyGet
                        Expr.NewArray(typeof<Uri>, items)),
                    isStatic = true)
            result.AddMember providedMethod

        providedAssembly.AddTypes [ result ]
        result

    let providerType = 
        let result = ProvidedTypeDefinition(executingAssembly, ns, "UriProvider", Some typeof<obj>, isErased=false)
        result.DefineStaticParameters([
                ProvidedStaticParameter("Schema", typeof<string>)
                ProvidedStaticParameter("SchemaQuery", typeof<string>, SchemaQuery.RdfResources)
                ProvidedStaticParameter("AllValuesMethod", typeof<string>, "")
            ],
            fun typeName args -> createType (typeName, string args.[0], string args.[1], string args.[2]) )

        result.AddXmlDoc """<summary>Uri properties from IRIs in RDF vocabularies.</summary>
           <param name='Schema'>RDF vocabulary where to look for IRIs.</param>
           <param name='SchemaQuery'>SPARQL query to extract IRIs from Schema. Default to resources with label and comment.</param>
           <param name='AllValuesMethod'>Name of method listing all Uri values.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])

[<TypeProviderAssembly>]
do ()
