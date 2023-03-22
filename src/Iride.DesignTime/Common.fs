namespace Iride

module Common =

    type KnownDataType = Node | Iri | Literal | Integer | Number | Date | Time | Boolean

    let knownDataType = function
    | "http://www.w3.org/2001/XMLSchema#string"
    | "http://www.w3.org/2000/01/rdf-schema#Literal"
    | "http://schema.org/Text" -> Literal
    | "http://www.w3.org/2001/XMLSchema#integer"
    | "http://schema.org/Integer" -> Integer
    | "http://www.w3.org/2001/XMLSchema#date"
    | "http://schema.org/Date" -> Date
    | "http://www.w3.org/2001/XMLSchema#dateTime"
    | "http://schema.org/DateTime"-> Time
    | "http://www.w3.org/2001/XMLSchema#decimal"
    | "http://schema.org/Number" -> Number
    | "http://www.w3.org/2001/XMLSchema#boolean"
    | "http://schema.org/Boolean" -> Boolean
    | _ -> Node
