// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Protocol.Catalog
{
    public interface ISimpleHttpClient
    {
        Task<byte[]> GetByteArrayAsync(string requestUri);
        T DeserializeBytes<T>(byte[] jsonBytes);
        Task<ResponseAndResult<T>> DeserializeUrlAsync<T>(string documentUrl);
    }
}