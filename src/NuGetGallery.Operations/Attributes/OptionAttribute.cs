// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.Operations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class OptionAttribute : Attribute
    {
        private string _description;

        public string AltName { get; set; }
        public string DescriptionResourceName { get; private set; }

        public string Description
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(DescriptionResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, DescriptionResourceName);
                }
                return _description;

            }
            private set
            {
                _description = value;
            }
        }

        public Type ResourceType { get; private set; }

        public OptionAttribute(string description)
        {
            Description = description;
        }

        public OptionAttribute(Type resourceType, string descriptionResourceName)
        {
            ResourceType = resourceType;
            DescriptionResourceName = descriptionResourceName;
        }
    }
}
