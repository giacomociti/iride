module IrideImplementation

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Iride


[<TypeProvider>]
type BasicGenerativeProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces 
        (config, 
         assemblyReplacementMap = [("Iride.DesignTime", "Iride")],
         addDefaultProbingLocation = true)
    let ns = "Iride"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<Iride.IMarker>.Assembly.GetName().Name = asm.GetName().Name)  

    let createType typeName (rdfSchemaUri: string) sparqlQuery allValuesMethod =
        let asm = ProvidedAssembly()
        let result = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)
        
        let providedProperties = [
            for prop in RdfHelper.getGraphProperties config.ResolutionFolder rdfSchemaUri sparqlQuery do
                let uri = prop.Uri.ToString()
                let providedProperty = 
                    ProvidedProperty(
                        propertyName = prop.Label, 
                        propertyType = typeof<Uri>,
                        getterCode = (fun _ -> <@@ Uri uri @@>), 
                        isStatic = true)
                providedProperty.AddXmlDoc prop.Comment
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

        asm.AddTypes [ result ]
        result

    let providerType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "UriProvider", Some typeof<obj>, isErased = false)
        result.DefineStaticParameters([
                ProvidedStaticParameter("RdfSchemaUri", typeof<string>)
                ProvidedStaticParameter("SparqlQuery", typeof<string>, Query.RdfResources)
                ProvidedStaticParameter("AllValuesMethod", typeof<string>, "")
            ],
            fun typeName args -> createType typeName (string args.[0]) (string args.[1]) (string args.[2]) )

        result.AddXmlDoc """<summary>Uri properties from IRIs in RDF vocabularies.</summary>
           <param name='RdfSchemaUri'>RDF vocabulary where to look for IRIs.</param>
           <param name='SparqlQuery'>SPARQL query to extract IRIs with their label and comment.</param>
           <param name='AllValuesMethod'>Name of method listing all Uri values.</param>
         """
        result

    do this.AddNamespace(ns, [providerType])


[<TypeProviderAssembly>]
do ()
