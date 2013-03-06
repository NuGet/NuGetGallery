using System.Web;
using System.Web.Mvc;
using WorldDomination.Web.Authentication.Mvc;

namespace NuGetGallery.Infrastructure
{
    public class AuthenticationCallback : IAuthenticationCallbackProvider
    {
        public ActionResult Process(HttpContextBase context, AuthenticateCallbackData model)
        {
            if (model.Exception != null)
            {
                throw model.Exception;
            }

            // TODO: Save/Update to Db.
            // eg. 
            // var user = _userService.FindByEmailAddress(model.AuthenticatedClient.UserInformation.Email);

            // TODO: Store data in Principal (eg. Claims).

            return new ContentResult
                       {
                           Content =
                               model.AuthenticatedClient.ProviderName + ":" +
                               model.AuthenticatedClient.UserInformation.Id + "(" +
                               model.AuthenticatedClient.UserInformation.Name + "," +
                               model.AuthenticatedClient.UserInformation.Email + ")"
                       };
        }
    }
}