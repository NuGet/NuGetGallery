// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

        protected virtual Task GenerateNuspec(IDictionary<string, JObject> metadata)
        {
            return Task.FromResult(0);
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

            //  no-commit mode - used for just running the validation

            bool isCommit = GetIsCommit(context);

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

            //  listed

            bool isListed = true;
            string unlist = context.Request.Query["unlist"];
            if (unlist != null)
            {
                isListed = !unlist.Equals(Boolean.TrueString, StringComparison.InvariantCultureIgnoreCase);
            }

            Trace.TraceInformation("UPLOAD Processing package {0}/{1}/{2} isListed: {3} isCommit: {4}", validationResult.PackageIdentity.Namespace, validationResult.PackageIdentity.Id, validationResult.PackageIdentity.Version, isListed, isCommit);

            //  process the package

            IDictionary<string, JObject> metadata = new Dictionary<string, JObject>();

            //  (1) save all the artifacts

            if (isCommit)
            {
                await Artifacts.Save(metadata, packageStream, Configuration.StoragePrimary, Configuration.StorageContainerArtifacts);

                Trace.TraceInformation("Save");
            }

            InferArtifactTypes(metadata);

            //  (2) promote the relevant peices of metadata so they later can appear on the catalog page 

            await ExtractMetadata(metadata, packageStream);

            Trace.TraceInformation("ExtractMetadata");

            //  (3) gather all the publication details

            PublicationDetails publicationDetails = await OwnershipHelpers.CreatePublicationDetails(_registrationOwnership, publicationVisibility);

            Trace.TraceInformation("CreatePublicationDetails");

            //  (4) add the new item to the catalog

            Uri catalogAddress = null;

            if (isCommit)
            {
                catalogAddress = await AddToCatalog(metadata["nuspec"], GetItemType(), publicationDetails, isListed);

                Trace.TraceInformation("AddToCatalog");
            }

            //  (5) update the registration ownership record

            if (isCommit)
            {
                await UpdateRegistrationOwnership(validationResult.PackageIdentity);

                Trace.TraceInformation("UpdateRegistrationOwnership");
            }

            //  (6) create response

            if (isCommit)
            {
                JToken response = new JObject
                { 
                    { "download", metadata["nuspec"]["packageContent"] },
                    { "catalog", catalogAddress.ToString() }
                };

                await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.Created);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
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

            Trace.TraceInformation("EDIT Processing package {0}/{1}/{2} listed: {3}", validationResult.PackageIdentity.Namespace, validationResult.PackageIdentity.Id, validationResult.PackageIdentity.Version, validationResult.Listed);

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

            await GenerateNuspec(metadata);

            Trace.TraceInformation("GenerateNuspec");

            //  (4) gather all the publication details

            PublicationDetails publicationDetails = await OwnershipHelpers.CreatePublicationDetails(_registrationOwnership, publicationVisibility);

            Trace.TraceInformation("CreatePublicationDetails");

            //  (5) add the new item to the catalog

            Uri catalogAddress = await AddToCatalog(metadata["nuspec"], GetItemType(), publicationDetails, validationResult.Listed);

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

                        JToken isListed;
                        if (metadata.TryGetValue("listed", out isListed))
                        {
                            if (isListed.Type != JTokenType.Boolean)
                            {
                                result.Errors.Add("listed must be a boolean value");
                            }
                            else
                            {
                                result.Listed = isListed.ToObject<bool>();
                            }
                        }
                        else
                        {
                            result.Listed = catalogEntry["listed"].ToObject<bool>();
                        }

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

        bool GetIsCommit(IOwinContext context)
        {
            string s = context.Request.Query["commit"];
            if (s != null)
            {
                return !s.Equals("false", StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        async Task ExtractMetadata(IDictionary<string, JObject> metadata, Stream nupkgStream)
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
                await GenerateNuspec(metadata);
            }
        }

        static Task<Uri> AddToCatalog(JObject nuspec, Uri itemType, PublicationDetails publicationDetails, bool isListed)
        {
            CatalogItem catalogItem = new GraphCatalogItem(nuspec, itemType, publicationDetails, isListed);
            return CatalogHelpers.AddToCatalog(catalogItem, Configuration.StoragePrimary, Configuration.StorageContainerCatalog, Configuration.CatalogBaseAddress);
        }

        async Task UpdateRegistrationOwnership(PackageIdentity packageIdentity)
        {
            await _registrationOwnership.AddVersion(packageIdentity.Namespace, packageIdentity.Id, packageIdentity.Version.ToString());
        }
    }
}