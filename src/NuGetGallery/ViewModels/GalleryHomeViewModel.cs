// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class GalleryHomeViewModel
    {
        public bool ShowTransformModal { get; set; }

        public bool TransformIntoOrganization { get; set; }

        public string Identity { get; set; }

        public GalleryHomeViewModel() : this(showTransformModal: false, transformIntoOrganization: false, identity: null) { }

        public GalleryHomeViewModel(bool showTransformModal, bool transformIntoOrganization, string identity = null)
        {
            ShowTransformModal = showTransformModal;
            TransformIntoOrganization = transformIntoOrganization;
            Identity = identity;
        }
    }
}