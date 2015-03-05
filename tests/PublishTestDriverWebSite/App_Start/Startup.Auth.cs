using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using PublishTestDriverWebSite.Utils;
using System;
using System.Configuration;
using System.Globalization;
using System.IdentityModel.Claims;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PublishTestDriverWebSite
{
    public class InterceptWebRequestHandler : WebRequestHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken);
        }
    }

    public partial class Startup
    {
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The App Key is a credential used to authenticate the application to Azure AD.  Azure AD supports password and certificate credentials.
        // The Metadata Address is used by the application to retrieve the signing keys used by Azure AD.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        // The Post Logout Redirect Uri is the URL where the user will be redirected after they sign out.
        //
        static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];
        static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        static string postLogoutRedirectUri = ConfigurationManager.AppSettings["ida:PostLogoutRedirectUri"];

        public static readonly string Authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        // This is the resource ID of the AAD Graph API.  We'll need this to request a token to call the Graph API.
        static string graphResourceId = ConfigurationManager.AppSettings["ida:GraphResourceId"];

        static string serviceResourceId = ConfigurationManager.AppSettings["nuget:ServiceResourceId"];

        public static X509Certificate2 Certificate = null;

        public static string Thumbprint = string.Empty;

        X509Certificate2 LoadCertificate()
        {
            string thumbprint = ConfigurationManager.AppSettings["nuget:Thumbprint"];
            Thumbprint = thumbprint;

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

        public void ConfigureAuth(IAppBuilder app)
        {
            Certificate = LoadCertificate();

            if (Certificate == null)
            {
                throw new Exception("Certificate == null");
            }

            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = clientId,
                    Authority = Authority,

                    TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = false
                    },

                    Notifications = new OpenIdConnectAuthenticationNotifications()
                    {
                        //
                        // If there is a code in the OpenID Connect response, redeem it for an access token and refresh token, and store those away.
                        //
                        AuthorizationCodeReceived = async (context) =>
                        {
                            //  This code gets the AccessToken for the AAD graph. This will be needed for some scenarios. However, it might 
                            //  be that we should ask for the services resource id at this stage. The AuthenticationResult includes a RefreshToken.
                            
                            var code = context.Code;

                            string signedInUserID = context.AuthenticationTicket.Identity.FindFirst(ClaimTypes.NameIdentifier).Value;
                            string tenantId = context.AuthenticationTicket.Identity.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

                            string authority = string.Format(aadInstance, tenantId);

                            AuthenticationContext authContext = new AuthenticationContext(authority, new NaiveSessionCache(signedInUserID));
                            ClientAssertionCertificate clientAssertionCertificate = new ClientAssertionCertificate(clientId, Certificate);
                            AuthenticationResult result = await authContext.AcquireTokenByAuthorizationCodeAsync(code, new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Path)), clientAssertionCertificate, graphResourceId);
                        },
                        RedirectToIdentityProvider = (context) =>
                        {
                            // This ensures that the address used for sign in and sign out is picked up dynamically from the request
                            // this allows you to deploy your app (to Azure Web Sites, for example)without having to change settings
                            // Remember that the base URL of the address used here must be provisioned in Azure AD beforehand.
                            string appBaseUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.PathBase;
                            context.ProtocolMessage.RedirectUri = appBaseUrl + "/";
                            context.ProtocolMessage.PostLogoutRedirectUri = appBaseUrl;
                            return Task.FromResult(0);
                        },
                        SecurityTokenReceived = (context) =>
                        {
                            return Task.FromResult(0);
                        },
                        AuthenticationFailed = (context) =>
                        {
                            //context.OwinContext.Response.Redirect("/Home/Error");
                            //context.HandleResponse(); // Suppress the exception
                            return Task.FromResult(0);
                        }
                    }
                });
        }
    }
}
