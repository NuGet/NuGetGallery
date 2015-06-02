// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace System.IO
{
    internal static class StreamExtensions
    {
        public static async Task<string> GetStringAsync(this Stream stream)
        {
            string receivedMessage;
            using (var streamReader = new StreamReader(stream, Encoding.ASCII))
            {
                receivedMessage = await streamReader.ReadToEndAsync();
            }
            return receivedMessage;
        }
    }
}