// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace System.Security.Cryptography
{
    public static class CryptographicAttributeObjectCollectionExtensions
    {
        /// <summary>
        /// Returns the first attribute if the Oid is found.
        /// Returns null if the attribute is not found.
        /// </summary>
        public static CryptographicAttributeObject FirstOrDefault(this CryptographicAttributeObjectCollection attributes, string oid)
        {
            if (oid == null)
            {
                throw new ArgumentNullException(nameof(oid));
            }

            foreach (var attribute in attributes)
            {
                if (StringComparer.Ordinal.Equals(oid, attribute.Oid.Value))
                {
                    return attribute;
                }
            }

            return null;
        }
    }
}
