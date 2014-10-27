using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class ReadCursor
    {
        public DateTime Value { get; set; }

        public abstract Task Load();

        public override string ToString()
        {
            return Value.ToString("O");
        }
    }
}
