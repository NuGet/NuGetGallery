// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Search.Client;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public interface IPackageVersionsQuery
    {
        Task<IEnumerable<string>> Execute(
            string id,
            bool? includePrerelease = false);
    }

    public class AutocompleteServicePackageVersionsQuery : IPackageVersionsQuery
    {
        private readonly ServiceDiscoveryClient _serviceDiscoveryClient;
        private readonly string _autocompleteServiceResourceType;
        private readonly RetryingHttpClientWrapper _httpClient;

        public AutocompleteServicePackageVersionsQuery(IGalleryConfigurationService configService)
        {
            _serviceDiscoveryClient = new ServiceDiscoveryClient(configService.Current.ServiceDiscoveryUri);
            _autocompleteServiceResourceType = configService.Current.AutocompleteServiceResourceType;
            _httpClient = new RetryingHttpClientWrapper(new HttpClient());
        }

        public async Task<IEnumerable<string>> Execute(string id, bool? includePrerelease)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var queryString = "id=" + Uri.EscapeUriString(id);
            if (!includePrerelease.HasValue)
            {
                queryString += "&prerelease=false";
            }
            else
            {
                queryString += "&prerelease=" + includePrerelease.Value;
            }

            var endpoints = await _serviceDiscoveryClient.GetEndpointsForResourceType(_autocompleteServiceResourceType);
            endpoints = endpoints.Select(e => new Uri(e + "?" + queryString)).AsEnumerable();

            var result = await _httpClient.GetStringAsync(endpoints);
            var resultObject = JObject.Parse(result);

            return resultObject["data"].Select(entry => entry.ToString());
        }
    }

    public class PackageVersionsQuery : IPackageVersionsQuery
    {
        private const string SqlFormat = @"SELECT p.[Version]
FROM Packages p
	JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE pr.ID = {{0}}
	{0}";

        private readonly IEntitiesContext _entities;

        public PackageVersionsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public Task<IEnumerable<string>> Execute(
            string id,
            bool? includePrerelease = false)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var dbContext = (DbContext)_entities;

            var prereleaseFilter = String.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = 0";
            }
            return Task.FromResult(dbContext.Database.SqlQuery<string>(
                String.Format(CultureInfo.InvariantCulture, SqlFormat, prereleaseFilter), id).AsEnumerable());
        }
    }
}