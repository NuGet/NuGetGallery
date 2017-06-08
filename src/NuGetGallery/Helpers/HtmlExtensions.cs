﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.WebPages;
using Newtonsoft.Json;

namespace NuGetGallery.Helpers
{
    public static class HtmlExtensions
    {
        public static MvcHtmlString EnumDropDownListFor<TModel, TEnum>(this HtmlHelper<TModel> self, Expression<Func<TModel, TEnum?>> expression, IEnumerable<TEnum> values, string emptyItemText)
            where TEnum : struct // Can't do ": enum" but this is close
        {
            return EnumDropDownListFor(self, expression, values, emptyItemText, null);
        }

        public static MvcHtmlString EnumDropDownListFor<TModel, TEnum>(this HtmlHelper<TModel> self, Expression<Func<TModel, TEnum?>> expression, IEnumerable<TEnum> values, string emptyItemText, Dictionary<string, object> htmlAttributes)
            where TEnum: struct // Can't do ": enum" but this is close
        {
            Debug.Assert(typeof(TEnum).IsEnum, "Expected an Enum Type!");

            ModelMetadata metadata = ModelMetadata.FromLambdaExpression(expression, self.ViewData);

            IEnumerable<SelectListItem> items = new[] {
                new SelectListItem() { Text = emptyItemText, Value = "" },
            }.Concat(
                values.Select(value => new SelectListItem()
                {
                    Text = EnumHelper.GetDescription(value),
                    Value = value.ToString(),
                    Selected = value.Equals(metadata.Model)
                }));

            return self.DropDownListFor(expression, items, htmlAttributes);
        }

        public static IHtmlString PreFormattedText(this HtmlHelper self, string text)
        {
            return self.Raw(HttpUtility.HtmlEncode(text).Replace("\n", "<br />").Replace("  ", "&nbsp; "));
        }

        public static IHtmlString ValidationSummaryFor(this HtmlHelper html, string key)
        {
            var toRemove = html.ViewData.ModelState.Keys
                .Where(k => !string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var copy = new Dictionary<string, ModelState>();
            foreach (var keyValuePair in html.ViewData.ModelState)
            {
                copy.Add(keyValuePair.Key, keyValuePair.Value);
            }

            foreach (var k in toRemove)
            {
                html.ViewData.ModelState.Remove(k);
            }

            var str = html.ValidationSummary();

            // Restore the old model state
            foreach (var k in toRemove)
            {
                html.ViewData.ModelState[k] = copy[k];
            }

            return str;
        }

        public static IHtmlString ToJson(this HtmlHelper html, object item)
        {
            if (item == null)
            {
                return html.Raw("{}");
            }

            var serializerSettings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                Formatting = Formatting.None
            };

            var json = JsonConvert.SerializeObject(item, serializerSettings);
            return html.Raw(json);
        }
    }
}