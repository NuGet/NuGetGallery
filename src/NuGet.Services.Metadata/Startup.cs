using Microsoft.Owin;
using Microsoft.Owin.Security.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using NuGet.Indexing;
using Owin;
using System;
using System.Diagnostics;
using System.IdentityModel.Tokens;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

[assembly: OwinStartup(typeof(NuGet.Services.Metadata.Startup))]

namespace NuGet.Services.Metadata
{
    public class Startup
    {
        Timer _timer;
        SecureSearcherManager _searcherManager;
        int _gate;

        private static readonly ConfigurationService _configurationService;

        static Startup()
        {
            Trace.TraceInformation("Startup");

            _configurationService = new ConfigurationService();
        }

        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();

            string audience = _configurationService.Get("ida.Audience");
            string tenant = _configurationService.Get("ida.Tenant");
            string aadInstance = _configurationService.Get("ida.AADInstance");

            string metadataAddress = string.Format(aadInstance, tenant) + "/federationmetadata/2007-06/federationmetadata.xml";

            app.UseWindowsAzureActiveDirectoryBearerAuthentication(new WindowsAzureActiveDirectoryBearerAuthenticationOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = audience,
                    ValidateIssuer = true,
                    IssuerValidator = (string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters) => issuer
                },
                Tenant = tenant,
                MetadataAddress = metadataAddress
            });

            string searchIndexRefresh = _configurationService.Get("Search.IndexRefresh") ?? "180";
            int seconds;
            if (!int.TryParse(searchIndexRefresh, out seconds))
            {
                seconds = 60;
            }

            _searcherManager = null;

            _gate = 0;
            _timer = new Timer(ReopenCallback, 0, 10, seconds * 1000);

            app.Run(Invoke);
        }

        void ReopenCallback(object obj)
        {
            int val = Interlocked.Increment(ref _gate);
            if (val > 1)
            {
                Interlocked.Decrement(ref _gate);
                return;
            }

            if (_searcherManager == null)
            {
                TrySetSearcherManager();
            }
            else
            {
                TryMaybeReopen();
            }

            Interlocked.Decrement(ref _gate);
            return;
        }

        public void TrySetSearcherManager()
        {
            try
            {
                _searcherManager = CreateSearcherManager();
                _searcherManager.Open();
            }
            catch (FileNotFoundException)
            {
                _searcherManager = null;
            }
        }

        public void TryMaybeReopen()
        {
            try
            {
                _searcherManager.MaybeReopen();
            }
            catch (StorageException)
            {
                _searcherManager = null;
            }
        }

        public SecureSearcherManager CreateSearcherManager()
        {
            SecureSearcherManager searcherManager;

            string luceneDirectory = _configurationService.Get("Local.Lucene.Directory");
            if (!string.IsNullOrEmpty(luceneDirectory))
            {
                string dataDirectory = _configurationService.Get("Local.Data.Directory");
                searcherManager = SecureSearcherManager.CreateLocal(luceneDirectory);
            }
            else
            {
                string storagePrimary = _configurationService.Get("Storage.Primary");
                string searchIndexContainer = _configurationService.Get("Search.IndexContainer");

                searcherManager = SecureSearcherManager.CreateAzure(storagePrimary, searchIndexContainer);
            }

            string registrationBaseAddress = _configurationService.Get("Search.RegistrationBaseAddress");

            searcherManager.RegistrationBaseAddress["http"] = MakeRegistrationBaseAddress("http", registrationBaseAddress);
            searcherManager.RegistrationBaseAddress["https"] = MakeRegistrationBaseAddress("https", registrationBaseAddress);
            return searcherManager;
        }

        static Uri MakeRegistrationBaseAddress(string scheme, string registrationBaseAddress)
        {
            Uri original = new Uri(registrationBaseAddress);
            if (original.Scheme == scheme)
            {
                return original;
            }
            else
            {
                return new UriBuilder(original)
                {
                    Scheme = scheme,
                    Port = -1
                }.Uri;
            }
        }

        public async Task Invoke(IOwinContext context)
        {
            string error = null;

            try
            {
                if (_searcherManager == null)
                {
                    await context.Response.WriteAsync("no index loaded");
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }

                switch (context.Request.Path.Value)
                {
                    case "/":
                        await context.Response.WriteAsync("READY.");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        break;

                    case "/query":
                        await SecureQueryImpl.Query(context, _searcherManager, ServiceHelpers.GetTenant(), ServiceHelpers.GetNameIdentifier());
                        break;

                    case "/owner":
                        await SecureQueryImpl.QueryByOwner(context, _searcherManager, ServiceHelpers.GetTenant(), ServiceHelpers.GetNameIdentifier());
                        break;

                    case "/find":
                        await SecureFindImpl.Find(context, _searcherManager, ServiceHelpers.GetTenant());
                        break;

                    case "/segments":
                        await ServiceInfoImpl.Segments(context, _searcherManager);
                        break;

                    case "/stats":
                        await ServiceInfoImpl.Stats(context, _searcherManager);
                        break;

                    default:
                        string storagePrimary = _configurationService.Get("Storage.Primary");
                        MetadataImpl.Access(context, string.Empty, _searcherManager, storagePrimary, 30);
                        break;
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Invoke Exception: {0} {1}", e.GetType().Name, e.Message);

                error = e.Message;
            }

            if (error != null)
            {
                await Utils.WriteErrorResponse(context, error, HttpStatusCode.InternalServerError);
            }
        }
    }
}
