using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;

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

        
    }
}