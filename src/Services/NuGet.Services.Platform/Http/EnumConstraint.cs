using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;

namespace NuGet.Services.Http
{
    public class EnumConstraint<TEnum> : IHttpRouteConstraint
        where TEnum : struct
    {
        private HashSet<string> _items = new HashSet<string>(
            Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => e.ToString()), 
            StringComparer.OrdinalIgnoreCase);

        public bool Match(HttpRequestMessage request, IHttpRoute route, string parameterName, IDictionary<string, object> values, HttpRouteDirection routeDirection)
        {
            object value;
            if (values.TryGetValue(parameterName, out value))
            {
                string valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
                return _items.Contains(valueString);
            }
            return false;
        }
    }
}
