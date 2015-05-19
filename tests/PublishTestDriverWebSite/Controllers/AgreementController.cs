// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security.OpenIdConnect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;

namespace PublishTestDriverWebSite.Controllers
{
    [Authorize]
    public class AgreementController : Controller
    {
        private string nugetServiceResourceId = ConfigurationManager.AppSettings["nuget:ServiceResourceId"];
        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];

        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        public ActionResult Index()
        {
            return View(new AgreementModel());
        }

        [HttpPost]
        public async Task<ActionResult> Acceptance(string agreement, string agreementVersion, string email, bool accept)
        {
            try
            {
                var signedInUserId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;

                var userObjectIdClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
                var userObjectId = userObjectIdClaim != null ? userObjectIdClaim.Value : signedInUserId;

                string tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

                string authority = string.Format(Startup.AuthorityFormat, tenantId);

                AuthenticationContext authContext = new AuthenticationContext(authority, new NaiveSessionCache(signedInUserId));

                AuthenticationResult authenticationResult;

                if (Startup.Certificate == null)
                {
                    ClientCredential credential = new ClientCredential(clientId, appKey);
                    authenticationResult = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, credential, new UserIdentifier(userObjectId, UserIdentifierType.UniqueId));
                }
                else
                {
                    ClientAssertionCertificate clientAssertionCertificate = new ClientAssertionCertificate(clientId, Startup.Certificate);
                    authenticationResult = await authContext.AcquireTokenSilentAsync(nugetServiceResourceId, clientAssertionCertificate, new UserIdentifier(userObjectId, UserIdentifierType.UniqueId));
                }

                string message = null;
                HttpStatusCode statusCode = HttpStatusCode.Gone;
                if (!accept)
                {
                    // Check status
                    HttpClient client = new HttpClient();
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetPublishServiceBaseAddress + "/agreements?agreement=" + agreement + "&agreementVersion=" + agreementVersion);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

                    HttpResponseMessage response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var model = JsonConvert.DeserializeObject<AgreementModel>(await response.Content.ReadAsStringAsync());

                        return View(model);
                    }
                    else
                    {
                        try
                        {
                            JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                            string error = publishServiceResponse["error"].ToString();
                            message = string.Format("Acceptance error \"{0}\"", error);
                            statusCode = response.StatusCode;
                        }
                        catch { }
                    }
                }
                else
                {
                    // Accept, then check
                    HttpClient client = new HttpClient();
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, nugetPublishServiceBaseAddress + "/agreements/accept");
                    request.Content = new StringContent(new JObject
                    {
                        { "agreement", agreement },
                        { "agreementVersion", agreementVersion },
                        { "email", email }
                    }.ToString());
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

                    HttpResponseMessage response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        return await Acceptance(agreement, agreementVersion, null, false);
                    }
                    else
                    {
                        try
                        {
                            JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                            string error = publishServiceResponse["error"].ToString();
                            message = string.Format("Acceptance error \"{0}\"", error);
                            statusCode = response.StatusCode;
                        }
                        catch { }
                    }
                }

                return View(new AgreementModel { Message = message ?? string.Format("{0} with no further details available", statusCode) });
            }
            catch (AdalSilentTokenAcquisitionException)
            {
                //TODO: this isn't quite right
                HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                return View(new AgreementModel { Message = "AuthorizationRequired" });
            }
        }
    }
}