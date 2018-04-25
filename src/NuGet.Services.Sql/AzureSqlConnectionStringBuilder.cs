// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Common;
using System.Globalization;

namespace NuGet.Services.Sql
{
    /// <summary>
    /// Custom connection string 
    /// </summary>
    public class AzureSqlConnectionStringBuilder : DbConnectionStringBuilder
    {
        private const string AadAuthorityTemplate = "https://login.microsoftonline.com/{0}/v2.0";

        public string AadTenant { get; }

        public string AadClientId { get; }

        public string AadCertificate { get; }

        public string AadCertificatePassword { get; }

        public string AadAuthority { get; }

        public AzureSqlConnectionStringBuilder(string connectionString)
        {
            ConnectionString = connectionString;

            AadTenant = Ingest("AadTenant");
            AadClientId = Ingest("AadClientId");
            AadCertificate = Ingest("AadCertificate");
            AadCertificatePassword = Ingest("AadCertificatePassword");

            if (!string.IsNullOrEmpty(AadTenant))
            {
                AadAuthority = string.Format(CultureInfo.InvariantCulture, AadAuthorityTemplate, AadTenant);
            }
        }

        private string Ingest(string propertyName)
        {
            string result = string.Empty;
            if (ContainsKey(propertyName))
            {
                result = this[propertyName] as string;
                Remove(propertyName);
            }
            return result;
        }
    }
}
