// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public class NuGetSearchTerm
    {
        public string Field { get; set; }

        public string TermOrPhrase { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as NuGetSearchTerm;
            if (other == null)
            {
                return false;
            }

            return string.Equals(Field, other.Field) && string.Equals(TermOrPhrase, other.TermOrPhrase);
        }

        public override int GetHashCode()
        {
            return Field.GetHashCode() ^ TermOrPhrase.GetHashCode();
        }
    }
}