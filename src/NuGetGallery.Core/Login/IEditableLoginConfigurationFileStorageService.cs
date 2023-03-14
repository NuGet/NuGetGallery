using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery.Shared;

namespace NuGetGallery.Login
{
    public interface IEditableLoginConfigurationFileStorageService: ILoginDiscontinuationFileStorageService
    {

        /// <summary>
        /// Get a reference to the loginDiscontinuation's raw content.
        /// </summary>
        /// <returns>A snapshot of the  loginDiscontinuation's content and ETag.</returns>
        Task<LoginDiscontinuationReference> GetReferenceAsync();

        /// <summary>
        /// Add or Remove an user email address to the excpetion email list on loginDiscontinuation.
        /// </summary>
        /// <param name="emailAddress">The user email address.</param>
        /// <param name="add">Indicate remove or add email address.</param>
        Task AddUserEmailAddressforPasswordAuthenticationAsync(string emailAddress, bool add);

        /// <summary>
        /// Get an excpetion email list on loginDiscontinuation.
        /// </summary>
        Task<IReadOnlyList<string>> GetListOfExceptionEmailList();

        /// <summary>
        /// Try to update the LoginDiscontinuation.
        /// </summary>
        /// <param name="loginDiscontinuation">The log in discontinuation configuration.</param>
        /// <param name="contentId">The loginDiscontinuation's ETag.</param>
        /// <returns>The result of the save operation.</returns>
        Task<ContentSaveResult> TrySaveAsync(LoginDiscontinuation loginDiscontinuation, string contentId);

    }
}
