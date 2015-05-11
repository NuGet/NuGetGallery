// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.Operations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class CommandAttribute : Attribute
    {
        private string _description;
        private string _usageSummary;
        private string _usageDescription;
        private string _example;

        public string CommandName { get; private set; }
        public Type ResourceType { get; private set; }
        public string DescriptionResourceName { get; private set; }


        public string AltName { get; set; }
        public int MinArgs { get; set; }
        public int MaxArgs { get; set; }
        public string UsageSummaryResourceName { get; set; }
        public string UsageDescriptionResourceName { get; set; }
        public string UsageExampleResourceName { get; set; }
        public bool IsSpecialPurpose { get; set; }

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

        public string UsageSummary
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(UsageSummaryResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, UsageSummaryResourceName);
                }
                return _usageSummary;
            }
            set
            {
                _usageSummary = value;
            }
        }

        public string UsageDescription
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(UsageDescriptionResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, UsageDescriptionResourceName);
                }
                return _usageDescription;
            }
            set
            {
                _usageDescription = value;
            }
        }

        public string UsageExample
        {
            get
            {
                if (ResourceType != null && !String.IsNullOrEmpty(UsageExampleResourceName))
                {
                    return ResourceHelper.GetLocalizedString(ResourceType, UsageExampleResourceName);
                }
                return _example;
            }
            set
            {
                _example = value;
            }
        }

        public CommandAttribute(string commandName, string description)
        {
            CommandName = commandName;
            Description = description;
            MinArgs = 0;
            MaxArgs = Int32.MaxValue;
        }

        public CommandAttribute(Type resourceType, string commandName, string descriptionResourceName)
        {
            ResourceType = resourceType;
            CommandName = commandName;
            DescriptionResourceName = descriptionResourceName;
            MinArgs = 0;
            MaxArgs = Int32.MaxValue;
        }
    }
}
