// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public sealed class OptimisticConcurrencyControlToken : IEquatable<OptimisticConcurrencyControlToken>
    {
        private readonly string _token;

        public static readonly OptimisticConcurrencyControlToken Null = new OptimisticConcurrencyControlToken(token: null);

        public OptimisticConcurrencyControlToken(string token)
        {
            _token = token;
        }

        public bool Equals(OptimisticConcurrencyControlToken other)
        {
            var azureToken = other as OptimisticConcurrencyControlToken;

            if (azureToken == null)
            {
                return false;
            }

            return _token == azureToken._token;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as OptimisticConcurrencyControlToken);
        }

        public override int GetHashCode()
        {
            return _token.GetHashCode();
        }

        public static bool operator ==(
            OptimisticConcurrencyControlToken token1,
            OptimisticConcurrencyControlToken token2)
        {
            if (((object)token1) == null || ((object)token2) == null)
            {
                return object.Equals(token1, token2);
            }

            return token1.Equals(token2);
        }

        public static bool operator !=(
            OptimisticConcurrencyControlToken token1,
            OptimisticConcurrencyControlToken token2)
        {
            if (((object)token1) == null || ((object)token2) == null)
            {
                return !object.Equals(token1, token2);
            }

            return !token1.Equals(token2);
        }
    }
}