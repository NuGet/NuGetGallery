# NuGet V3 Source Code

This repo contains nuget.org's implementation of the [NuGet V3 API](https://docs.microsoft.com/en-us/nuget/api/overview).

The following folders power the [search](https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource) and [autocomplete](https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource) resources:

* `NuGet.Jobs.Auxiliary2AzureSearch` - The job that updates miscellaneous data in the Azure Search index.
* `NuGet.Jobs.Catalog2AzureSearch` - The job that updates the Azure Search index when packages are uploaded or modified.
* `NuGet.Jobs.Db2AzureSearch` - The job that creates the Azure Search index using the Gallery database.
* `NuGet.Services.SearchService` - The nuget.org search service, powered by Azure Search.

Other interesting folders include:

* `Ng` -  The job that updates several V3 resources, including the [catalog](https://docs.microsoft.com/en-us/nuget/api/catalog-resource) and [package content](https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource) resources.
* `NuGet.Jobs.Catalog2Registration` - The job that updates the [package metadata](https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource) resource.