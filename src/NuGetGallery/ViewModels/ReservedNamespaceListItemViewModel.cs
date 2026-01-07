// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReservedNamespaceListItemViewModel
    {
        public string Value { get; }

        public bool IsPublic { get; }

        public bool IsPrefix { get; }

        public IEnumerable<User> Owners { get; }

        public ReservedNamespaceListItemViewModel(ReservedNamespace reservedNamespace)
        {
            Value = reservedNamespace.Value;
            IsPublic = reservedNamespace.IsSharedNamespace;
            IsPrefix = reservedNamespace.IsPrefix;
            Owners = reservedNamespace.Owners;
        }

        public string GetPattern()
        {
            var namespaceValue = Value;
            if (IsPrefix)
            {
                namespaceValue += "*";
            }

            return namespaceValue;
        }
    }
}