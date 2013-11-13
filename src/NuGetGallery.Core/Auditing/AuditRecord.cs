using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Auditing
{
    public abstract class AuditRecord
    {
        private string _resourceType;

        public abstract string GetPath();
        
        public virtual string GetResourceType()
        {
            return _resourceType ?? (_resourceType = InferResourceType());
        }

        private string InferResourceType()
        {
            string type = GetType().Name;
            if (type.EndsWith("AuditRecord", StringComparison.OrdinalIgnoreCase))
            {
                return type.Substring(0, type.Length - 11);
            }
            return type;
        }
    }
}
