// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Newtonsoft.Json;

namespace NuGetGallery.Helpers
{
    public static class HtmlExtensions
    {
        public static MvcHtmlString EnumDropDownListFor<TModel, TEnum>(this HtmlHelper<TModel> self, Expression<Func<TModel, TEnum?>> expression, IEnumerable<TEnum> values, string emptyItemText)
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

            return self.DropDownListFor(expression, items);
        }

        /// <summary>
        /// RegEx Source: http://stackoverflow.com/a/1112171
        /// Sample: https://regex101.com/r/kI9xT9/2
        /// </summary>
        private static Regex regExHttpLinks = new Regex(@"(?<=\()\b(https?://|www\.)[-A-Za-z0-9+&@#/%?=~_()|!:,.;]*[-A-Za-z0-9+&@#/%=~_()|](?=\))|(?<=(?<wrap>[=~|_#]))\b(https?://|www\.)[-A-Za-z0-9+&@#/%?=~_()|!:,.;]*[-A-Za-z0-9+&@#/%=~_()|](?=\k<wrap>)|\b(https?://|www\.)[-A-Za-z0-9+&@#/%?=~_()|!:,.;]*[-A-Za-z0-9+&@#/%=~_()|]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static IHtmlString PreFormattedTextWithClickableLinks(this HtmlHelper self, string html, bool newLineAsNewParagraph = false)
        {
            if (string.IsNullOrEmpty(html))
            {
                return new HtmlString(string.Empty);
            }

            html = self.Encode(html);

            if (newLineAsNewParagraph)
            {
                var splittedParagraphs = html.Split('\n');

                html = string.Empty;
                foreach (var splittedParagraph in splittedParagraphs)
                {
                    html += "<p>" + splittedParagraph + "</p>";
                }
            }
            else
            {
                html = html.Replace("\n", "<br />");
                html = html.Replace("  ", "&nbsp; ");
            }

            // replace periods on numeric values that appear to be valid domain names
            var periodReplacement = "[[[replace:period]]]";
            html = Regex.Replace(html, @"(?<=\d)\.(?=\d)", periodReplacement);

            // create links for matches
            var linkMatches = regExHttpLinks.Matches(html);
            for (int i = 0; i < linkMatches.Count; i++)
            {
                var temp = linkMatches[i].ToString();

                if (!temp.Contains("://"))
                {
                    temp = "http://" + temp;
                }

                html = html.Replace(linkMatches[i].ToString(), String.Format("<a href=\"{0}\" title=\"{0}\">{1}</a>", temp.Replace(".", periodReplacement).ToLower(), linkMatches[i].ToString().Replace(".", periodReplacement)));
            }

            // Clear out period replacement
            html = html.Replace(periodReplacement, ".");

            return self.Raw(html);
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