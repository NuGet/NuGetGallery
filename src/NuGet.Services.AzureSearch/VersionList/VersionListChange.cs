// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    public class VersionListChange
    {
        private VersionListChange(string fullOrOriginalVersion, bool isDelete, VersionPropertiesData data)
        {
            if (fullOrOriginalVersion == null)
            {
                throw new ArgumentNullException(nameof(fullOrOriginalVersion));
            }

            IsDelete = isDelete;
            ParsedVersion = NuGetVersion.Parse(fullOrOriginalVersion);
            FullVersion = ParsedVersion.ToFullString();
            Data = data;
        }

        public bool IsDelete { get; }
        public string FullVersion { get; }
        public NuGetVersion ParsedVersion { get; }
        public VersionPropertiesData Data { get; }

        public static VersionListChange Delete(string fullOrOriginalVersion)
        {
            return new VersionListChange(fullOrOriginalVersion, isDelete: true, data: null);
        }

        public static VersionListChange Upsert(string fullOrOriginalVersion, VersionPropertiesData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return new VersionListChange(fullOrOriginalVersion, isDelete: false, data: data);
        }
    }
}
