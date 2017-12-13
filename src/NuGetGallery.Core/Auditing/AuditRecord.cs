// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Auditing
{
    public abstract class AuditRecord
    {
        private string _resourceType;

        public abstract string GetPath();
        public abstract string GetAction();

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

    public abstract class AuditRecord<T>
        : AuditRecord
        where T : struct
    {
        protected AuditRecord(T action)
        {
            Action = action;
        }

        public T Action { get; set; }

        public override string GetAction()
        {
            return Action.ToString().ToLowerInvariant();
        }
    }
}
