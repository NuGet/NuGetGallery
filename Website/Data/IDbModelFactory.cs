using System.Data.Entity.Infrastructure;

namespace NuGetGallery.Data
{
    public interface IDbModelFactory
    {
        DbCompiledModel CreateModel();
    }
}
