using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public class DeleteImpl
    {
        IRegistrationOwnership _registrationOwnership;

        public DeleteImpl(IRegistrationOwnership registrationOwnership)
        {
            _registrationOwnership = registrationOwnership;
        }

        public async Task Delete(IOwinContext context)
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

            IList<string> authorizationErrors = await OwnershipHelpers.CheckRegistrationAuthorizationForEdit(_registrationOwnership, validationResult.PackageIdentity);

            if (authorizationErrors.Count > 0)
            {
                await ServiceHelpers.WriteErrorResponse(context, authorizationErrors, HttpStatusCode.Forbidden);
                return;
            }

            //  process delete

            //  (1) gather all the publication details

            PublicationDetails publicationDetails = await OwnershipHelpers.CreatePublicationDetails(_registrationOwnership, publicationVisibility);

            //  (2) add the new item to the catalog

            Uri catalogAddress = await AddToCatalog(validationResult.PackageIdentity, publicationDetails);

            //  (3) update the registration ownership record

            await UpdateRegistrationOwnership(validationResult.PackageIdentity);

            //  (4) create response

            JToken response = new JObject
            { 
                { "catalog", catalogAddress.ToString() }
            };

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        async Task<ValidationResult> Validate(Stream requestStream)
        {
            ValidationResult result = new ValidationResult();

            JObject metadata;
            using (StreamReader reader = new StreamReader(requestStream))
            {
                try
                {
                    metadata = JObject.Parse(await reader.ReadToEndAsync());
                    result.PackageIdentity = ValidationHelpers.ValidateIdentity(metadata, result.Errors);
                }
                catch (FormatException)
                {
                    result.Errors.Add("request could not be parsed as JSON");
                }
            }

            return result;
        }

        static Task<Uri> AddToCatalog(PackageIdentity packageIdentity, PublicationDetails publicationDetails)
        {
            CatalogItem catalogItem = new DeleteCatalogItem(packageIdentity, publicationDetails);
            return CatalogHelpers.AddToCatalog(catalogItem, Configuration.StoragePrimary, Configuration.StorageContainerCatalog, Configuration.CatalogBaseAddress);
        }

        Task UpdateRegistrationOwnership(PackageIdentity packageIdentity)
        {
            //TODO: conditionally cleanup the package ownership record - we should be able to free up the id/version and possibly the id
            return Task.FromResult(0);
        }
    }
}