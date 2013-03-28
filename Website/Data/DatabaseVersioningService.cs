using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NuGetGallery.Data.Migrations;

namespace NuGetGallery.Data
{
    public class DatabaseVersioningService : IDatabaseVersioningService
    {
        private static string _minimumMigrationId;
        private static readonly Type MinimumMigration = typeof(AddMinRequiredVersionColumn); // This is the last migration before we switched to manually running them
        private static readonly Regex MigrationIdRegex = new Regex(@"^(?<date>\d{15})_(?<name>.*)$");

        public static string MinimumMigrationId
        {
            get { return _minimumMigrationId ?? (_minimumMigrationId = MigrationUtils.GetMigrationId(MinimumMigration)); }
        }
        
        private ISet<string> _appliedVersions;
        private ISet<string> _pendingVersions;
        private ISet<string> _availableVersions;

        public IDbMigrator Migrator { get; protected set; }

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

        protected DatabaseVersioningService()
        {
        }

        public DatabaseVersioningService(IDbMigrator migrator)
        {
            Migrator = migrator;
        }

        public DatabaseVersion GetVersion(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException(String.Format(Strings.ArgumentCannotBeNullOrEmpty, "id"), "id");
            }

            var match = MigrationIdRegex.Match(id);
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
            Migrator.Update();

            // Reload the migrations
            LoadMigrationData();
        }

        public void UpdateToMinimum()
        {
            EnsureMigrationData();
            if (!_appliedVersions.Contains(MinimumMigrationId))
            {
                Migrator.Update(MinimumMigrationId);

                // Reload the migrations
                LoadMigrationData();
            }
        }

        private void EnsureMigrationData()
        {
            if (_appliedVersions == null || _pendingVersions == null || _availableVersions == null)
            {
                LoadMigrationData();
            }
        }

        private void LoadMigrationData()
        {
            _appliedVersions = new HashSet<string>(Migrator.GetDatabaseMigrations(), StringComparer.OrdinalIgnoreCase);
            _pendingVersions = new HashSet<string>(Migrator.GetPendingMigrations(), StringComparer.OrdinalIgnoreCase);
            _availableVersions = new HashSet<string>(Migrator.GetLocalMigrations(), StringComparer.OrdinalIgnoreCase);
        }
    }
}