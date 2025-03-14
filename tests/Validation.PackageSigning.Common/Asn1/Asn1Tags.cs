// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    public static class Asn1Tags
    {
        public static readonly Asn1Tag ContextSpecific0 = new(TagClass.ContextSpecific, tagValue: 0);
        public static readonly Asn1Tag ContextSpecific1 = new(TagClass.ContextSpecific, tagValue: 1);
        internal static readonly Asn1Tag ContextSpecific2 = new(TagClass.ContextSpecific, tagValue: 2);
        internal static readonly Asn1Tag ContextSpecific3 = new(TagClass.ContextSpecific, tagValue: 3);
        internal static readonly Asn1Tag ContextSpecific4 = new(TagClass.ContextSpecific, tagValue: 4);
        internal static readonly Asn1Tag ContextSpecific5 = new(TagClass.ContextSpecific, tagValue: 5);
        internal static readonly Asn1Tag ContextSpecific6 = new(TagClass.ContextSpecific, tagValue: 6);
        internal static readonly Asn1Tag ContextSpecific7 = new(TagClass.ContextSpecific, tagValue: 7);
        internal static readonly Asn1Tag ContextSpecific8 = new(TagClass.ContextSpecific, tagValue: 8);
    }
}
