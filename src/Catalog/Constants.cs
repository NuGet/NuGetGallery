// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Constants
    {
        public static readonly DateTime DateTimeMinValueUtc = DateTimeOffset.MinValue.UtcDateTime;
        public const int MaxPageSize = 550;
        public const string Sha512 = "SHA512";
        public static readonly DateTime UnpublishedDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public const string NoStoreCacheControl = "no-store";
    }
}