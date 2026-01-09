// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public static class UploadHelper
    {
        private static readonly object UniqueLock = new object();

        /// <summary>
        /// Helper class for defining the properties of a test package to be uploaded.
        /// </summary>
        public class PackageToUpload
        {
            /// <summary>
            /// The ID of the package to upload.
            /// </summary>
            public string Id { get; }

            /// <summary>
            /// The version of the package to upload.
            /// </summary>
            public string Version { get; }
            
            /// <summary>
            /// The username of the user that will be specified as the owner of the package in the verification form.
            /// </summary>
            public string Owner { get; }

            public PackageToUpload(string id, string version = null, string owner = null)
            {
                Id = id;
                Version = version ?? GetUniquePackageVersion();
                Owner = owner ?? GalleryConfiguration.Instance.Account.Name;
            }

            protected PackageToUpload(PackageToUpload package)
            {
                Id = package.Id;
                Version = package.Version;
                Owner = package.Owner;
            }
        }

        /// <summary>
        /// Gets a unique ID for a package to upload.
        /// </summary>
        public static string GetUniquePackageId()
        {
            lock (UniqueLock)
            {
                return $"NuGetFunctionalTest_{Guid.NewGuid():N}";
            }
        }

        /// <summary>
        /// Gets a unique version for a package to upload.
        /// </summary>
        public static string GetUniquePackageVersion()
        {
            lock (UniqueLock)
            {
                var ticks = DateTimeOffset.UtcNow.Ticks;
                return $"1.0.0-v{ticks}";
            }
        }
    }
}
