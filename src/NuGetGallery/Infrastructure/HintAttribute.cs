// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web.Mvc;

namespace NuGetGallery
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class HintAttribute : Attribute, IMetadataAware
    {
        private readonly object _typeId = new Object();

        public HintAttribute(string hint)
        {
            Hint = hint;
        }

        public string Hint { get; private set; }

        public override object TypeId
        {
            get { return _typeId; }
        }

        public void OnMetadataCreated(ModelMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }
            metadata.AdditionalValues["Hint"] = Hint;
        }
    }
}