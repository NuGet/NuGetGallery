// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs
{
    /// <summary>
    /// Keys to environment variables common across all jobs
    /// </summary>
    public static class EnvironmentVariableKeys
    {
        public const string SqlGallery = "NUGETJOBS_SQL_GALLERY";
        public const string StorageGallery = "NUGETJOBS_STORAGE_GALLERY";
        public const string StoragePrimary = "NUGETJOBS_STORAGE_PRIMARY";
        public const string StorageBackup = "NUGETJOBS_STORAGE_BACKUP";
    }
}