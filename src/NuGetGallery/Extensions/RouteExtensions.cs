// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public static class RouteExtensions
    {
        public struct ObfuscatedMetadata
        {
            public int ObfuscatedSegment
            { get; }

            public string ObfuscateValue
            { get; }

            public ObfuscatedMetadata(int obfuscatedSegment, string obfuscateValue)
            {
                ObfuscatedSegment = obfuscatedSegment;
                ObfuscateValue = obfuscateValue;
            }
        }

        internal static Dictionary<string, ObfuscatedMetadata[]> ObfuscatedRouteMap = new Dictionary<string, ObfuscatedMetadata[]>();

        public static void MapRoute(this RouteCollection routes, string name, string url, object defaults, object constraints, ObfuscatedMetadata obfuscationMetadata)
        {
            routes.MapRoute(name, url, defaults, constraints);
            if (!ObfuscatedRouteMap.ContainsKey(url)) { ObfuscatedRouteMap.Add(url, new[] { obfuscationMetadata }); }
        }

        public static void MapRoute(this RouteCollection routes, string name, string url, object defaults, ObfuscatedMetadata obfuscationMetadata)
        {
            routes.MapRoute(name, url, defaults, new[] { obfuscationMetadata });
        }

        public static void MapRoute(this RouteCollection routes, string name, string url, object defaults, ObfuscatedMetadata[] obfuscationMetadatas)
        {
            routes.MapRoute(name, url, defaults);
            if (!ObfuscatedRouteMap.ContainsKey(url)) { ObfuscatedRouteMap.Add(url, obfuscationMetadatas); }
        }

        public static string ObfuscateUrlPath(this Route route, string urlPath)
        {
            var path = route.Url;
            if (!ObfuscatedRouteMap.ContainsKey(path))
            {
                return null;
            }
            var metadatas = ObfuscatedRouteMap[path];
            string[] segments = urlPath.Split('/');
            foreach (var metadata in metadatas)
            {
                segments[metadata.ObfuscatedSegment] = metadata.ObfuscateValue;
            }
            return string.Join("/", segments);
        }
    }
}