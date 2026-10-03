// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    /// <summary>
    /// Checks owner-wide staging capacity without reserving slots or serializing concurrent uploads.
    /// </summary>
    public class StagingQuotaService : IStagingQuotaService
    {
        private readonly IEntitiesContext _entities;
        private readonly int _defaultLimit;
        private readonly Dictionary<string, int> _ownerLimits;

        public StagingQuotaService(IEntitiesContext entities, IAppConfiguration configuration)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.StagingQuotaLimit < 1)
            {
                throw new InvalidOperationException("StagingQuotaLimit must be positive.");
            }

            _defaultLimit = configuration.StagingQuotaLimit;
            _ownerLimits = ReadOwnerLimits(configuration.StagingQuotaOwnerOverrides);
        }

        public StagingQuotaUsage GetUsage(User owner)
        {
            ValidateOwner(owner);
            return CreateUsage(owner, GetPackages(owner.Key).Count(), GetSymbols(owner.Key).Count());
        }

        public async Task EnsureCapacityAsync(User owner)
        {
            ValidateOwner(owner);
            var packages = await GetPackages(owner.Key).CountAsync();
            var symbols = await GetSymbols(owner.Key).CountAsync();
            var usage = CreateUsage(owner, packages, symbols);
            if (usage.UsedArtifacts >= usage.Limit)
            {
                throw new StagingQuotaExceededException();
            }
        }

        private IQueryable<StagedPackage> GetPackages(int ownerKey)
        {
            return _entities.StagedPackages.AsNoTracking()
                .Where(attempt => attempt.StagedPackageIdentity.OwnerKey == ownerKey)
                .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                .Where(attempt => attempt.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded);
        }

        private IQueryable<StagedSymbolPackage> GetSymbols(int ownerKey)
        {
            return _entities.StagedSymbolPackages.AsNoTracking()
                .Where(attempt => attempt.StagedPackageIdentity.OwnerKey == ownerKey)
                .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                .Where(attempt => attempt.SymbolPackage.StatusKey == PackageStatus.Staged)
                .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded);
        }

        private StagingQuotaUsage CreateUsage(User owner, int packages, int symbols)
        {
            if (!_ownerLimits.TryGetValue(owner.Username, out var limit))
            {
                limit = _defaultLimit;
            }

            return new StagingQuotaUsage { Owner = owner.Username, UsedPackages = packages, UsedSymbols = symbols, Limit = limit };
        }

        private static Dictionary<string, int> ReadOwnerLimits(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("StagingQuotaOwnerOverrides must be a JSON object.");
            }

            JObject overrides;
            try
            {
                overrides = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonReaderException exception)
            {
                throw new InvalidOperationException("StagingQuotaOwnerOverrides must be a JSON object mapping owner names to positive artifact limits.", exception);
            }

            var limits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in overrides.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name) || property.Name != property.Name.Trim() || limits.ContainsKey(property.Name))
                {
                    throw new InvalidOperationException("StagingQuotaOwnerOverrides contains an invalid or duplicate owner name.");
                }

                if (property.Value.Type != JTokenType.Integer || !int.TryParse(property.Value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 1)
                {
                    throw new InvalidOperationException($"The staging quota override for '{property.Name}' must be a positive integer no greater than {int.MaxValue}.");
                }

                limits.Add(property.Name, limit);
            }
            return limits;
        }

        private static void ValidateOwner(User owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (owner.Key <= 0 || string.IsNullOrWhiteSpace(owner.Username))
            {
                throw new ArgumentException("The staging owner must be persisted and have a username.", nameof(owner));
            }
        }
    }
}
