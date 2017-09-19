// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Threading.Tasks;

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
        Task<string> GetReadMeHtmlAsync(ReadMeRequest readMeRequest, Encoding encoding);

        /// <summary>
        /// Get the converted HTML from the stored ReadMe markdown.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="model">Display package view model to populate.</param>
        /// <param name="isPending">Whether to retrieve the pending ReadMe.</param>
        /// <returns>Pending or active ReadMe converted to HTML.</returns>
        Task GetReadMeHtmlAsync(Package package, DisplayPackageViewModel model, bool isPending = false);

        /// <summary>
        /// Get package ReadMe markdown from storage.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="isPending">Whether to retrieve the pending ReadMe.</param>
        /// <returns>Pending or active ReadMe markdown from storage.</returns>
        Task<string> GetReadMeMdAsync(Package package, bool isPending = false);

        /// <summary>
        /// Save a pending ReadMe if changes are detected.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="edit">Package edit entity.</param>
        /// <returns>True if a ReadMe is pending, false otherwise.</returns>
        Task<bool> SavePendingReadMeMdIfChanged(Package package, EditPackageVersionRequest edit, Encoding encoding);
    }
}
