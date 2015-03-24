using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Ownership;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public abstract class PublishImpl
    {
        private const string DefaultStorageContainerCatalog = "catalog";
        private const string DefaultStorageContainerPackages = "artifacts";
        private const string DefaultStorageContainerOwnership = "ownership";

        private static readonly string _storagePrimary;
        private static readonly string _storageContainerCatalog;
        private static readonly string _storageContainerPackages;
        private static readonly string _storageContainerOwnership;
        private static readonly string _catalogBaseAddress;

        private readonly IRegistrationOwnership _registrationOwnership;

        static PublishImpl()
        {
            var configurationService = new ConfigurationService();

            _storagePrimary = configurationService.Get("Storage.Primary");
            _storageContainerCatalog = configurationService.Get("Storage.Container.Catalog") ?? DefaultStorageContainerCatalog;
            _storageContainerPackages = configurationService.Get("Storage.Container.Artifacts") ?? DefaultStorageContainerPackages;
            _storageContainerOwnership = configurationService.Get("Storage.Container.Ownership") ?? DefaultStorageContainerOwnership;
            _catalogBaseAddress = configurationService.Get("Catalog.BaseAddress");
        }

        protected PublishImpl(IRegistrationOwnership registrationOwnership)
        {
            _registrationOwnership = registrationOwnership;
        }

        protected abstract bool IsMetadataFile(string fullName);
        protected abstract JObject CreateMetadataObject(string fullname, Stream stream);
        protected abstract Uri GetItemType();

        protected virtual Task<ValidationResult> Validate(Stream nupkgStream)
        {
            return Task.FromResult(new ValidationResult());
        }

        protected virtual void ValidateEdit(EditValidationResult result)
        {
        }

        protected virtual void InferArtifactTypes(IDictionary<string, JObject> metadata)
        {
        }

        protected virtual void GenerateNuspec(IDictionary<string, JObject> metadata)
        {
        }

        protected virtual Task<IDictionary<string, PackageArtifact>> GenerateNewArtifactsFromEdit(IDictionary<string, JObject> metadata, JObject catalogEntry, JObject editMetadata, string storagePrimary)
        {
            return Task.FromResult<IDictionary<string, PackageArtifact>>(new Dictionary<string, PackageArtifact>());
        }

        public async Task Upload(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.HasTenantEnabled())
            {
                await ServiceHelpers.WriteErrorResponse(context, "package publication has not been enabled in this tenant", HttpStatusCode.Forbidden);
                return;
            }

            PublicationVisibility publicationVisibility;
            if (!PublicationVisibility.TryCreate(context, out publicationVisibility))
            {
                await ServiceHelpers.WriteErrorResponse(context, "specify either organization OR subscription NOT BOTH", HttpStatusCode.BadRequest);
                return;
            }

            Stream packageStream = context.Request.Body;

            //  validation

            ValidationResult validationResult = await Validate(packageStream);

            if (validationResult.HasErrors)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationResult.Errors, HttpStatusCode.BadRequest);
                return;
            }

            //  registration authorization

            IList<string> authorizationErrors = await CheckRegistrationAuthorization(validationResult.PackageIdentity);

            if (authorizationErrors.Count > 0)
            {
                await ServiceHelpers.WriteErrorResponse(context, authorizationErrors, HttpStatusCode.Forbidden);
                return;
            }

            //  process the package

            IDictionary<string, JObject> metadata = new Dictionary<string, JObject>();

            //  (1) save all the artifacts

            await Artifacts.Save(metadata, packageStream, _storagePrimary, _storageContainerPackages);

            InferArtifactTypes(metadata);

            //  (2) promote the relevant peices of metadata so they later can appear on the catalog page 

            ExtractMetadata(metadata, packageStream);

            //  (3) gather all the publication details

            PublicationDetails publicationDetails = await CreatePublicationDetails(publicationVisibility);

            //  (4) add the new item to the catalog

            Uri catalogAddress = await AddToCatalog(metadata["nuspec"], GetItemType(), publicationDetails);

            //  (5) update the registration ownership record

            await UpdateRegistrationOwnership(validationResult.PackageIdentity);

            //  (6) create response

            JToken response = new JObject
            { 
                { "download", metadata["nuspec"]["packageContent"] },
                { "catalog", catalogAddress.ToString() }
            };

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        public async Task Edit(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.HasTenantEnabled())
            {
                await ServiceHelpers.WriteErrorResponse(context, "package publication has not been enabled in this tenant", HttpStatusCode.Forbidden);
                return;
            }

            PublicationVisibility publicationVisibility;
            if (!PublicationVisibility.TryCreate(context, out publicationVisibility))
            {
                await ServiceHelpers.WriteErrorResponse(context, "specify either organization OR subscription NOT BOTH", HttpStatusCode.BadRequest);
                return;
            }

            Stream metadataStream = context.Request.Body;

            //  validation

            EditValidationResult validationResult = await ValidateEdit(metadataStream);

            if (validationResult.HasErrors)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationResult.Errors, HttpStatusCode.BadRequest);
                return;
            }

            //  registration authorization

            IList<string> authorizationErrors = await CheckRegistrationAuthorizationForEdit(validationResult.PackageIdentity);

            if (authorizationErrors.Count > 0)
            {
                await ServiceHelpers.WriteErrorResponse(context, authorizationErrors, HttpStatusCode.Forbidden);
                return;
            }

            //  process the edit

            IDictionary<string, JObject> metadata = new Dictionary<string, JObject>();

            //  (1) generate any new or replacement artifacts based on the current catalogEntry and the editMetadata

            IDictionary<string, PackageArtifact> artifacts = await GenerateNewArtifactsFromEdit(metadata, validationResult.CatalogEntry, validationResult.EditMetadata, StoragePrimary);
            
            //  (2) save the new package

            await Artifacts.Save(metadata, artifacts, StoragePrimary, StorageContainerPackages);

            InferArtifactTypes(metadata);

            //  (3) promote the relevant peices of metadata so they later can appear on the catalog page 

            GenerateNuspec(metadata);

            //  (4) gather all the publication details

            PublicationDetails publicationDetails = await CreatePublicationDetails(publicationVisibility);

            //  (5) add the new item to the catalog

            Uri catalogAddress = await AddToCatalog(metadata["nuspec"], GetItemType(), publicationDetails);

            //  (6) update the registration ownership record

            await UpdateRegistrationOwnership(validationResult.PackageIdentity);

            //  (7) create response

            JToken response = new JObject
            { 
                { "download", metadata["nuspec"]["packageContent"] },
                { "catalog", catalogAddress.ToString() }
            };

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        async Task<IList<string>> CheckRegistrationAuthorization(PackageIdentity packageIdentity)
        {
            IList<string> errors = new List<string>();

            if (await _registrationOwnership.HasRegistration(packageIdentity.Namespace, packageIdentity.Id))
            {
                if (!await _registrationOwnership.HasOwner(packageIdentity.Namespace, packageIdentity.Id))
                {
                    errors.Add("user does not have access to this registration");
                    return errors;
                }

                if (await _registrationOwnership.HasVersion(packageIdentity.Namespace, packageIdentity.Id, packageIdentity.Version.ToString()))
                {
                    errors.Add("this package version already exists for this registration");
                    return errors;
                }
            }

            return errors;
        }

        async Task<IList<string>> CheckRegistrationAuthorizationForEdit(PackageIdentity packageIdentity)
        {
            IList<string> errors = new List<string>();

            if (await _registrationOwnership.HasRegistration(packageIdentity.Namespace, packageIdentity.Id))
            {
                if (!await _registrationOwnership.HasOwner(packageIdentity.Namespace, packageIdentity.Id))
                {
                    errors.Add("user does not have access to this registration");
                    return errors;
                }
            }
            else
            {
                errors.Add("this package does not exits in the ownership record");
            }

            return errors;
        }

        async Task<PublicationDetails> CreatePublicationDetails(PublicationVisibility publicationVisibility)
        {
            string userId = _registrationOwnership.GetUserId();
            string userName = await _registrationOwnership.GetPublisherName();
            string tenantId = _registrationOwnership.GetTenantId();

            //TODO: requires Graph access
            string tenantName = string.Empty;
            //string tenantName = await _registrationOwnership.GetTenantName();

            //var client = await ServiceHelpers.GetActiveDirectoryClient();

            PublicationDetails publicationDetails = new PublicationDetails
            {
                Published = DateTime.UtcNow,
                Owner = OwnershipOwner.Create(ClaimsPrincipal.Current),
                TenantId = tenantId,
                TenantName = tenantName,
                Visibility = publicationVisibility
            };

            return publicationDetails;
        }


        async Task<EditValidationResult> ValidateEdit(Stream metadataStream)
        {
            EditValidationResult result = new EditValidationResult();

            JObject editMetadata = await ServiceHelpers.ReadJObject(metadataStream);
            if (editMetadata != null)
            {
                result.EditMetadata = editMetadata;

                JToken catalogEntryAddress;
                if (editMetadata.TryGetValue("catalogEntry", out catalogEntryAddress))
                {
                    JObject catalogEntry = await LoadFromCatalog(catalogEntryAddress.ToString());

                    if (catalogEntry != null)
                    {
                        result.CatalogEntry = catalogEntry;
                        result.PackageIdentity = PackageIdentity.FromCatalogEntry(catalogEntry);

                        ValidateEdit(result);
                    }
                    else
                    {
                        result.Errors.Add("unable to load catalogEntry");
                    }
                }
                else
                {
                    result.Errors.Add("corresponding catalogEntry must be specified");
                }
            }
            else
            {
                result.Errors.Add("unable to read content as JSON");
            }

            return result;
        }

        public async Task GetDomains(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            IEnumerable<string> domains = await _registrationOwnership.GetDomains();
            await ServiceHelpers.WriteResponse(context, new JArray(domains.ToArray()), HttpStatusCode.OK);
        }

        public async Task GetTenants(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            IEnumerable<string> tenants = await _registrationOwnership.GetTenants();
            await ServiceHelpers.WriteResponse(context, new JArray(tenants.ToArray()), HttpStatusCode.OK);
        }

        public async Task TenantEnable(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.IsUserAdministrator())
            {
                await ServiceHelpers.WriteErrorResponse(context, "this operation is only permitted for administrators", HttpStatusCode.Forbidden);
                return;
            }

            await _registrationOwnership.EnableTenant();

            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }

        public async Task TenantDisable(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.IsUserAdministrator())
            {
                await ServiceHelpers.WriteErrorResponse(context, "this operation is only permitted for administrators", HttpStatusCode.Forbidden);
                return;
            }

            await _registrationOwnership.DisableTenant();

            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }

        void ExtractMetadata(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            using (ZipArchive archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (IsMetadataFile(entry.FullName))
                    {
                        using (Stream stream = entry.Open())
                        {
                            metadata.Add(entry.FullName, CreateMetadataObject(entry.FullName, stream));
                        }
                    }
                }
            }

            if (!metadata.ContainsKey("nuspec"))
            {
                GenerateNuspec(metadata);
            }
        }

        static async Task<Uri> AddToCatalog(JObject nuspec, Uri itemType, PublicationDetails publicationDetails)
        {
            StorageWriteLock writeLock = new StorageWriteLock(_storagePrimary, _storageContainerCatalog);

            await writeLock.AquireAsync();

            Uri rootUri = null;

            Exception exception = null;
            try
            {
                Storage storage = CreateStorage();

                AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage);
                writer.Add(new GraphCatalogItem(nuspec, itemType, publicationDetails));
                await writer.Commit();

                rootUri = writer.RootUri;
            }
            catch (Exception e)
            {
                exception = e;
            }

            await writeLock.ReleaseAsync();

            if (exception != null)
            {
                throw exception;
            }

            return rootUri;
        }

        async Task<JObject> LoadFromCatalog(string catalogEntryAddress)
        {
            Storage storage = CreateStorage();
            string json = await storage.LoadString(new Uri(catalogEntryAddress));
            return JObject.Parse(json);
        }

        static Storage CreateStorage()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(StoragePrimary);

            Storage storage;
            if (CatalogBaseAddress == null)
            {
                storage = new AzureStorage(account, StorageContainerCatalog);
            }
            else
            {
                string baseAddress = CatalogBaseAddress.TrimEnd('/') + "/" + StorageContainerCatalog;

                storage = new AzureStorage(account, StorageContainerCatalog, string.Empty, new Uri(baseAddress));
            }

            return storage;
        }

        async Task UpdateRegistrationOwnership(PackageIdentity packageIdentity)
        {
            StorageWriteLock writeLock = new StorageWriteLock(_storagePrimary, _storageContainerOwnership);

            await writeLock.AquireAsync();

            Exception exception = null;
            try
            {
                await _registrationOwnership.AddVersion(packageIdentity.Namespace, packageIdentity.Id, packageIdentity.Version.ToString());
            }
            catch (Exception e)
            {
                exception = e;
            }

            await writeLock.ReleaseAsync();

            if (exception != null)
            {
                throw exception;
            }
        }
    }
}