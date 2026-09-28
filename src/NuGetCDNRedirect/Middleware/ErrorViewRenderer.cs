// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace NuGet.Services.CDNRedirect.Middleware
{
    public interface IErrorViewRenderer
    {
        Task RenderAsync(HttpContext context, CancellationToken cancellationToken = default);
    }

    public class ErrorViewRenderer : IErrorViewRenderer
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public ErrorViewRenderer(
            ICompositeViewEngine viewEngine,
            ITempDataProvider tempDataProvider)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        public async Task RenderAsync(HttpContext context, CancellationToken cancellationToken = default)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/html; charset=utf-8";

            var viewResult = _viewEngine.GetView(
                executingFilePath: null,
                viewPath: "/Views/Shared/Error.cshtml",
                isMainPage: true);

            if (!viewResult.Success)
            {
                await context.Response.WriteAsync(
                    "An error occurred while processing your request.",
                    cancellationToken);
                return;
            }

            var actionContext = new ActionContext(
                context,
                new RouteData(),
                new ActionDescriptor());
            var viewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary());
            var tempData = new TempDataDictionary(context, _tempDataProvider);

            await using var writer = new StreamWriter(context.Response.Body);
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewData,
                tempData,
                writer,
                new HtmlHelperOptions());
            await viewResult.View.RenderAsync(viewContext);
        }
    }
}
