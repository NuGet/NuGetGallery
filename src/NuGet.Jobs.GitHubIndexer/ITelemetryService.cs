// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface ITelemetryService
    {
        /// <summary>
        /// Track the duration to find GitHub repositories to index.
        /// </summary>
        IDisposable TrackDiscoverRepositoriesDuration();

        /// <summary>
        /// Track the duration to index a GitHub repository. This includes checking out files
        /// from the repository and parsing NuGet dependencies.
        /// </summary>
        /// <param name="repositoryName">The name of the repository to index</param>
        IDisposable TrackIndexRepositoryDuration(string repositoryName);

        /// <summary>
        /// Track the duration to list files from a GitHub repository.
        /// </summary>
        /// <param name="repositoryName">The name of the repository that will be listed</param>
        IDisposable TrackListFilesDuration(string repositoryName);

        /// <summary>
        /// Track the total duration to check out files from a GitHub repository.
        /// </summary>
        /// <param name="repositoryName">The name of the repository whose files will be checked out.</param>
        IDisposable TrackCheckOutFilesDuration(string repositoryName);

        /// <summary>
        /// Track the total duration to upload the GitHub usage blob.
        /// </summary>
        IDisposable TrackUploadGitHubUsageBlobDuration();

        /// <summary>
        /// Track when the generated GitHub Usage blob is empty. This indicates an integration issue.
        /// </summary>
        void TrackEmptyGitHubUsageBlob();
    }
}
