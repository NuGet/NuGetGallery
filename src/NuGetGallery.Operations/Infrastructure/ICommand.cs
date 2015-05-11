// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NuGetGallery.Operations
{
    [InheritedExport]
    public interface ICommand
    {
        CommandAttribute CommandAttribute { get; }

        IList<string> Arguments { get; }

        void Execute();
    }
}
