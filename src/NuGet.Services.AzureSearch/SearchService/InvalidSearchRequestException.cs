using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class InvalidSearchRequestException : Exception
    {
        /// <summary>
        /// Create a new invalid search request exception.
        /// </summary>
        /// <param name="message">The message to display to the user. Must not contain sensitive information.</param>
        public InvalidSearchRequestException(string message)
            : base(message)
        {
        }
    }
}
