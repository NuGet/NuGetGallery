// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Threading;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class NonhijackableV2HttpHandlerResourceProvider : ResourceProvider
    {
        public NonhijackableV2HttpHandlerResourceProvider() : 
            base(typeof(HttpHandlerResource), 
                nameof(HttpHandlerResource),
                /// Must have higher priority than <see cref="HttpHandlerResourceV3Provider"/>
                nameof(HttpHandlerResourceV3Provider))
        {
            _innerResourceProvider = new HttpHandlerResourceV3Provider();
        }

        private HttpHandlerResourceV3Provider _innerResourceProvider;

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var resource = await _innerResourceProvider.TryCreate(source, token);

            if (resource.Item1)
            {
                var clientHandler = ((HttpHandlerResource)resource.Item2).ClientHandler;
                var messageHandler = new NonhijackedV2HttpMessageHandler(((HttpHandlerResource)resource.Item2).MessageHandler);

                var httpHandlerResource = new HttpHandlerResourceV3(clientHandler, messageHandler);

                resource = new Tuple<bool, INuGetResource>(httpHandlerResource != null, httpHandlerResource);
            }

            return resource;
        }
    }
}
