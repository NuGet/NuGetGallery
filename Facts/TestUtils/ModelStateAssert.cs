using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Xunit;

namespace NuGetGallery
{
    public static class ModelStateAssert
    {
        public static void HasErrors(ModelStateDictionary dict, string key, params string[] errors)
        {
            HasErrors(dict, key, errors.Select(s => new ModelError(s)).ToArray());
        }

        public static void HasErrors(ModelStateDictionary dict, string key, params ModelError[] errors)
        {
            Assert.True(dict.ContainsKey(key));
            var state = dict[key];
            Assert.Equal(
                // Tuples have an Equals override. ModelError doesn't :(
                errors.Select(e => Tuple.Create(e.Exception, e.ErrorMessage)).ToArray(),
                state.Errors.Select(e => Tuple.Create(e.Exception, e.ErrorMessage)).ToArray());
        }
    }
}
