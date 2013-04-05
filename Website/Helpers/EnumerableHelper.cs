using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public static class EnumerableHelper
    {
        public static ISet<T> ToSet<T>(this IEnumerable<T> items)
        {
            return items as ISet<T> ?? new HashSet<T>(items);
        }

        public static T SingleOrThrow<T, TException>(this IEnumerable<T> enumerable, Func<TException> exceptionFactory)
            where TException : Exception
        {
            IEnumerator<T> enumerator = enumerable.GetEnumerator();
            if (enumerator.MoveNext())
            {
                T ret = enumerator.Current;
                if (enumerator.MoveNext())
                {
                    throw new InvalidOperationException("Enumeration contains multiple elements");
                }

                return ret;
            }

            TException exception = exceptionFactory.Invoke();
            throw exception;
        }
    }
}