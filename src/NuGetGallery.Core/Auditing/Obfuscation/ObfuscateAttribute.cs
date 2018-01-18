// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using System;

using System;

namespace NuGetGallery.Auditing.Obfuscation
{

    public class ObfuscateAttribute : Attribute
    {
        public ObfuscationType ObfuscationType { get; } 

        public ObfuscateAttribute(ObfuscationType obfuscationType)
        {
            ObfuscationType = obfuscationType;
        }
    }
}
