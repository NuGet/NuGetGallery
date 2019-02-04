// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum SimulatedErrorType
    {
        Result400,
        Result404,
        Result500,
        Result503,
        HttpException400,
        HttpException404,
        HttpException500,
        HttpException503,
        HttpResponseException400,
        HttpResponseException404,
        HttpResponseException500,
        HttpResponseException503,
        Exception,
        UserSafeException,
        ReadOnlyMode,
    }
}
