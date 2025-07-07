// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Services.Models
{
    public class McpServerEntryTemplateResult : IEquatable<McpServerEntryTemplateResult>
    {
        public McpServerEntryResultValidity Validity { get; set; }
        public string Template { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as McpServerEntryTemplateResult);
        }

        public bool Equals(McpServerEntryTemplateResult other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Validity == other.Validity && string.Equals(Template, other.Template, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Validity.GetHashCode();
                hash = hash * 23 + Template?.GetHashCode() ?? 0;
                return hash;
            }
        }

        public static bool operator ==(McpServerEntryTemplateResult left, McpServerEntryTemplateResult right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(McpServerEntryTemplateResult left, McpServerEntryTemplateResult right)
        {
            return !(left == right);
        }
    }

    public enum McpServerEntryResultValidity
    {
        Success,
        MissingMetadata,
        MissingNugetRegistry,
        InvalidMetadata,
        Unset,
    }
}
