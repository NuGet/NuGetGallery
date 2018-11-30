// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    public class VersionListChange
    {
        private VersionListChange(bool isDelete, NuGetVersion parsedVersion, string fullVersion, VersionPropertiesData data)
        {
            IsDelete = isDelete;
            ParsedVersion = parsedVersion ?? throw new ArgumentNullException(nameof(parsedVersion));
            FullVersion = fullVersion;
            Data = data;
        }

        public bool IsDelete { get; }
        public NuGetVersion ParsedVersion { get; }

        /// <summary>
        /// When <see cref="IsDelete"/> is true, this value is null.
        /// </summary>
        public string FullVersion { get; }

        /// <summary>
        /// When <see cref="IsDelete"/> is true, this value is null.
        /// </summary>
        public VersionPropertiesData Data { get; }

        /// <summary>
        /// Initialize a version list change representing a delete of the provided version.
        /// </summary>
        /// <param name="parsedVersion">The version. This can be parsed from any form of the version.</param>
        /// <returns>The version list change.</returns>
        public static VersionListChange Delete(NuGetVersion parsedVersion)
        {
            if (parsedVersion == null)
            {
                throw new ArgumentNullException(nameof(parsedVersion));
            }

            return new VersionListChange(
                isDelete: true,
                parsedVersion: parsedVersion,
                fullVersion: null,
                data: null);
        }

        /// <summary>
        /// Initialize a version list change representing an upsert of the provided version.
        /// </summary>
        /// <param name="fullOrOriginalVersion">The full version string or the original version string.</param>
        /// <param name="data">The properties relevent to the version list resource.</param>
        /// <returns>The version list change.</returns>
        public static VersionListChange Upsert(string fullOrOriginalVersion, VersionPropertiesData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (fullOrOriginalVersion == null)
            {
                throw new ArgumentNullException(nameof(fullOrOriginalVersion));
            }

            var parsedVersion = NuGetVersion.Parse(fullOrOriginalVersion);
            var fullVersion = parsedVersion.ToFullString();
            return new VersionListChange(
                isDelete: false,
                parsedVersion: parsedVersion,
                fullVersion: fullVersion,
                data: data);
        }
    }
}
