using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public abstract class PublishImpl
    {
        private readonly IRegistrationOwnership _registrationOwnership;

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
            Trace.TraceInformation("PublishImpl.Upload");

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

            IList<string> authorizationErrors = await OwnershipHelpers.CheckRegistrationAuthorization(_registrationOwnership, validationResult.PackageIdentity);

            if (authorizationErrors.Count > 0)
            {
                await ServiceHelpers.WriteErrorResponse(context, authorizationErrors, HttpStatusCode.Forbidden);
                return;
            }

            Trace.TraceInformation("UPLOAD Processing package {0}/{1}/{2}", validationResult.PackageIdentity.Namespace, validationResult.PackageIdentity.Id, validationResult.PackageIdentity.Version);

            //  process the package

            IDictionary<string, JObject> metadata = new Dictionary<string, JObject>();

            //  (1) save all the artifacts

            await Artifacts.Save(metadata, packageStream, Configuration.StoragePrimary, Configuration.StorageContainerArtifacts);

            Trace.TraceInformation("Save");

            InferArtifactTypes(metadata);

            //  (2) promote the relevant peices of metadata so they later can appear on the catalog page 

            ExtractMetadata(metadata, packageStream);

            Trace.TraceInformation("ExtractMetadata");

            //  (3) gather all the publication details

            PublicationDetails publicationDetails = await OwnershipHelpers.CreatePublicationDetails(_registrationOwnership, publicationVisibility);

            Trace.TraceInformation("CreatePublicationDetails");

            //  (4) add the new item to the catalog

            Uri catalogAddress = await AddToCatalog(metadata["nuspec"], GetItemType(), publicationDetails);

            Trace.TraceInformation("AddToCatalog");

            //  (5) update the registration ownership record

            await UpdateRegistrationOwnership(validationResult.PackageIdentity);

            Trace.TraceInformation("UpdateRegistrationOwnership");

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
            Trace.TraceInformation("PublishImpl.Edit");

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

            IList<string> authorizationErrors = await OwnershipHelpers.CheckRegistrationAuthorizationForEdit(_registrationOwnership, validationResult.PackageIdentity);

            if (authorizationErrors.Count > 0)
            {
                await ServiceHelpers.WriteErrorResponse(context, authorizationErrors, HttpStatusCode.Forbidden);
                return;
            }

            Trace.TraceInformation("EDIT Processing package {0}/{1}/{2}", validationResult.PackageIdentity.Namespace, validationResult.PackageIdentity.Id, validationResult.PackageIdentity.Version);

            //  process the edit

            IDictionary<string, JObject> metadata = new Dictionary<string, JObject>();

            //  (1) generate any new or replacement artifacts based on the current catalogEntry and the editMetadata

            IDictionary<string, PackageArtifact> artifacts = await GenerateNewArtifactsFromEdit(metadata, validationResult.CatalogEntry, validationResult.EditMetadata, Configuration.StoragePrimary);

            Trace.TraceInformation("GenerateNewArtifactsFromEdit");
            
            //  (2) save the new package

            await Artifacts.Save(metadata, artifacts, Configuration.StoragePrimary, Configuration.StorageContainerArtifacts);

            InferArtifactTypes(metadata);

            Trace.TraceInformation("Save");

            //  (3) promote the relevant peices of metadata so they later can appear on the catalog page 

            GenerateNuspec(metadata);

            Trace.TraceInformation("GenerateNuspec");

            //  (4) gather all the publication details

            PublicationDetails publicationDetails = await OwnershipHelpers.CreatePublicationDetails(_registrationOwnership, publicationVisibility);

            Trace.TraceInformation("CreatePublicationDetails");

            //  (5) add the new item to the catalog

            Uri catalogAddress = await AddToCatalog(metadata["nuspec"], GetItemType(), publicationDetails);

            Trace.TraceInformation("AddToCatalog");

            //  (6) update the registration ownership record

            await UpdateRegistrationOwnership(validationResult.PackageIdentity);

            Trace.TraceInformation("UpdateRegistrationOwnership");

            //  (7) create response

            JToken response = new JObject
            { 
                { "download", metadata["nuspec"]["packageContent"] },
                { "catalog", catalogAddress.ToString() }
            };

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        async Task<EditValidationResult> ValidateEdit(Stream metadataStream)
        {
            EditValidationResult result = new EditValidationResult();

            JObject metadata = await ServiceHelpers.ReadJObject(metadataStream);
            if (metadata != null)
            {
                ValidationHelpers.CheckDisallowedEditProperty(metadata, "namespace", result.Errors);
                ValidationHelpers.CheckDisallowedEditProperty(metadata, "id", result.Errors);
                ValidationHelpers.CheckDisallowedEditProperty(metadata, "version", result.Errors);

                if (result.Errors.Count > 0)
                {
                    // the edit request was invalid so don't waste any more cycles on this request 
                    return result;
                }

                result.EditMetadata = metadata;

                JToken catalogEntryAddress;
                if (metadata.TryGetValue("catalogEntry", out catalogEntryAddress))
                {
                    JObject catalogEntry = await CatalogHelpers.LoadFromCatalog(catalogEntryAddress.ToString(), Configuration.StoragePrimary, Configuration.StorageContainerCatalog, Configuration.CatalogBaseAddress);

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

        public async Task List(IOwinContext context)
        {
            Trace.TraceInformation("PublishImpl.List");

            ListValidationResult validationResult = await ValidateList(context.Request.Body);
        }

        async Task<ListValidationResult> ValidateList(Stream metadataStream)
        {
            ListValidationResult result = new ListValidationResult();

            JObject metadata = await ServiceHelpers.ReadJObject(metadataStream);
            if (metadata != null)
            {
                ValidationHelpers.CheckDisallowedEditProperty(metadata, "namespace", result.Errors);
                ValidationHelpers.CheckDisallowedEditProperty(metadata, "id", result.Errors);
                ValidationHelpers.CheckDisallowedEditProperty(metadata, "version", result.Errors);

                if (result.Errors.Count > 0)
                {
                    // the edit request was invalid so don't waste any more cycles on this request 
                    return result;
                }

                JToken listed;
                if (metadata.TryGetValue("listed", out listed))
                {
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

        static Task<Uri> AddToCatalog(JObject nuspec, Uri itemType, PublicationDetails publicationDetails)
        {
            CatalogItem catalogItem = new GraphCatalogItem(nuspec, itemType, publicationDetails);
            return CatalogHelpers.AddToCatalog(catalogItem, Configuration.StoragePrimary, Configuration.StorageContainerCatalog, Configuration.CatalogBaseAddress);
        }

        async Task UpdateRegistrationOwnership(PackageIdentity packageIdentity)
        {
            await _registrationOwnership.AddVersion(packageIdentity.Namespace, packageIdentity.Id, packageIdentity.Version.ToString());
        }
    }
}