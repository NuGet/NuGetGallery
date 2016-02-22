// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData.Batch;

namespace NuGetGallery.OData
{
    public class ODataServiceVersionHeaderPropagatingBatchHandler 
        : DefaultODataBatchHandler
    {
        private const string DataServiceVersionHeader = "DataServiceVersion";
        private const string MaxDataServiceVersionHeader = "MaxDataServiceVersion";

        public ODataServiceVersionHeaderPropagatingBatchHandler(HttpServer httpServer) 
            : base(httpServer)
        {
        }

        public override async Task<IList<ODataBatchRequestItem>> ParseBatchRequestsAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var items = await base.ParseBatchRequestsAsync(request, cancellationToken);

            CopyRequestHeadersToBatchItems(request, items);

            return items;
        }

        /// <summary>
        /// OData does not copy headers from the main request to the batch sub-request. Let's do it here...
        /// </summary>
        public virtual void CopyRequestHeadersToBatchItems(HttpRequestMessage request, IList<ODataBatchRequestItem> items)
        {
            var batchRequests = items
                .OfType<OperationRequestItem>().Select(o => o.Request)
                .Union(items.OfType<ChangeSetRequestItem>().SelectMany(cs => cs.Requests));

            foreach (var batchRequest in batchRequests)
            {
                foreach (var header in request.Headers)
                {
                    if (String.Equals(header.Key, DataServiceVersionHeader, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(header.Key, MaxDataServiceVersionHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        batchRequest.Headers.Add(header.Key, request.Headers.GetValues(header.Key));
                    }
                }
            }
        }
    }
}