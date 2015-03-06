using System;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationKey
    {
        public RegistrationKey(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
            
        public override string ToString()
        {
            return Id.ToLowerInvariant();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            RegistrationKey rhs = obj as RegistrationKey;

            if (rhs == null)
            {
                return false;
            }

            return (Id == rhs.Id); 
        }

        public static RegistrationKey Promote(string resourceUri, IGraph graph)
        {
            INode subject = graph.CreateUriNode(new Uri(resourceUri));
            string id = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Id)).First().Object.ToString();

            return new RegistrationKey(id);
        }
    }
}
