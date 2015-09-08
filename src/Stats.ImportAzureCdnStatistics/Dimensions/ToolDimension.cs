// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.ImportAzureCdnStatistics
{
    public class ToolDimension
    {
        public ToolDimension(string toolId, string toolVersion, string fileName)
        {
            ToolId = toolId;
            ToolVersion = toolVersion;
            FileName = fileName;
        }

        public int Id { get; set; }
        public string ToolId { get; }
        public string ToolVersion { get; }
        public string FileName { get; }

        protected bool Equals(ToolDimension other)
        {
            return string.Equals(ToolId, other.ToolId) && string.Equals(ToolVersion, other.ToolVersion) && string.Equals(FileName, other.FileName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ToolDimension) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (ToolId != null ? ToolId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (ToolVersion != null ? ToolVersion.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (FileName != null ? FileName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}