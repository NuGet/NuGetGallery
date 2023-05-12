// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery.Shared;

namespace NuGetGallery.Login
{
    public interface IEditableLoginConfigurationFileStorageService: ILoginDiscontinuationFileStorageService
    {

        /// <summary>
        /// Get a reference to the raw content of <see cref="LoginDiscontinuation"/>.
        /// </summary>
        /// <returns>A snapshot of the  loginDiscontinuation's content and ETag.</returns>
        Task<LoginDiscontinuationReference> GetReferenceAsync();

        /// <summary>
        /// Add or Remove an user email address to the exception email list on <see cref="LoginDiscontinuation"/>.
        /// </summary>
        /// <param name="emailAddress">The user email address.</param>
        /// <param name="operation"> <see cref="ContentOperations">.</param>
        Task AddUserEmailAddressforPasswordAuthenticationAsync(string emailAddress, ContentOperations operation);

        /// <summary>
        /// Get an exception email list on <see cref="LoginDiscontinuation"/>.
        /// </summary>
        /// <returns>the exception email list on loginDiscontinuation.</returns>
        Task<IReadOnlyList<string>> GetListOfExceptionEmailList();

        /// <summary>
        /// Try to update the <see cref="LoginDiscontinuation"/>.
        /// </summary>
        /// <param name="loginDiscontinuation">The log in discontinuation configuration.</param>
        /// <param name="contentId">The loginDiscontinuation's ETag.</param>
        /// <returns>The result of the save operation.</returns>
        Task<ContentSaveResult> TrySaveAsync(LoginDiscontinuation loginDiscontinuation, string contentId);

    }
}
