using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.WebPages;

namespace NuGetGallery
{
    public static class IEnumerableExtensions
    {
        public static string Flatten(this IEnumerable<string> list)
        {
            if (list == null)
            {
                return String.Empty;
            }

            return String.Join(", ", list.ToArray());
        }

        public static HelperResult Flatten<T>(this IEnumerable<T> items, Func<T, HelperResult> template)
        {
            if (items == null)
            {
                return null;
            }
            var formattedItems = items.Select(item => template(item).ToHtmlString());

            return new HelperResult(writer => { writer.Write(String.Join(", ", formattedItems.ToArray())); });
        }

        public static bool AnySafe<T>(this IEnumerable<T> items)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any();
        }

        public static bool AnySafe<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any(predicate);
        }
    }
}
