using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class GraphHelpers
    {
        public static void MaterializeInference(IGraph graph)
        {
            //  hard code some type inference

            //  nuget:MicroservicePackage rdfs:subClassOf nuget:PackageDetails

            foreach (Triple triple in graph.GetTriplesWithPredicateObject(graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.MicroservicePackage)))
            {
                graph.Assert(triple.Subject, triple.Predicate, graph.CreateUriNode(Schema.DataTypes.PackageDetails));
            }

            //  nuget:PowerShellPackage rdfs:subClassOf nuget:PackageDetails

            foreach (Triple triple in graph.GetTriplesWithPredicateObject(graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.PowerShellPackage)))
            {
                graph.Assert(triple.Subject, triple.Predicate, graph.CreateUriNode(Schema.DataTypes.PackageDetails));
            }
        }
    }
}
