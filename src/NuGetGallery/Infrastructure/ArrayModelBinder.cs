// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ArrayModelBinder<T> : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var isListType = bindingContext.ModelType.IsAssignableFrom(typeof(List<T>));
            var isArrayType = bindingContext.ModelType.IsAssignableFrom(typeof(T[]));
            if (!isListType && !isArrayType)
            {
                throw new InvalidOperationException($"{nameof(ArrayModelBinder<T>)} does not support models of type {bindingContext.ModelType.FullName}");
            }

            var values = new List<T>();
            var i = 0;
            string nextItemName;
            while (bindingContext.ValueProvider.ContainsPrefix((nextItemName = $"{bindingContext.ModelName}[{i++}]")))
            {
                var valueProviderResult = bindingContext.ValueProvider.GetValue(nextItemName);
                values.Add((T)valueProviderResult.ConvertTo(typeof(T)));
            }

            if (isListType)
            {
                return values;
            }
            else if (isArrayType)
            {
                return values.ToArray();
            }

            return null;
        }
    }
}