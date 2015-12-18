// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using NuGet.Indexing;

namespace NuGet.Services.BasicSearch
{
    public class ServiceEndpoints
    {
        public static async Task V3SearchAsync(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            var skip = GetSkip(context);
            var take = GetTake(context);
            var includePrerelease = GetIncludePrerelease(context);
            var includeExplanation = GetIncludeExplanation(context);

            var q = context.Request.Query["q"] ?? string.Empty;
            var feed = context.Request.Query["feed"];
            var scheme = context.Request.Uri.Scheme;

            await ResponseHelpers.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceImpl.Search(jsonWriter, searcherManager, scheme, q, includePrerelease, skip, take, feed, includeExplanation));
        }

        public static async Task AutoCompleteAsync(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            var skip = GetSkip(context);
            var take = GetTake(context);
            var includePrerelease = GetIncludePrerelease(context);
            var includeExplanation = GetIncludeExplanation(context);

            var q = context.Request.Query["q"];
            var id = context.Request.Query["id"];
            if (q == null && id == null)
            {
                q = string.Empty;
            }

            await ResponseHelpers.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceImpl.AutoComplete(jsonWriter, searcherManager, q, id, includePrerelease, skip, take, includeExplanation));
        }

        public static async Task FindAsync(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            var id = context.Request.Query["id"] ?? string.Empty;
            var scheme = context.Request.Uri.Scheme;

            await ResponseHelpers.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceImpl.Find(jsonWriter, searcherManager, id, scheme));
        }

        public static async Task V2SearchAsync(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            var skip = GetSkip(context);
            var take = GetTake(context);
            var ignoreFilter = GetIgnoreFilter(context);
            var countOnly = GetCountOnly(context);
            var includePrerelease = GetIncludePrerelease(context);

            var q = context.Request.Query["q"] ?? string.Empty;
            var sortBy = context.Request.Query["sortBy"] ?? string.Empty;
            var feed = context.Request.Query["feed"];

            await ResponseHelpers.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => GalleryServiceImpl.Search(jsonWriter, searcherManager, q, countOnly, includePrerelease, sortBy, skip, take, feed, ignoreFilter));
        }

        public static async Task RankingsAsync(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            await ResponseHelpers.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceInfoImpl.Rankings(jsonWriter, searcherManager));
        }

        public static async Task Stats(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            await ResponseHelpers.WriteResponseAsync(
                context,
                HttpStatusCode.OK,
                jsonWriter => ServiceInfoImpl.Stats(jsonWriter, searcherManager));
        }

        private static bool GetIncludeExplanation(IOwinContext context)
        {
            bool includeExplanation;
            if (!bool.TryParse(context.Request.Query["explanation"], out includeExplanation))
            {
                includeExplanation = false;
            }

            return includeExplanation;
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