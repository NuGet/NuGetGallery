using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            return new ContentResult()
            {
                Content = model.AuthenticatedClient.ProviderName + ":" + model.AuthenticatedClient.UserInformation.Id + "(" + model.AuthenticatedClient.UserInformation.Name + "," + model.AuthenticatedClient.UserInformation.Email + ")"
            };
        }
    }
}
