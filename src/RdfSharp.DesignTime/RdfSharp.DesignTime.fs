module RdfSharpImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open FSharp.Quotations
open FSharp.Core.CompilerServices
open MyNamespace
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open RdfHelper

// Put any utility helpers here
[<AutoOpen>]
module internal Helpers =
    let x = 1

[<TypeProvider>]
type BasicGenerativeProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("RdfSharp.DesignTime", "RdfSharp.Runtime")])

    let ns = "RdfSharp"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)  

    let createType typeName (rdfSchemaUri: string) (sparqlQuery) =
        let asm = ProvidedAssembly()
        let result = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)
        
        for property in withGraph (getProperties sparqlQuery) rdfSchemaUri do
            let uri = property.Uri.ToString()
            let providedProperty = 
                ProvidedProperty(
                    propertyName = property.Label, 
                    propertyType = typeof<Uri>,
                    getterCode = (fun _ -> <@@ Uri uri @@>), 
                    isStatic = true)
            providedProperty.AddXmlDoc property.Comment
            result.AddMember providedProperty
 
        asm.AddTypes [ result ]
        result

    let myParamType = 
        let result =
            ProvidedTypeDefinition(asm, ns, "RdfPropertyProvider", Some typeof<obj>, isErased = false)
        result.DefineStaticParameters([
                ProvidedStaticParameter("RdfSchemaUri", typeof<string>)
                ProvidedStaticParameter("SparqlQuery", typeof<string>, RdfHelper.properties)
            ],
            fun typeName args -> createType typeName (string args.[0]) (string args.[1])  )
        result

    do
        this.AddNamespace(ns, [myParamType])


[<TypeProviderAssembly>]
do ()
