using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.GitHub
{
    public class KiloFormatExtension
    {
        public static string ToKiloFormat(int number)
        {
            if (number >= 1_000_000_000)
                return new System.Text.StringBuilder((number / 1_000_000_000.0f).ToString("F3")) { [4] = 'G' }.ToString();
            if (number >= 100_000_000)
                return (number / 1000) + "M";
            if (number >= 10_000_000)
                return new System.Text.StringBuilder((number / 1000000.0f).ToString("F2")) { [4] = 'M' }.ToString();
            if (number >= 1_000_000)
                return new System.Text.StringBuilder((number / 1000000.0f).ToString("F3")) { [4] = 'M' }.ToString();
            if (number >= 100_000)
                return (number / 1000) + "K";
            if (number >= 10_000)
                return new System.Text.StringBuilder((number / 1000.0f).ToString("F2")) { [4] = 'K' }.ToString();
            if (number >= 1000)
                return new System.Text.StringBuilder((number / 1000.0f).ToString("F3")) { [4] = 'K' }.ToString();

            return number.ToString("#,0");
        }
    }
}
