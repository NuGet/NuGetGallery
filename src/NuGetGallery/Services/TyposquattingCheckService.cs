using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace NuGetGallery
{
    public class TyposquattingCheckService : ITyposquattingCheckService
    {
        private static IPackageService PackageService { get; set; }

        private static readonly HashSet<char> SpecialCharacters = new HashSet<char>{'.', '_', '-'};
        private static readonly string SpecialCharactersToString = "[" + new string(SpecialCharacters.ToArray()) + "]";
        private const char PlaceholderForAlignment = '*';  // This const place holder variable is used for strings alignment 
        
        /// <summary>
        /// The following dictionary is built through picking up similar characters manually from wiki unicode page.
        /// https://en.wikipedia.org/wiki/List_of_Unicode_characters
        /// </summary>
        private static readonly IReadOnlyDictionary<char, string> SimilarCharacterDictionary = new Dictionary<char, string>()
        {
            {'a', "AÀÁÂÃÄÅàáâãäåĀāĂăĄąǍǎǞǟǠǡǺǻȀȁȂȃȦȧȺΆΑάαἀἁἂἃἄἅἆἇἈἉἊἋἌἍἎἏӐӑӒӓὰάᾀᾁᾂᾃᾄᾅᾆᾇᾈᾊᾋᾌᾍᾎᾏᾰᾱᾲᾳᾴᾶᾷᾸᾹᾺΆᾼАДад"},
            {'b', "BƀƁƂƃƄƅɃḂḃΒϦЂБВЪЬвъьѢѣҌҍႦႪხҔҕӃӄ" },
            {'c', "CÇçĆćĈĉĊċČčƇƈȻȼϲϹСсҪҫ𐒨"},
            {'d', "DÐĎďĐđƉƊƋƌǷḊḋԀԁԂԃ"},
            {'e', "EÈÉÊËèéêëĒēĔĕĖėĘęĚěȄȅȆȇȨȩɆɇΈΕЀЁЄѐёҼҽҾҿӖӗἘἙἚἛἜἝῈΈЕе"},
            {'f', "FƑƒḞḟϜϝҒғӺӻ"},
            {'g', "GĜĝĞğĠġĢģƓǤǥǦǧǴǵԌԍ"},
            {'h', "HĤĥħǶȞȟΉΗἨἩἪἫἬἭἮἯᾘᾙᾚᾛᾜᾝᾞᾟῊΉῌЋНнћҢңҤҥҺһӇӈӉӊԊԋԦԧԨԩհႬႹ𐒅𐒌𐒎𐒣"},
            {'i', "I¡ìíîïǐȉȋΐίιϊіїὶίῐῑῒΐῖῗΊΙΪȊȈἰἱἲἳἴἵἶἷἸἹἺἻἼἽἾἿῘῙῚΊІЇӀӏÌÍÎÏĨĩĪīĬĭĮįİǏ"},
            {'j', "JĴĵǰȷͿϳЈ"},
            {'k', "KĶķĸƘƙǨǩΚκϏЌКкќҚқҜҝҞҟҠҡԞԟ"},
            {'l', "LĹĺĻļĽľĿŀŁłſƖƪȴẛ"},
            {'m', "MṀṁΜϺϻМмӍӎ𐒄"},
            {'n', "NÑñŃńŅņŇňŉƝǸǹΝᾐᾑᾒᾓᾔᾕᾖᾗῂῃῄῆῇпԤԥԦԧԮԯ𐒐"},
            {'o', "OÒÓÔÕÖðòóôõöøŌōŎŏŐőƠơǑǒǪǫǬǭȌȍȎȏȪȫȬȭȮȯȰȱΌΟδοόϘϙὀὁὂὃὄὅὈὉὊὋὌὍὸόῸΌОоӦӧՕჿჾ𐒆𐒠0"},
            {'p', "PÞþƤƥƿṖṗΡρϷϸῤῥῬРрҎҏႲႼ"},
            {'q', "QȡɊɋԚԛգႭႳ"},
            {'r', "RŔŕŖŗŘřƦȐȑȒȓɌɼгѓ"},
            {'s', "SŚśŜŝŞşŠšȘșȿṠṡЅѕՏႽჽ𐒖𐒡"},
            {'t', "TŢţŤťŦŧƬƭƮȚțȾṪṫͲͳΤτТтҬҭէ"},
            {'u', "UÙÚÛÜùúûüŨũŪūŬŭŮůŰűŲųƯưǓǔǕǖǗǘǙǚǛǜȔȕȖȗμυϋύὐὑὒὓὔὕὖὗὺύῠῡῢΰῦῧՍႮ𐒩"},
            {'v', "VƔƲνѴѵѶѷ"},
            {'w', "WŴŵƜẀẁẂẃẄẅωώШЩшщѡѿὠὡὢὣὤὥὦὧὼώᾠᾡᾢᾣᾤᾥᾦᾧῲῳῴῶῷԜԝ"},
            {'x', "X×ΧχХхҲҳӼӽӾӿჯ"},
            {'y', "YÝýÿŶŷŸƳƴȲȳɎɏỲỳΎΥΫγϒϓϔЎУЧуўҮүҶҷҸҹӋӌӮӯӰӱӲӳӴӵὙὛὝὟῨῩῪΎႯႸ𐒋𐒦"},
            {'z', "ZŹźŻżŽžƵƶȤȥΖჍ"},
            {'3', "ƷǮǯȜȝʒЗзэӞӟӠӡჳ"},
            {'8', "Ȣȣ"},
            {'_', ".-" }
        };
        private static readonly IReadOnlyDictionary<char, char> NormalizedMappingDictionary = GetNormalizedMappingDictionary(SimilarCharacterDictionary);

        // TODO: Threshold parameters will be saved in the configuration file.
        // https://github.com/NuGet/Engineering/issues/1645
        private static List<ThresholdInfo> _thresholdsList = new List<ThresholdInfo>();

        // TODO: popular packages checklist will be implemented
        // https://github.com/NuGet/Engineering/issues/1624
        private static List<PackageInfo> _packagesCheckList = new List<PackageInfo>();

        private class BasicEditDistanceInfo
        {
            public int Distance { get; set; }
            public PathInfo[,] Path { get; set; }
        }  
        
        private enum PathInfo
        {
            [Description("Match")]
            MATCH,
            [Description("Delete")]
            DELETE,
            [Description("Substitute")]
            SUBSTITUTE,
            [Description("Insert")]
            INSERT,
        }

        public TyposquattingCheckService()
        {            
        }

        public TyposquattingCheckService(List<PackageInfo> packagesCheckList, List<ThresholdInfo> thresholdsList, IPackageService packageService) : this()
        {
            PackageService = packageService;
            SetPackageIdCheckList(packagesCheckList);
            SetThresholdsList(thresholdsList);
        }

        public static void SetPackageIdCheckList(List<PackageInfo> packagesCheckList)
        {
            _packagesCheckList = packagesCheckList;
            return;
        }      

        public static void SetThresholdsList(List<ThresholdInfo> thresholdsList)
        {
            _thresholdsList = thresholdsList;
            return;
        }
        
        private static Dictionary<char, char> GetNormalizedMappingDictionary(IReadOnlyDictionary<char, string> similarCharacterDictionary)
        {
            Dictionary<char, char> normalizedMappingDictionary = new Dictionary<char, char>();
            foreach (var item in similarCharacterDictionary)
            {
                foreach(char c in item.Value)
                {
                    normalizedMappingDictionary[c] = item.Key;
                }
            }

            return normalizedMappingDictionary;
        }

        private static int GetThreshold(string packageId)
        {
            foreach (var thresholdInfo in _thresholdsList)
            {
                if (packageId.Length >= thresholdInfo.LowerBound && packageId.Length < thresholdInfo.UpperBound)
                {
                    return thresholdInfo.Threshold;
                }
            }

            throw new ArgumentException("There is no predefined typo-squatting threshold for this package Id!");
        }

        private static string NormalizeString(string str)
        {
            StringBuilder normalizedStr = new StringBuilder(str);
            for (int i = 0; i < normalizedStr.Length; i++)
            {
                if (NormalizedMappingDictionary.ContainsKey(normalizedStr[i]))
                {
                    normalizedStr[i] = NormalizedMappingDictionary[normalizedStr[i]];
                }
            }
            
            return normalizedStr.ToString();
        }

        public bool IsUploadedPackageIdTyposquatting(string uploadedPackageId, User uploadedPackageOwner)
        {
            if (uploadedPackageId == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageId));
            }

            int threshold = GetThreshold(uploadedPackageId);
            uploadedPackageId = NormalizeString(uploadedPackageId);

            int countCollision = 0;
            Parallel.ForEach(_packagesCheckList, (package, loopState) =>
            {
                if (package.Owners.Contains(uploadedPackageOwner.Username))
                {
                    return;
                }
                else
                {
                    if (IsDistanceLessThanThreshold(uploadedPackageId, package.Id, threshold))
                    {
                        var owners = PackageService.FindPackageRegistrationById(package.Id).Owners;
                        foreach (var owner in owners)
                        {
                            if (owner.Username == uploadedPackageOwner.Username)
                            {
                                return;
                            }
                        }

                        Interlocked.Increment(ref countCollision);
                        loopState.Stop();
                    }
                }                
            });

            return countCollision != 0;
        }

        private static bool IsDistanceLessThanThreshold(string str1, string str2, int threshold)
        {
            if (str1 == null)
            {
                throw new ArgumentNullException(nameof(str1));
            }
            if (str2 == null)
            {
                throw new ArgumentNullException(nameof(str2));
            }

            string newStr1 = Regex.Replace(str1, SpecialCharactersToString, string.Empty);
            string newStr2 = Regex.Replace(str2, SpecialCharactersToString, string.Empty);
            if (Math.Abs(newStr1.Length - newStr2.Length) > threshold)
            {
                return false;
            }

            return GetDistance(str1, str2, threshold) <= threshold;
        }

        private static int GetDistance(string str1, string str2, int threshold)
        {
            var basicEditDistanceInfo = GetBasicEditDistanceWithPath(str1, str2);
            if (basicEditDistanceInfo.Distance <= threshold)
            {
                return basicEditDistanceInfo.Distance;  
            }
            var alignedStrings = TraceBackAndAlignStrings(basicEditDistanceInfo.Path, str1, str2);
            int refreshedEditDistance = RefreshEditDistance(alignedStrings[0], alignedStrings[1], basicEditDistanceInfo.Distance);

            return refreshedEditDistance;
        }

        /// <summary>
        /// The following function is used to calculate the classical edit distance and construct the path in dynamic programming way.
        /// </summary>
        private static BasicEditDistanceInfo GetBasicEditDistanceWithPath(string str1, string str2)
        {
            var distances = new int[str1.Length + 1, str2.Length + 1];
            var path = new PathInfo[str1.Length + 1, str2.Length + 1];
            distances[0, 0] = 0;
            path[0, 0] = PathInfo.MATCH;
            for (int i = 1; i <= str1.Length; i++)
            {
                distances[i, 0] = i;
                path[i, 0] = PathInfo.DELETE;
            }

            for (int j = 1; j <= str2.Length; j++)
            {
                distances[0, j] = j;
                path[0, j] = PathInfo.INSERT;
            }

            for (int i = 1; i <= str1.Length; i++)
            {
                for (int j = 1; j <= str2.Length; j++)
                {
                    if (str1[i - 1] == str2[j - 1])
                    {
                        distances[i, j] = distances[i - 1, j - 1];
                        path[i, j] = PathInfo.MATCH;
                    }
                    else
                    {
                        distances[i, j] = distances[i - 1, j - 1] + 1;
                        path[i, j] = PathInfo.SUBSTITUTE;

                        if (distances[i - 1, j] + 1 < distances[i, j])
                        {
                            distances[i, j] = distances[i - 1, j] + 1;
                            path[i, j] = PathInfo.DELETE;
                        }

                        if (distances[i, j - 1] + 1 < distances[i, j])
                        {
                            distances[i, j] = distances[i, j - 1] + 1;
                            path[i, j] = PathInfo.INSERT;
                        }
                    }
                }
            }

            return new BasicEditDistanceInfo
            {
                Distance = distances[str1.Length, str2.Length],
                Path = path
            };
        }

        /// <summary>
        /// The following function is used to traceback based on the construction path and align two strings.
        /// Example:  For two strings: "asp.net" "aspnet". After traceback and alignment, we will have aligned strings as "asp.net" "asp*net" ('*' is the placeholder).
        /// </summary>
        private static string[] TraceBackAndAlignStrings(PathInfo[,] path, string str1, string str2)
        {
            StringBuilder newStr1 = new StringBuilder(str1);
            StringBuilder newStr2 = new StringBuilder(str2);
            string[] alignedStrs = new string[2];

            int i = str1.Length;
            int j = str2.Length;
            while (i > 0 && j > 0)
            {
                switch (path[i, j])
                {
                    case PathInfo.MATCH:
                        i--;
                        j--;
                        break;
                    case PathInfo.SUBSTITUTE:
                        i--;
                        j--;
                        break;
                    case PathInfo.DELETE:
                        newStr2.Insert(j, PlaceholderForAlignment);
                        i--;
                        break;
                    case PathInfo.INSERT:
                        newStr1.Insert(i, PlaceholderForAlignment);
                        j--;
                        break;
                    default:
                        throw new ArgumentException("Invalidate operation for edit distance trace back: " + path[i, j]);
                }
            }

            for (int k = 0; k < i; k++)
            {
                newStr2.Insert(k, PlaceholderForAlignment);
            }

            for (int k = 0; k < j; k++)
            {
                newStr1.Insert(k, PlaceholderForAlignment);
            }

            alignedStrs[0] = newStr1.ToString();
            alignedStrs[1] = newStr2.ToString();

            return alignedStrs;
        }

        /// <summary>
        /// The following function is used to refresh the edit distance based on predefined rules. (Insert/Delete special characters will not account for distance)
        /// Example:  For two aligned strings: "asp.net" "asp*net" ('*' is the placeholder), we will scan the two strings again and the mapping from '.' to '*' will not account for the distance.
        ///           So the final distance will be 0 for these two strings "asp.net" "aspnet".
        /// </summary>
        private static int RefreshEditDistance(string alignedStr1, string alignedStr2, int basicEditDistance)
        {
            if (alignedStr1.Length != alignedStr2.Length)
            {
                throw new ArgumentException("The lengths of two aligned strings are not same!");
            }

            int sameSubstitution = 0;
            for (int i = 0; i < alignedStr2.Length; i++)
            {
                if (alignedStr1[i] != alignedStr2[i])
                {
                    if (alignedStr1[i] == PlaceholderForAlignment && SpecialCharacters.Contains(alignedStr2[i]))
                    {
                        sameSubstitution += 1;
                    }
                    else if (SpecialCharacters.Contains(alignedStr1[i]) && alignedStr2[i] == PlaceholderForAlignment)
                    {
                        sameSubstitution += 1;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            return basicEditDistance - sameSubstitution;
        }             
    }

    public class PackageInfo
    {
        public string Id { get; set; }
        public List<string> Owners { get; set; }
    }

    public class ThresholdInfo
    {
        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
        public int Threshold { get; set; }
    }
}