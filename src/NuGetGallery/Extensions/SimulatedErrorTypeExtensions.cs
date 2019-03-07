// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace NuGetGallery
{
    public static class SimulatedErrorTypeExtensions
    {
        public static HttpResponseMessage MapToWebApiResult(this SimulatedErrorType type)
        {
            var message = type.GetMessage();
            switch (type)
            {
                case SimulatedErrorType.Result400:
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        ReasonPhrase = message,
                    };
                case SimulatedErrorType.Result404:
                    return new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        ReasonPhrase = message,
                    };
                case SimulatedErrorType.Result500:
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        ReasonPhrase = message,
                    };
                case SimulatedErrorType.Result503:
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        ReasonPhrase = message,
                    };
                default:
                    throw type.MapToException();
            }
        }

        public static ActionResult MapToMvcResult(this SimulatedErrorType type)
        {
            var message = type.GetMessage();
            switch (type)
            {
                case SimulatedErrorType.Result400:
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, message);
                case SimulatedErrorType.Result404:
                    return new HttpStatusCodeResult(HttpStatusCode.NotFound, message);
                case SimulatedErrorType.Result500:
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, message);
                case SimulatedErrorType.Result503:
                    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, message);
                default:
                    throw type.MapToException();
            }
        }
        public static string GetMessage(this SimulatedErrorType type)
        {
            return $"{nameof(SimulatedErrorType)} {type}";
        }

        public static Exception MapToException(this SimulatedErrorType type)
        {
            var message = type.GetMessage();
            switch (type)
            {
                case SimulatedErrorType.HttpException400:
                    return new HttpException(400, message);
                case SimulatedErrorType.HttpException404:
                    return new HttpException(404, message);
                case SimulatedErrorType.HttpException500:
                    return new HttpException(500, message);
                case SimulatedErrorType.HttpException503:
                    return new HttpException(503, message);
                case SimulatedErrorType.HttpResponseException400:
                    return new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        ReasonPhrase = message,
                    });
                case SimulatedErrorType.HttpResponseException404:
                    return new HttpResponseException(new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        ReasonPhrase = message,
                    });
                case SimulatedErrorType.HttpResponseException500:
                    return new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        ReasonPhrase = message,
                    });
                case SimulatedErrorType.HttpResponseException503:
                    return new HttpResponseException(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        ReasonPhrase = message,
                    });
                case SimulatedErrorType.Exception:
                    return new Exception(message);
                case SimulatedErrorType.UserSafeException:
                    return new UserSafeException(message);
                case SimulatedErrorType.ReadOnlyMode:
                    return new ReadOnlyModeException(message);
                default:
                    return new Exception("Unknown simulated error type.");
            }
        }
    }
}