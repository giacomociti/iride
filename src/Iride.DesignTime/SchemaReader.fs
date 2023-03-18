namespace Iride

open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Parsing
open Iride.Common
open System

type SchemaReader(graph: IGraph, classQuery, propertyQuery) =

    let propertyParametrizedQuery = SparqlParameterizedString propertyQuery

    let label (graph: IGraph) (subject: INode) = 
        let labelNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#label")
        graph.GetTriplesWithSubjectPredicate(subject, labelNode)
        |> Seq.tryHead
        |> Option.map (fun t -> (t.Object :?> ILiteralNode).Value)
        |> Option.defaultWith (fun () -> getName subject.Uri) 

    let comment (graph: IGraph) (subject: INode) =
        let commentNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#comment")
        graph.GetTriplesWithSubjectPredicate(subject, commentNode)
        |> Seq.tryHead
        |> Option.map (fun t -> (t.Object :?> ILiteralNode).Value)
        |> Option.defaultWith (fun () -> subject.Uri.ToString()) 

    let classes (graph: IGraph) =
        let typeNode = graph.CreateUriNode(UriFactory.Create RdfSpecsHelper.RdfType)
        let owlNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2002/07/owl#Class")
        let classNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#Class")
        graph.GetTriplesWithPredicateObject(typeNode, classNode)
        |> Seq.append (graph.GetTriplesWithPredicateObject(typeNode, owlNode))
        |> Seq.filter (fun x -> x.Subject.NodeType = NodeType.Uri)
        |> Seq.map (fun x ->
            {| Uri = x.Subject.Uri; Label = label graph x.Subject; Comment = comment graph x.Subject |})

    let properties (classUri: Uri) (graph: IGraph)  =
        let domainNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#domain")
        let rangeNode = graph.CreateUriNode(UriFactory.Create "http://www.w3.org/2000/01/rdf-schema#range")
        graph.GetTriplesWithPredicateObject(domainNode, graph.CreateUriNode(classUri))
        |> Seq.map (fun x ->
            let ranges = 
                graph.GetTriplesWithSubjectPredicate(x.Subject, rangeNode)
                |> Seq.map (fun t -> t.Object)
                |> Seq.toList
            {| Uri = x.Subject.Uri
               Range = 
                match ranges with
                | [ r ] when r.NodeType = NodeType.Uri -> r.Uri
                | _ -> Uri "http://www.w3.org/2000/01/rdf-schema#Resource"
               Label = label graph x.Subject
               Comment = comment graph x.Subject |})

    member _.GetClasses () =
        if classQuery = "" then graph else graph.ExecuteQuery(classQuery) :?> IGraph
        |> classes

    member _.GetProperties (classUri: Uri) =
        if propertyQuery = "" 
        then graph 
        else 
            propertyParametrizedQuery.SetUri("domain", classUri)
            graph.ExecuteQuery(propertyParametrizedQuery) :?> IGraph
        |> properties classUri