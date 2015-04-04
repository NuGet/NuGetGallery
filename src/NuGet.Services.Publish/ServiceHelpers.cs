using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web;

namespace NuGet.Services.Publish
{
    public static class ServiceHelpers
    {
        private static readonly string _graphResourceId;
        private static readonly string _clientId;
        private static readonly string _aadInstance;
        private static readonly string _appKey;
        private static readonly ConfigurationService _configurationService;

        static ServiceHelpers()
        {
            _configurationService = new ConfigurationService();
            _graphResourceId = _configurationService.Get("ida.GraphResourceId");
            _aadInstance = _configurationService.Get("ida.AADInstance");
            _clientId = _configurationService.Get("ida.ClientId");
            _appKey = _configurationService.Get("ida.AppKey");
        }

        public static JObject LoadContext(string name)
        {
            string assName = Assembly.GetExecutingAssembly().GetName().Name;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assName + "." + name))
            {
                string json = new StreamReader(stream).ReadToEnd();
                return JObject.Parse(json);
            }
        }

        public static async Task Test(IOwinContext context)
        {
            //
            // The Scope claim tells you what permissions the client application has in the service.
            // In this case we look for a scope value of user_impersonation, or full access to the service as the user.
            //
            Claim scopeClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope");

            if (scopeClaim != null && scopeClaim.Value == "user_impersonation")
            {
                Claim claim = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier);
                string msg = string.Format("OK claim.Subject.Name = {0} Value = {1}", claim.Subject.Name, claim.Value);
                await context.Response.WriteAsync(msg);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                await context.Response.WriteAsync("The Scope claim does not contain 'user_impersonation' or scope claim not found");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
        }

        public static async Task WriteErrorResponse(IOwinContext context, string error, HttpStatusCode statusCode)
        {
            JToken content = new JObject 
            {
                { "type", "SimpleError" },
                { "error", error }
            };

            await WriteResponse(context, content, statusCode);
        }

        public static async Task WriteErrorResponse(IOwinContext context, IEnumerable<string> errors, HttpStatusCode statusCode)
        {
            JArray array = new JArray();
            foreach (string error in errors)
            {
                array.Add(error);
            }

            JToken content = new JObject
            { 
                { "type", "ValidationError" },
                { "errors", array }
            };

            await WriteResponse(context, content, statusCode);
        }

        public static async Task WriteResponse(IOwinContext context, JToken content, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;

            if (content != null)
            {
                string callback = context.Request.Query["callback"];

                string contentType;
                string responseString;
                if (string.IsNullOrEmpty(callback))
                {
                    responseString = content.ToString();
                    contentType = "application/json";
                }
                else
                {
                    responseString = string.Format("{0}({1})", callback, content);
                    contentType = "application/javascript";
                }

                context.Response.Headers.Add("Pragma", new string[] { "no-cache" });
                context.Response.Headers.Add("Cache-Control", new string[] { "no-cache" });
                context.Response.Headers.Add("Expires", new string[] { "0" });
                context.Response.ContentType = contentType;

                await context.Response.WriteAsync(responseString);
            }
        }

        static X509Certificate2 LoadCertificate()
        {
            string thumbprint = _configurationService.Get("nuget.Thumbprint");

            if (string.IsNullOrWhiteSpace(thumbprint))
            {
                return null;
            }

            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                certStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (certCollection.Count > 0)
                {
                    return certCollection[0];
                }
                return null;
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }

        public static string GetTenantId()
        {
            Claim tenantClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid");
            string tenantId = (tenantClaim != null) ? tenantClaim.Value : string.Empty;
            return tenantId;
        }

        public static string GetUserId()
        {
            Claim userClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            string userId = (userClaim != null) ? userClaim.Value : string.Empty;
            return userId;
        }

        public static async Task<ActiveDirectoryClient> GetActiveDirectoryClient()
        {
            // BUG BUG BUG this code does not work properly on AAD because it pesumes the objectidentifier claim which is not correct

            string tenantId = GetTenantId();

            string authority = string.Format(_aadInstance, tenantId);

            AuthenticationContext authContext = new AuthenticationContext(authority);

            AuthenticationResult result;

            if (string.IsNullOrEmpty(_appKey))
            {
                //string assertion = Startup.SecurityToken.ToString();

                X509Certificate2 cert = LoadCertificate();

                string authHeader = HttpContext.Current.Request.Headers["Authorization"];
                string userAccessToken = authHeader.Substring(authHeader.LastIndexOf(' ')).Trim();

                var bootstrapContext = ClaimsPrincipal.Current.Identities.First().BootstrapContext as System.IdentityModel.Tokens.BootstrapContext;
                userAccessToken = bootstrapContext.Token;

                UserAssertion userAssertion = new UserAssertion(userAccessToken);

                ClientAssertionCertificate clientAssertionCertificate = new ClientAssertionCertificate(_clientId, cert);
                result = await authContext.AcquireTokenAsync(_graphResourceId, clientAssertionCertificate, userAssertion);
            }
            else
            {
                ClientCredential clientCredential = new ClientCredential(_clientId, _appKey);
                result = await authContext.AcquireTokenAsync(_graphResourceId, clientCredential);
            }

            string accessToken = result.AccessToken;

            Uri serviceRoot = new Uri(new Uri(_graphResourceId), tenantId);

            ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(serviceRoot, () => { return Task.FromResult(accessToken); });

            string userId = GetUserId();

            var tenantDetails = await activeDirectoryClient.TenantDetails.ExecuteAsync();

            return activeDirectoryClient;
        }

        static string DumpClaims()
        {
            JArray claimsArray = new JArray();
            foreach (Claim claim in ClaimsPrincipal.Current.Claims)
            {
                JObject obj = new JObject();
                obj.Add("type", claim.Type);
                obj.Add("value", claim.Value);
                claimsArray.Add(obj);
            }

            string allTheClaims = claimsArray.ToString();

            return allTheClaims;
        }

        public static async Task<JObject> ReadJObject(Stream stream)
        {
            try
            {
                using (TextReader reader = new StreamReader(stream))
                {
                    string json = await reader.ReadToEndAsync();
                    return JObject.Parse(json);
                }
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}