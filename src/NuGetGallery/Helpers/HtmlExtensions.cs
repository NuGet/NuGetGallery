// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NuGetGallery.Configuration;

namespace NuGetGallery.Helpers
{
    public static class HtmlExtensions
    {
        private const string _htmlQuote = "&quot;";
        private const string _htmlSingleQuote = "&#39;";
        private const string _semicolon = ";";
        private const string _hyphen = "-";
        private static readonly string[] _trimmedHtmlEntities = new [] { _htmlQuote, _htmlSingleQuote, _semicolon, _hyphen };

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

        public static IHtmlString BreakWord(this HtmlHelper self, string text)
        {
            return self.Raw(HttpUtility
                .HtmlEncode(text)
                .Replace("-", "-<wbr>")
                .Replace(".", ".<wbr>"));
        }

        public static IHtmlString PreFormattedText(this HtmlHelper self, string text, IGalleryConfigurationService configurationService)
        {
            // Encode HTML entities. Important! Security!
            var encodedText = HttpUtility.HtmlEncode(text);
            // Turn HTTP and HTTPS URLs into links.
            // Source: https://stackoverflow.com/a/4750468
            string anchorEvaluator(Match match)
            {
                string trimmedEntityValue = string.Empty;
                string trimmedAnchorValue = match.Value;

                foreach (var trimmedEntity in _trimmedHtmlEntities)
                {
                    if (match.Value.EndsWith(trimmedEntity))
                    {
                        // Remove trailing html entity from anchor URL
                        trimmedAnchorValue = match.Value.Substring(0, match.Value.Length - trimmedEntity.Length);
                        trimmedEntityValue = trimmedEntity;

                        break;
                    }
                }

                if (PackageHelper.TryPrepareUrlForRendering(trimmedAnchorValue, out string formattedUri))
                {
                    string anchorText = formattedUri;
                    string siteRoot = configurationService.GetSiteRoot(useHttps: true);
                    // Format links to NuGet packages
                    Match packageMatch = RegexEx.MatchWithTimeout(formattedUri, $@"({Regex.Escape(siteRoot)}\/packages\/(?<name>\w+([_.-]\w+)*(\/[0-9a-zA-Z-.]+)?)\/?$)", RegexOptions.IgnoreCase);
                    if (packageMatch != null && packageMatch.Groups["name"].Success)
                    {
                        anchorText = packageMatch.Groups["name"].Value;
                    }

                    return $"<a href=\"{formattedUri}\" rel=\"nofollow\">{anchorText}</a>" + trimmedEntityValue;
                }

                return match.Value;
            }

            encodedText = RegexEx.TryReplaceWithTimeout(
                encodedText,
                @"((http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)",
                anchorEvaluator,
                RegexOptions.IgnoreCase);

            // Replace new lines with the <br /> tag.
            encodedText = encodedText.Replace("\n", "<br />");

            // Replace more than one space in a row with a space then &nbsp;.
            encodedText = RegexEx.TryReplaceWithTimeout(
                encodedText,
                "  +",
                match => " " + string.Join(string.Empty, Enumerable.Repeat("&nbsp;", match.Value.Length - 1)),
                RegexOptions.None);

            return self.Raw(encodedText);
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