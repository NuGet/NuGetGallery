// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.Incidents
{
    public class IncidentApiClient : IIncidentApiClient
    {
        private const string IncidentApiIncidentsEndpoint = "incidents";
        private static readonly string IncidentApiIndividualIncidentQueryFormatString = $"{IncidentApiIncidentsEndpoint}({{0}})";
        private static readonly string IncidentApiIncidentListQueryFormatString = $"{IncidentApiIncidentsEndpoint}?{{0}}";

        private static readonly JsonSerializerSettings _incidentApiJsonSerializerSettings =
            new JsonSerializerSettings() { DateTimeZoneHandling = DateTimeZoneHandling.Utc };

        private readonly IncidentApiConfiguration _configuration;

        public IncidentApiClient(IncidentApiConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<Incident> GetIncident(string id)
        {
            return GetIncidentApiResponse<Incident>(GetIncidentApiGetIncidentQuery(id));
        }

        public async Task<IEnumerable<Incident>> GetIncidents(string query)
        {
            var incidents = new List<Incident>();
            var nextLink = GetIncidentApiUri(GetIncidentApiIncidentList(query));
            do
            {
                var incidentList = await GetIncidentApiResponse<IncidentList>(nextLink);
                incidents.AddRange(incidentList.Incidents);
                nextLink = incidentList.NextLink;
            } while (nextLink != null);

            return incidents;
        }

        private string GetIncidentApiIncidentList(string oDataQueryParameters)
        {
            return string.Format(IncidentApiIncidentListQueryFormatString, oDataQueryParameters);
        }

        private string GetIncidentApiGetIncidentQuery(string id)
        {
            return string.Format(IncidentApiIndividualIncidentQueryFormatString, id);
        }

        private Uri GetIncidentApiUri(string query)
        {
            return new Uri(_configuration.BaseUri, query);
        }

        private Task<T> GetIncidentApiResponse<T>(string query)
        {
            return GetIncidentApiResponse<T>(GetIncidentApiUri(query));
        }

        private async Task<T> GetIncidentApiResponse<T>(Uri uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.ClientCertificates.Add(_configuration.Certificate);
            var response = await request.GetResponseAsync();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var content = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<T>(content, _incidentApiJsonSerializerSettings);
            }
        }
    }
}
