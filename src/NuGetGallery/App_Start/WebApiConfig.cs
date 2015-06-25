// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Http;
using Microsoft.AspNet.WebApi.MessageHandlers.Compression;
using Microsoft.AspNet.WebApi.MessageHandlers.Compression.Compressors;
using Ninject.Web.WebApi;

namespace NuGetGallery
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Add Ninject support
            config.DependencyResolver = new NinjectDependencyResolver(Container.Kernel);

            // Enable compression
            config.MessageHandlers.Insert(0, new ServerCompressionHandler(new GZipCompressor(), new DeflateCompressor()));
        }
    }
}