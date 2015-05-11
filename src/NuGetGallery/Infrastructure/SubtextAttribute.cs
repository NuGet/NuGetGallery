// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class SubtextAttribute : Attribute, IMetadataAware
    {
        private readonly object _typeId = new Object();

        public SubtextAttribute(string subtext)
        {
            Subtext = subtext;
        }

        public string Subtext { get; private set; }
        public bool AllowHtml { get; set; }

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
            metadata.AdditionalValues["Subtext"] = AllowHtml ? (object)new HtmlString(Subtext) : Subtext;
        }
    }
}