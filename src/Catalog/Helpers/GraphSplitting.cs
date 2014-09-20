using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public static class GraphSplitting
    {
        public static Uri GetPackageRegistrationUri(IGraph graph)
        {
            return ((IUriNode)graph.GetTriplesWithPredicateObject(
                graph.CreateUriNode(new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type")),
                graph.CreateUriNode(new Uri("http://schema.nuget.org/schema#PackageRegistration")))
                .First().Subject).Uri;
        }
        public static IList<Uri> GetResources(IGraph graph)
        {
            IList<Uri> resources = new List<Uri>();

            INode rdfType = graph.CreateUriNode(new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type"));

            Uri[] types = new Uri[]
            {
                //new Uri("http://schema.nuget.org/schema#PackageRegistration"), 
                new Uri("http://schema.nuget.org/schema#PackageList")
            };

            foreach (Uri type in types)
            {
                foreach (Triple triple in graph.GetTriplesWithPredicateObject(rdfType, graph.CreateUriNode(type)))
                {
                    resources.Add(((IUriNode)triple.Subject).Uri);
                }
            }

            return resources;
        }

        public static IGraph ReplaceResourceUris(IGraph original, IDictionary<string, Uri> replacements)
        {
            IGraph modified = new Graph();
            foreach (Triple triple in original.Triples)
            {
                Uri subjectUri;
                if (!replacements.TryGetValue(triple.Subject.ToString(), out subjectUri))
                {
                    subjectUri = ((IUriNode)triple.Subject).Uri;
                }

                INode subjectNode = modified.CreateUriNode(subjectUri);
                INode predicateNode = triple.Predicate.CopyNode(modified);

                INode objectNode;
                if (triple.Object is IUriNode)
                {
                    Uri objectUri;
                    if (!replacements.TryGetValue(triple.Object.ToString(), out objectUri))
                    {
                        objectUri = ((IUriNode)triple.Object).Uri;
                    }
                    objectNode = modified.CreateUriNode(objectUri);
                }
                else
                {
                    objectNode = triple.Object.CopyNode(modified);
                }

                modified.Assert(subjectNode, predicateNode, objectNode);
            }

            return modified;
        }
        public static void Collect(IGraph source, INode subject, IGraph destination, ISet<string> exclude)
        {
            foreach (Triple triple in source.GetTriplesWithSubject(subject))
            {
                destination.Assert(triple.CopyTriple(destination));

                if (triple.Object is IUriNode && !exclude.Contains(((IUriNode)triple.Object).Uri.ToString()))
                {
                    Collect(source, triple.Object, destination, exclude);
                }
            }
        }

        static Uri RebaseUri(Uri nodeUri, Uri sourceUri, Uri destinationUri)
        {
            if (nodeUri == sourceUri && nodeUri.ToString() != sourceUri.ToString())
            {
                return new Uri(destinationUri.ToString() + nodeUri.Fragment);
            }
            return nodeUri;
        }
        public static void Rebase(IGraph source, IGraph destination, Uri sourceUri, Uri destinationUri)
        {
            Uri modifiedDestinationUri = new Uri(destinationUri.ToString().Replace('#', '/'));

            foreach (Triple triple in source.Triples)
            {
                Uri subjectUri;
                if (triple.Subject.ToString() == destinationUri.ToString())
                {
                    subjectUri = modifiedDestinationUri;
                }
                else
                {
                    subjectUri = RebaseUri(((IUriNode)triple.Subject).Uri, sourceUri, modifiedDestinationUri);
                }

                INode subjectNode = destination.CreateUriNode(subjectUri);
                INode predicateNode = triple.Predicate.CopyNode(destination);

                INode objectNode;
                if (triple.Object is IUriNode)
                {
                    Uri objectUri = RebaseUri(((IUriNode)triple.Object).Uri, sourceUri, modifiedDestinationUri);
                    objectNode = destination.CreateUriNode(objectUri);
                }
                else
                {
                    objectNode = triple.Object.CopyNode(destination);
                }

                destination.Assert(subjectNode, predicateNode, objectNode);
            }
        }

        static IDictionary<string, Uri> CreateReplacements(Uri originalUri, IList<Uri> resources)
        {
            IDictionary<string, Uri> replacements = new Dictionary<string, Uri>();

            foreach (Uri resource in resources)
            {
                if (resource == originalUri)
                {
                    string oldUri = resource.ToString();
                    string newUri = oldUri.Replace(".json#", "/");

                    if (!newUri.EndsWith(".json"))
                    {
                        newUri += ".json";
                    }

                    replacements.Add(oldUri, new Uri(newUri));
                }
            }

            return replacements;
        }

        public static IDictionary<Uri, IGraph> Split(Uri originalUri, IGraph originalGraph)
        {
            IList<Uri> resources = GraphSplitting.GetResources(originalGraph);

            IDictionary<string, Uri> replacements = CreateReplacements(originalUri, resources);

            ISet<string> exclude = new HashSet<string>();
            exclude.Add(originalUri.ToString());

            IGraph modified = GraphSplitting.ReplaceResourceUris(originalGraph, replacements);

            IDictionary<Uri, IGraph> graphs = new Dictionary<Uri, IGraph>();

            IGraph parent = new Graph();

            foreach (Triple triple in modified.Triples)
            {
                triple.CopyTriple(parent);
                parent.Assert(triple);
            }

            foreach (Uri uri in replacements.Values)
            {
                INode subject = modified.CreateUriNode(uri);

                IGraph cutGraph = new Graph();
                GraphSplitting.Collect(modified, subject, cutGraph, exclude);

                foreach (Triple triple in cutGraph.Triples)
                {
                    triple.CopyTriple(parent);
                    parent.Retract(triple);
                }

                IGraph rebasedGraph = new Graph();
                GraphSplitting.Rebase(cutGraph, rebasedGraph, originalUri, uri);

                graphs.Add(uri, rebasedGraph);
            }

            graphs.Add(originalUri, parent);

            return graphs;
        }
    }
}
