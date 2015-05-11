// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.Owin;
using Microsoft.Owin.Security.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Metadata.Catalog.Ownership;
using Owin;
using System;
using System.Diagnostics;
using System.IdentityModel.Tokens;
using System.Net;
using System.Threading.Tasks;

[assembly: OwinStartup(typeof(NuGet.Services.Publish.Startup))]

namespace NuGet.Services.Publish
{
    public class Startup
    {
        private static readonly ConfigurationService _configurationService;

        private static Uri _imagesUri;

        static Startup()
        {
            Trace.TraceInformation("Startup");

            _configurationService = new ConfigurationService();
        }

        public void Configuration(IAppBuilder app)
        {
            if (!HasNoSecurityConfigured())
            {
                Trace.TraceInformation("Using AAD middleware");

                string audience = _configurationService.Get("ida.Audience");
                string tenant = _configurationService.Get("ida.Tenant");
                string aadInstance = _configurationService.Get("ida.AADInstance");

                string metadataAddress = string.Format(aadInstance, tenant) + "/federationmetadata/2007-06/federationmetadata.xml";

                app.UseWindowsAzureActiveDirectoryBearerAuthentication(new WindowsAzureActiveDirectoryBearerAuthenticationOptions
                {
                    TokenValidationParameters = new TokenValidationParameters
                    {
                        SaveSigninToken = true,
                        ValidAudience = audience,
                        ValidateIssuer = true,
                        IssuerValidator = (string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters) => { return issuer; }
                    },
                    Tenant = tenant,
                    MetadataAddress = metadataAddress
                });
            }

            Initialize();

            app.Run(Invoke);
        }

        static void CreateContainer(string connectionString, string containerName)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);

            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);

            if (container.CreateIfNotExists())
            {
                string blobContainerPublicAccessType = _configurationService.Get("Storage.BlobContainerPublicAccessType") ?? "Off";

                Trace.TraceInformation("Crreating container {0} permission {1}", containerName, blobContainerPublicAccessType);

                switch (blobContainerPublicAccessType)
                {
                    case "Off": container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Off });
                        break;
                    case "Container": container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Container });
                        break;
                    case "Blob": container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
                        break;
                }
            }
        }

        static void Initialize()
        {
            string connectionString = _configurationService.Get("Storage.Primary");
            CreateContainer(connectionString, _configurationService.Get("Storage.Container.Artifacts"));
            CreateContainer(connectionString, _configurationService.Get("Storage.Container.Catalog"));
            //CreateContainer(connectionString, _configurationService.Get("Storage.Container.Ownership"));

            TableStorageRegistration.Initialize(connectionString);

            string images = _configurationService.Get("Storage.Container.Images");
            if (images == null)
            {
                images = "images";
            }
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(images);
            _imagesUri = container.Uri;
        }

        async Task Invoke(IOwinContext context)
        {
            string error = null;

            try
            {
                switch (context.Request.Method)
                {
                    case "GET":
                        await InvokeGET(context);
                        break;
                    case "POST":
                        await InvokePOST(context);
                        break;
                    default:
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
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
                await ServiceHelpers.WriteErrorResponse(context, error, HttpStatusCode.InternalServerError);
            }
        }

        async Task InvokeGET(IOwinContext context)
        {
            IRegistrationOwnership registrationOwnership = CreateRegistrationOwnership(context);

            switch (context.Request.Path.Value)
            {
                case "/":
                    {
                        await context.Response.WriteAsync("READY.");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        break;
                    }
                case "/domains":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.GetDomains(context);
                        break;
                    }
                case "/tenants":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.GetTenants(context);
                        break;
                    }
                default:
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }
            }
        }

        async Task InvokePOST(IOwinContext context)
        {
            IRegistrationOwnership registrationOwnership = CreateRegistrationOwnership(context);
            ICategorizationPermission categorizationPermission = CategorizationPermission();

            switch (context.Request.Path.Value)
            {
                case "/apiapp/checkaccess":
                    {
                        CheckAccessImpl uploader = new CheckAccessImpl(registrationOwnership);
                        await uploader.CheckAccess(context);
                        break;
                    }
                case "/apiapp/upload":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership, categorizationPermission, _imagesUri);
                        await uploader.Upload(context);
                        break;
                    }
                case "/apiapp/edit":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership, categorizationPermission, _imagesUri);
                        await uploader.Edit(context);
                        break;
                    }
                case "/delete":
                    {
                        DeleteImpl deleteImpl = new DeleteImpl(registrationOwnership);
                        await deleteImpl.Delete(context);
                        break;
                    }
                case "/tenant/enable":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.TenantEnable(context);
                        break;
                    }
                case "/tenant/disable":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.TenantDisable(context);
                        break;
                    }
                case "/catalog/powershell":
                    {
                        PublishImpl uploader = new PowerShellPublishImpl(registrationOwnership);
                        await uploader.Upload(context);
                        break;
                    }
                default:
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }
            }
        }

        IRegistrationOwnership CreateRegistrationOwnership(IOwinContext context)
        {
            if (HasNoSecurityConfigured())
            {
                return new NoSecurityRegistrationOwnership();
            }

            //string storagePrimary = _configurationService.Get("Storage.Primary");
            //string storageContainerOwnership = _configurationService.Get("Storage.Container.Ownership") ?? "ownership";
            //CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);
            //return new StorageRegistrationOwnership(context, account, storageContainerOwnership);

            string storagePrimary = _configurationService.Get("Storage.Primary");
            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);
            return new StorageRegistrationOwnership(context, account);
        }

        ICategorizationPermission CategorizationPermission()
        {
            string storagePrimary = _configurationService.Get("Storage.Primary");
            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);
            return new StorageCategorizationPermission(account);
        }

        bool HasNoSecurityConfigured()
        {
            string noSecurity = _configurationService.Get("NoSecurity");
            return (!string.IsNullOrEmpty(noSecurity) && noSecurity.Equals("true", StringComparison.InvariantCultureIgnoreCase));
        }
    }
}