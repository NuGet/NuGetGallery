using System.Collections.Generic;

namespace NuGetGallery.Data
{
    public interface IDbMigrator
    {
        void Update();
        void Update(string targetMigration);
        IEnumerable<string> GetDatabaseMigrations();
        IEnumerable<string> GetPendingMigrations();
        IEnumerable<string> GetLocalMigrations();
    }
}
