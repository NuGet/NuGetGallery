// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Validation.Symbols
{
    public interface ISymbolsFileService
    {
        /// <summary>
        /// Downloads the nupkg file async.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The snupkg stream.</returns>
        Task<Stream> DownloadNupkgFileAsync(string packageId, string packageNormalizedVersion, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads the snupkg file.
        /// </summary>
        /// <param name="snupkgUri">The uri of the snupkg.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The snupkg stream.</returns>
        Task<Stream> DownloadSnupkgFileAsync(string snupkgUri, CancellationToken cancellationToken);
    }
}
