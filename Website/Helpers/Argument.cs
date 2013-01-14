using System;

namespace NuGetGallery
{
    internal static class Argument
    {
        // IsSet == not null. Maybe someday there will be a better word.
        // http://stackoverflow.com/questions/14273820/is-there-a-short-word-for-the-opposite-of-null
        internal static void IsSet(object obj, string name)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}