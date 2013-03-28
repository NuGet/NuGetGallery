using System.Collections.Generic;
using System.Data.Entity.Migrations;

namespace NuGetGallery.Data
{
    public class DbMigratorWrapper : IDbMigrator
    {
        private readonly DbMigrator _migrator;

        public DbMigratorWrapper(DbMigrator migrator)
        {
            _migrator = migrator;
        }

        public void Update()
        {
            _migrator.Update();
        }

        public void Update(string targetMigration)
        {
            _migrator.Update(targetMigration);
        }

        public IEnumerable<string> GetDatabaseMigrations()
        {
            return _migrator.GetDatabaseMigrations();
        }

        public IEnumerable<string> GetPendingMigrations()
        {
            return _migrator.GetPendingMigrations();
        }

        public IEnumerable<string> GetLocalMigrations()
        {
            return _migrator.GetLocalMigrations();
        }
    }
}