// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

        public abstract string GetAction();

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

    public abstract class AuditRecord<T> : AuditRecord
        where T : struct
    {
        public T Action { get; set; }

        protected AuditRecord(T action)
        {
            Action = action;
        }

        public override string GetAction()
        {
            return Action.ToString().ToLowerInvariant();
        }
    }
}
