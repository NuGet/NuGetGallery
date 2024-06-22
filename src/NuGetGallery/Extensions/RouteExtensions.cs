// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public static class RouteExtensions
    {
        public struct ObfuscatedPathMetadata
        {
            public int ObfuscatedSegment { get; }

            public string ObfuscatedSegmentValue { get; }

            public ObfuscatedPathMetadata(int obfuscatedSegment, string obfuscatedSegmentValue)
            {
                ObfuscatedSegment = obfuscatedSegment >= 0 ? obfuscatedSegment : throw new ArgumentOutOfRangeException(nameof(obfuscatedSegment));
                ObfuscatedSegmentValue = obfuscatedSegmentValue ?? throw new ArgumentNullException(nameof(obfuscatedSegmentValue));
            }
        }

        public struct ObfuscatedQueryMetadata
        {
            public string ObfuscatedQueryParameter { get; }

            public string ObfuscatedQueryParameterValue { get; }

            public ObfuscatedQueryMetadata(string obfuscatedQueryParameter, string obfuscatedQueryParameterValue)
            {
                ObfuscatedQueryParameter = obfuscatedQueryParameter ?? throw new ArgumentNullException(nameof(obfuscatedQueryParameter));
                ObfuscatedQueryParameterValue = obfuscatedQueryParameterValue ?? throw new ArgumentNullException(nameof(obfuscatedQueryParameterValue));
            }
        }

        internal static Dictionary<string, ObfuscatedPathMetadata[]> ObfuscatedRouteMap = new Dictionary<string, ObfuscatedPathMetadata[]>();
        internal static ObfuscatedQueryMetadata[] ObfuscatedReturnUrlMetadata = new ObfuscatedQueryMetadata[] 
        {
            new ObfuscatedQueryMetadata("returnUrl", Obfuscator.DefaultTelemetryReturnUrl),
            new ObfuscatedQueryMetadata("ReturnUrl", Obfuscator.DefaultTelemetryReturnUrl)
        };

        public static void MapRoute(this RouteCollection routes, string name, string url, object defaults, object constraints, ObfuscatedPathMetadata obfuscationMetadata)
        {
            routes.MapRoute(name, url, defaults, constraints);
            if (!ObfuscatedRouteMap.ContainsKey(url)) { ObfuscatedRouteMap.Add(url, new[] { obfuscationMetadata }); }
        }

        public static void MapRoute(this RouteCollection routes, string name, string url, object defaults, ObfuscatedPathMetadata obfuscationMetadata)
        {
            routes.MapRoute(name, url, defaults, new[] { obfuscationMetadata });
        }

        public static void MapRoute(
            this RouteCollection routes,
            string name,
            string url,
            object defaults,
            ObfuscatedPathMetadata[] obfuscationMetadatas)
        {
            routes.MapRoute(name, url, defaults, constraints: null, obfuscationMetadatas: obfuscationMetadatas);
        }

        public static void MapRoute(
            this RouteCollection routes,
            string name,
            string url,
            object defaults,
            object constraints,
            ObfuscatedPathMetadata[] obfuscationMetadatas)
        {
            routes.MapRoute(name, url, defaults, constraints);
            if (!ObfuscatedRouteMap.ContainsKey(url)) { ObfuscatedRouteMap.Add(url, obfuscationMetadatas); }
        }

        public static string ObfuscateUrlPath(this Route route, string urlPath)
        {
            var path = route.Url;
            if (!ObfuscatedRouteMap.TryGetValue(path, out var metadatas))
            {
                return null;
            }

            string[] segments = urlPath.Split('/');
            foreach (var metadata in metadatas)
            {
                segments[metadata.ObfuscatedSegment] = metadata.ObfuscatedSegmentValue;
            }
            return string.Join("/", segments);
        }

        public static Uri ObfuscateUrlQuery(Uri uri, ObfuscatedQueryMetadata[] metadata)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
            var uriQuery = uri.Query;
            if (string.IsNullOrEmpty(uriQuery))
            {
                return uri;
            }
            if (!metadata.Any())
            {
                return uri;
            }
            var parsedQuery = HttpUtility.ParseQueryString(uriQuery);
            var obfuscatedQueryItems = parsedQuery.AllKeys.Select((key) =>
            {
                return metadata.Where(qm => qm.ObfuscatedQueryParameter == key).Any() ?
                 $"{key}={metadata.Where(qm => qm.ObfuscatedQueryParameter == key).First().ObfuscatedQueryParameterValue}" :
                 $"{key}={parsedQuery.Get(key)}";
            });

            var portSegment = uri.IsDefaultPort ? "" : $":{uri.Port}";
            return new Uri($"{uri.Scheme}://{uri.Host}{portSegment}{uri.AbsolutePath}?{string.Join("&", obfuscatedQueryItems)}");
        }
    }
}