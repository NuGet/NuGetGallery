// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Sql.Tests
{
    public static class MockConnectionStrings
    {
        public const string AadTenant = "aadTenant";
        public const string AadClientId = "aadClientId";

        public const string BaseConnectionString = "Data Source=tcp:DB.database.windows.net;Initial Catalog=DB";

        public static readonly string SqlConnectionString = $"{BaseConnectionString};User ID=$$user$$;Password=$$pass$$";
        public static readonly string AadSqlConnectionString = $"{BaseConnectionString};AadTenant={AadTenant};AadClientId={AadClientId};AadCertificate=$$cert$$";
    }
}
