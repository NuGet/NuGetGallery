// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using System;

using System;

namespace NuGetGallery.Auditing.Obfuscation
{

    public class ObfuscateAttribute(ObfuscationType obfuscationType) : Attribute
    {
        public ObfuscationType ObfuscationType { get; } = obfuscationType;
    }
}
