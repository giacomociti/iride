module IrideImplementation

open System
open System.Reflection
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
    do assert (typeof<Iride.UriFactory>.Assembly.GetName().Name = asm.GetName().Name)  

    let createType typeName (rdfSchemaUri: string) (sparqlQuery) =
        let asm = ProvidedAssembly()
        let result = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)
        
        for property in RdfHelper.getGraphProperties rdfSchemaUri sparqlQuery do
            let uri = property.Uri.ToString()
            let providedProperty = 
                ProvidedProperty(
                    propertyName = property.Label, 
                    propertyType = typeof<Uri>,
                    getterCode = (fun _ -> <@@ Uri uri @@>), 
                    isStatic = true)

            sprintf "%s\n%s" uri property.Comment
            |> providedProperty.AddXmlDoc
            
            result.AddMember providedProperty
 
        asm.AddTypes [ result ]
        result

    let myParamType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "UriProvider", Some typeof<obj>, isErased = false)
        result.DefineStaticParameters([
                ProvidedStaticParameter("RdfSchemaUri", typeof<string>)
                ProvidedStaticParameter("SparqlQuery", typeof<string>, Query.RdfProperties)
            ],
            fun typeName args -> createType typeName (string args.[0]) (string args.[1])  )

        result.AddXmlDoc """<summary>Uri properties from IRIs in RDF ontologies.</summary>
           <param name='RdfSchemaUri'>RDF ontology where to look for IRIs.</param>
           <param name='SparqlQuery'>SPARQL query to extract IRIs with their label and comment.</param>
         """
        result

    do this.AddNamespace(ns, [myParamType])


[<TypeProviderAssembly>]
do ()
