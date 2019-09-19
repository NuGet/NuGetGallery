// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IReadMeService
    {
        /// <summary>
        /// Determine if a <see cref="ReadMeRequest"/> has readMe markdown data.
        /// </summary>
        /// <param name="readMeRequest">Request object.</param>
        /// <returns>True if input provided, false otherwise.</returns>
        bool HasReadMeSource(ReadMeRequest readMeRequest);

        /// <summary>
        /// Get the converted HTML from a <see cref="ReadMeRequest"/> request with markdown data.
        /// </summary>
        /// <param name="readMeRequest">Request object.</param>
        /// <returns>HTML from markdown conversion.</returns>
        Task<RenderedReadMeResult> GetReadMeHtmlAsync(ReadMeRequest readMeRequest, Encoding encoding);

        /// <summary>
        /// Get the converted HTML from the stored ReadMe markdown.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <returns>ReadMe converted to HTML.</returns>
        Task<RenderedReadMeResult> GetReadMeHtmlAsync(Package package);

        /// <summary>
        /// Get package ReadMe markdown from storage.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <returns>ReadMe markdown from storage.</returns>
        Task<string> GetReadMeMdAsync(Package package);

        /// <summary>
        /// Save a ReadMe if changes are detected.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="edit">Package version edit readme request.</param>
        /// <param name="encoding">The encoding used when reading the existing readme.</param>
        /// <param name="commitChanges">Whether or not to commit the pending changes to the database.</param>
        /// <returns>True if the package readme changed, otherwise false.</returns>
        Task<bool> SaveReadMeMdIfChanged(
            Package package,
            EditPackageVersionReadMeRequest edit,
            Encoding encoding,
            bool commitChanges);
    }
}
