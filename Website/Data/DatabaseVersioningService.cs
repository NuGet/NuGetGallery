using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using NuGetGallery.Data.Migrations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery.Data
{
    public class DatabaseVersioningService : IDatabaseVersioningService
    {
        private static string _minimumMigrationId;
        private static readonly Type MinimumMigration = typeof(AddMinRequiredVersionColumn);
        private static readonly Regex MigrationIdRegex = new Regex(@"(?<date>\d{15})_(?<name>.*)");

        private static string MinimumMigrationId
        {
            get { return _minimumMigrationId ?? (_minimumMigrationId = MigrationUtils.GetMigrationId(MinimumMigration)); }
        }
        
        private DbMigrator _migrator;
        private ISet<string> _appliedVersions;
        private ISet<string> _pendingVersions;
        private ISet<string> _availableVersions;

        public ISet<string> AppliedVersions
        {
            get { 
                EnsureMigrationData();
                return _appliedVersions;
            }
        }

        public ISet<string> PendingVersions
        {
            get
            {
                EnsureMigrationData();
                return _pendingVersions;
            }
        }

        public ISet<string> AvailableVersions
        {
            get
            {
                EnsureMigrationData();
                return _availableVersions;
            }
        }

        public DatabaseVersion GetVersion(string id)
        {
            var match = MigrationIdRegex.Match(id);
            Debug.Assert(match.Success);
            if (!match.Success)
            {
                throw new ArgumentException(String.Format(Strings.InvalidEntityFrameworkMigrationId, id), "id");
            }
            DateTime createdUtc = DateTime.ParseExact(match.Groups["date"].Value, "yyyyMMddHHmmssf", CultureInfo.InvariantCulture);
            string name = match.Groups["name"].Value;
            string description = String.Empty;
            
            // Try to get the type with that name
            var migrationType = typeof(Initial).Assembly.GetType(typeof(Initial).Namespace + "." + name);
            if (migrationType != null)
            {
                var attr = migrationType.GetCustomAttribute<DescriptionAttribute>();
                if (attr != null)
                {
                    description = attr.Description;
                }
            }

            return new DatabaseVersion(id, createdUtc, name, description);
        }

        public void UpdateToLatest()
        {
            EnsureMigrationData();
            _migrator.Update();

            // Reload the migrations
            LoadMigrationData();
        }

        public void UpdateToMinimum()
        {
            EnsureMigrationData();
            if (!_appliedVersions.Contains(MinimumMigrationId))
            {
                _migrator.Update(MinimumMigrationId);
            }

            // Reload the migrations
            LoadMigrationData();
        }

        private void EnsureMigrationData()
        {
            if (_migrator == null || _appliedVersions == null || _pendingVersions == null || _availableVersions == null)
            {
                LoadMigrationData();
            }
        }

        private void LoadMigrationData()
        {
            _migrator = new DbMigrator(new MigrationsConfiguration());
            _appliedVersions = new HashSet<string>(_migrator.GetDatabaseMigrations(), StringComparer.OrdinalIgnoreCase);
            _pendingVersions = new HashSet<string>(_migrator.GetPendingMigrations(), StringComparer.OrdinalIgnoreCase);
            _availableVersions = new HashSet<string>(_migrator.GetLocalMigrations(), StringComparer.OrdinalIgnoreCase);
        }
    }
}