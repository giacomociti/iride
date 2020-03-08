namespace Iride

module CommonUris =

    [<Literal>]
    let Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns"

    [<Literal>]
    let Rdfs = "http://www.w3.org/2000/01/rdf-schema"

    [<Literal>]
    let Owl = "http://www.w3.org/2002/07/owl"

module SchemaQuery =

    [<Literal>]
    let RdfResources = """
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri rdfs:label ?label ;
               rdfs:comment ?comment .
        }
        """

    [<Literal>]
    let RdfProperties = """
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdf:Property
          OPTIONAL { ?uri rdfs:label ?label }
          OPTIONAL { ?uri rdfs:comment ?comment }
        }
        """

    [<Literal>]
    let RdfsClasses = """
        PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

        SELECT ?uri ?label ?comment WHERE {
          ?uri a rdfs:Class 
          OPTIONAL { ?uri rdfs:label ?label }
          OPTIONAL { ?uri rdfs:comment ?comment }
        }
        """

    [<Literal>]
    let RdfPropertiesAndClasses = """
       PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> 
       PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#> 
       
       SELECT ?uri ?label ?comment WHERE {
           ?uri a ?x 
           VALUES (?x) { (rdf:Property) (rdfs:Class) }
           OPTIONAL { ?uri rdfs:label ?label }
           OPTIONAL { ?uri rdfs:comment ?comment }
       }
       """

// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("Iride.DesignTime.dll")>]
do ()