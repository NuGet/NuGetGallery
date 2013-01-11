using System;

namespace NuGetGallery
{
    internal static class Argument
    {
        // Isset == not null
        // http://stackoverflow.com/questions/14273820/is-there-a-short-word-for-the-opposite-of-null
        // and taking some poetic liberty of interpretation IsSet (which is a little vague in meaning) 
        // but treating it as a new word 'Isset' (meaning specifically 'not null').
        internal static void Isset(object obj, string name)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}