// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using NuGet.Indexing;
using NuGet.Versioning;

namespace NuGet.Services.BasicSearch
{
    public class ServiceEndpoints
    {
        public static async Task V3SearchAsync(IOwinContext context, NuGetSearcherManager searcherManager, ResponseWriter responseWriter)
        {
            var skip = GetSkip(context);
            var take = GetTake(context);
            var includePrerelease = GetIncludePrerelease(context);
            var semVerLevel = GetSemVerLevel(context);
            bool includeExplanation = GetBool(context, nameof(includeExplanation));

            var q = context.Request.Query["q"] ?? string.Empty;
            var feed = context.Request.Query["feed"];
            var scheme = context.Request.Uri.Scheme;

            await responseWriter.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceImpl.Search(jsonWriter, searcherManager, scheme, q, includePrerelease, semVerLevel, skip, take, feed, includeExplanation));
        }

        public static async Task AutoCompleteAsync(IOwinContext context, NuGetSearcherManager searcherManager, ResponseWriter responseWriter)
        {
            var skip = GetSkip(context);
            var take = GetTake(context);
            var includePrerelease = GetIncludePrerelease(context);
            var semVerLevel = GetSemVerLevel(context);
            bool explanation = GetBool(context, nameof(explanation));

            var q = context.Request.Query["q"];
            var id = context.Request.Query["id"];
            if (q == null && id == null)
            {
                q = string.Empty;
            }

            await responseWriter.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceImpl.AutoComplete(jsonWriter, searcherManager, q, id, includePrerelease, semVerLevel, skip, take, explanation));
        }

        public static async Task V2SearchAsync(IOwinContext context, NuGetSearcherManager searcherManager, ResponseWriter responseWriter)
        {
            var skip = GetSkip(context);
            var take = GetTake(context);
            var ignoreFilter = GetIgnoreFilter(context);
            var countOnly = GetCountOnly(context);
            var includePrerelease = GetIncludePrerelease(context);
            var semVerLevel = GetSemVerLevel(context);

            var q = context.Request.Query["q"] ?? string.Empty;
            var sortBy = context.Request.Query["sortBy"] ?? string.Empty;
            var feed = context.Request.Query["feed"];

            var luceneQuery = GetLuceneQuery(context);

            await responseWriter.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => GalleryServiceImpl.Search(jsonWriter, searcherManager, q, countOnly, includePrerelease, semVerLevel, sortBy, skip, take, feed, ignoreFilter, luceneQuery));
        }

        public static async Task Stats(IOwinContext context, NuGetSearcherManager searcherManager, ResponseWriter responseWriter)
        {
            await responseWriter.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceInfoImpl.Stats(jsonWriter, searcherManager));
        }

        private static bool GetBool(IOwinContext context, string queryKey)
        {
            bool value;
            if (!bool.TryParse(context.Request.Query[queryKey], out value))
            {
                value = false;
            }

            return value;
        }

        private static bool GetIncludePrerelease(IOwinContext context)
        {
            bool includePrerelease;
            if (!bool.TryParse(context.Request.Query["prerelease"], out includePrerelease))
            {
                includePrerelease = false;
            }

            return includePrerelease;
        }

        private static NuGetVersion GetSemVerLevel(IOwinContext context)
        {
            NuGetVersion semVerLevel;
            if (NuGetVersion.TryParse(context.Request.Query["semVerLevel"], out semVerLevel))
            {
                return semVerLevel;
            }

            return SemVerHelpers.SemVer1Level;
        }

        private static bool GetLuceneQuery(IOwinContext context)
        {
            bool luceneQuery;
            if (!bool.TryParse(context.Request.Query["luceneQuery"], out luceneQuery))
            {
                luceneQuery = true; // defaults to true
            }

            return luceneQuery;
        }

        private static int GetTake(IOwinContext context)
        {
            int take;
            if (!int.TryParse(context.Request.Query["take"], out take) || take < 1 || take > 1000)
            {
                take = 20;
            }

            return take;
        }

        private static int GetSkip(IOwinContext context)
        {
            int skip;
            if (!int.TryParse(context.Request.Query["skip"], out skip) || skip < 0)
            {
                skip = 0;
            }

            return skip;
        }

        private static bool GetIgnoreFilter(IOwinContext context)
        {
            bool ignoreFilter;
            if (!bool.TryParse(context.Request.Query["ignoreFilter"], out ignoreFilter))
            {
                ignoreFilter = false;
            }

            return ignoreFilter;
        }

        private static bool GetCountOnly(IOwinContext context)
        {
            bool countOnly;
            if (!bool.TryParse(context.Request.Query["countOnly"], out countOnly))
            {
                countOnly = false;
            }

            return countOnly;
        }
    }
}