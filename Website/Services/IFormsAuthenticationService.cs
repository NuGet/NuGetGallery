using System;
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery
{
    public interface IFormsAuthenticationService
    {
        string GetAuthTicket(
            string userName, 
            bool createPersistentCookie, 
            IEnumerable<string> roles);

        string GetAuthTicket(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles,
            TimeSpan validFor);

        void SetAuthCookie(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles);

        void SignOut();

        bool ShouldForceSSL(HttpContextBase context);

        /// <summary>
        /// Returns the user name from the specified ticket
        /// </summary>
        /// <param name="ticket">The encrypted ticket</param>
        /// <returns>The user name in the ticket. Null if the ticket is invalid or expired</returns>
        string GetUserNameFromTicket(string ticket);
    }
}