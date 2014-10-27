using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class ReadWriteCursor : ReadCursor
    {
        public abstract Task Save();
    }
}
