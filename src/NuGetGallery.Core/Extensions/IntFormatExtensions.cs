using System.Text;

namespace NuGetGallery
{
    public static class IntFormatExtensions
    {
        public static string ToKiloFormat(this int number)
        {
            if (number >= 1_000_000_000)
            {
                return new StringBuilder((number / 1_000_000_000.0f).ToString("F3")) { [4] = 'G' }.ToString();
            }

            if (number >= 100_000_000)
            {
                return (number / 1_000_000) + "M";
            }

            if (number >= 1_000_000)
            {
                return new StringBuilder((number / 1_000_000.0f).ToString(number >= 10_000_000 ? "F2" : "F3")) { [4] = 'M' }.ToString();
            }

            if (number >= 100_000)
            {
                return (number / 1_000) + "K";
            }

            if (number >= 1000)
            {
                return new StringBuilder((number / 1_000.0f).ToString(number >= 10_000 ? "F2" : "F3")) { [4] = 'K' }.ToString();
            }

            return number.ToString("#,0");
        }
    }
}
