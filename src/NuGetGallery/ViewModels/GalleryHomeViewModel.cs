// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class GalleryHomeViewModel
    {
        public bool ShowTransformModal { get; set; }

        public bool ShouldTransform { get; set; }

        public IList<AuthenticationProviderViewModel> Providers { get; set; }

        public GalleryHomeViewModel() : this(showTransformModal: false, canTransform: false) { }

        public GalleryHomeViewModel(bool showTransformModal, bool canTransform)
        {
            ShowTransformModal = showTransformModal;
            ShouldTransform = canTransform;
        }
    }
}