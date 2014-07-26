using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class IdVersionKeyEntry : IdKeyEntry
    {
        public IdVersionKeyEntry(string id, string version, string description, string registrationBaseAddress)
            : base(id, version, description, registrationBaseAddress)
        {
        }

        public override string Key
        {
            get { return Id + "." + Version; }
        }
    }
}
