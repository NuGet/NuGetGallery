// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web.Http;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Extensions;
using System.Web.Http.OData.Routing.Conventions;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Csdl;
using Microsoft.Data.OData;
using NuGetGallery.OData;
using NuGetGallery.OData.Conventions;
using NuGetGallery.OData.Routing;

namespace NuGetGallery
{
    public static class NuGetODataV2FeedConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Build model
            var model = GetEdmModel();

            // Insert conventions to make NuGet-compatible OData feed possible
            var conventions = ODataRoutingConventions.CreateDefault();
            conventions.Insert(0, new EntitySetCountRoutingConvention());
            conventions.Insert(0, new ActionCountRoutingConvention("ODataV2Feed"));
            conventions.Insert(0, new MethodNameActionRoutingConvention("ODataV2Feed"));
            conventions.Insert(0, new EntitySetPropertyRoutingConvention("ODataV2Feed"));
            conventions.Insert(0, new CompositeKeyRoutingConvention());

            // Translate all requests to use V2FeedController instead of PackagesController
            conventions =
                conventions.Select(c => new ControllerAliasingODataRoutingConvention(c, "Packages", "ODataV2Feed"))
                    .Cast<IODataRoutingConvention>()
                    .ToList();

            // Add OData routes
            config.Routes.MapODataServiceRoute("api-v2-odata", "api/v2", model,
                new CountODataPathHandler(), conventions,
                new ODataServiceVersionHeaderPropagatingBatchHandler(GlobalConfiguration.DefaultServer));
            config.Routes.MapODataServiceRoute("api-v2curated-odata", "api/v2/curated-feeds/{curatedFeedName}", model,
                new CountODataPathHandler(), conventions,
                new ODataServiceVersionHeaderPropagatingBatchHandler(GlobalConfiguration.DefaultServer));
        }

        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();

            builder.DataServiceVersion = new Version(2, 0);
            builder.MaxDataServiceVersion = new Version(2, 0);
            builder.Namespace = "NuGetGallery";
            builder.ContainerName = "V2FeedContext";
            
            var packagesCollection = builder.EntitySet<V2FeedPackage>("Packages");
            packagesCollection.EntityType.HasKey(pkg => pkg.Id);
            packagesCollection.EntityType.HasKey(pkg => pkg.Version);

            var searchAction = builder.Action("Search");
            searchAction.Parameter<string>("searchTerm");
            searchAction.Parameter<string>("targetFramework");
            searchAction.Parameter<bool>("includePrerelease");
            searchAction.Parameter<string>("semVerLevel");
            searchAction.ReturnsCollectionFromEntitySet<V2FeedPackage>("Packages");

            var findPackagesAction = builder.Action("FindPackagesById");
            findPackagesAction.Parameter<string>("id");
            findPackagesAction.ReturnsCollectionFromEntitySet<V2FeedPackage>("Packages");

            var getUpdatesAction = builder.Action("GetUpdates");
            getUpdatesAction.Parameter<string>("packageIds");
            getUpdatesAction.Parameter<string>("versions");
            getUpdatesAction.Parameter<bool>("includePrerelease");
            getUpdatesAction.Parameter<bool>("includeAllVersions");
            getUpdatesAction.Parameter<string>("targetFrameworks");
            getUpdatesAction.Parameter<string>("versionConstraints");
            getUpdatesAction.Parameter<string>("semVerLevel");
            getUpdatesAction.ReturnsCollectionFromEntitySet<V2FeedPackage>("Packages");

            var model = builder.GetEdmModel();
            model.SetEdmVersion(new Version(1, 0));
            model.SetEdmxVersion(new Version(1, 0));
            model.SetHasDefaultStream(model.FindDeclaredType(typeof(V2FeedPackage).FullName) as IEdmEntityType, hasStream: true);
            
            return model;
        }
    }
}