using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class TyposquattingCheckService
    {
        private static readonly char[] SpecialCharacters = {'.', '_', '-'}; 
        private static readonly Dictionary<char, string> SimilarCharacterDictionary = new Dictionary<char, string>()
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
        private static readonly Dictionary<char, char> NormalizedMappingDictionary = GetNormalizedMappingDictionary(SimilarCharacterDictionary);

        // TODO: Threshold parameters will be saved in the configuration file. 
        private const int TyposquattingThreshold1 = 0;
        private const int TyposquattingThreshold2 = 1;
        private const int TyposquattingThreshold3 = 2;
        private const int TyposquattingThresholdInterval1 = 30;
        private const int TyposquattingThresholdInterval2 = 50;

        // TODO: popular packages checklist will be implemented
        private List<string> _packageIdCheckList = new List<string>();

        private class BasicEditDistanceInfo
        {
            public int Distance { get; set; }
            public char[,] Path { get; set; }
        }

        public TyposquattingCheckService()
        {
        }

        public TyposquattingCheckService(List<string> packageIdCheckList) : this()
        {
            SetPackageIdCheckList(packageIdCheckList);
        }

        public void SetPackageIdCheckList(List<string> packageIdCheckList)
        {
            _packageIdCheckList = packageIdCheckList;
            return;
        }      
        
        private static Dictionary<char, char> GetNormalizedMappingDictionary(Dictionary<char, string> similarCharacterDictionary)
        {
            Dictionary<char, char> normalizedMappingDictionary = new Dictionary<char, char>();
            foreach(var item in similarCharacterDictionary)
            {
                foreach(char c in item.Value)
                {
                    normalizedMappingDictionary[c] = item.Key;
                }
            }

            return normalizedMappingDictionary;
        }

        private int GetThreshold(string packageId)
        {
            if (packageId.Length < TyposquattingThresholdInterval1)
            {
                return TyposquattingThreshold1;
            }
            else if (packageId.Length >= TyposquattingThresholdInterval1 && packageId.Length < TyposquattingThresholdInterval2)
            {
                return TyposquattingThreshold2;
            }
            else
            {
                return TyposquattingThreshold3;
            }   
        }

        private string NormalizeString(string str)
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

        public bool IsUploadedPackageIdTyposquatting(string uploadedPackageId)
        {
            if (uploadedPackageId == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageId));
            }

            int threshold = GetThreshold(uploadedPackageId);
            uploadedPackageId = NormalizeString(uploadedPackageId);

            int countCollision = 0;
            Parallel.ForEach(_packageIdCheckList, (packageId, loopState) =>
            {
                if (IsDistanceLessThanThreshold(uploadedPackageId, packageId, threshold))
                {
                    Interlocked.Increment(ref countCollision);
                    loopState.Stop();
                }
            });

            return countCollision != 0;
        }

        private bool IsDistanceLessThanThreshold(string str1, string str2, int threshold)
        {
            if (str1 == null)
            {
                throw new ArgumentNullException(nameof(str1));
            }
            if (str2 == null)
            {
                throw new ArgumentNullException(nameof(str2));
            }

            string newStr1 = Regex.Replace(str1, "[" + new string(SpecialCharacters) + "]", ""); 
            string newStr2 = Regex.Replace(str2, "[" + new string(SpecialCharacters) + "]", "");
            if (Math.Abs(newStr1.Length - newStr2.Length) > threshold)
            {
                return false;
            }

            return GetDistance(str1, str2, threshold) <= threshold;
        }

        private int GetDistance(string str1, string str2, int threshold)
        {
            BasicEditDistanceInfo basicEditDistanceInfo = GetBasicEditDistanceWithPath(str1, str2);
            if (basicEditDistanceInfo.Distance <= threshold)
            {
                return basicEditDistanceInfo.Distance;  
            }
            var alignedStrings = TraceBackAndAlignStrings(basicEditDistanceInfo.Path, str1, str2);
            int refreshedEditDistance = RefreshEditDistance(alignedStrings[0], alignedStrings[1], basicEditDistanceInfo.Distance);

            return refreshedEditDistance;
        }

        private BasicEditDistanceInfo GetBasicEditDistanceWithPath(string str1, string str2)
        {
            var distances = new int[str1.Length + 1, str2.Length + 1];
            var path = new char[str1.Length + 1, str2.Length + 1];
            distances[0, 0] = 0;
            path[0, 0] = 'M';
            for (int i = 1; i <= str1.Length; i++)
            {
                distances[i, 0] = i;
                path[i, 0] = 'D';
            }

            for (int j = 1; j <= str2.Length; j++)
            {
                distances[0, j] = j;
                path[0, j] = 'I';
            }

            for (int i = 1; i <= str1.Length; i++)
            {
                for (int j = 1; j <= str2.Length; j++)
                {
                    if (str1[i - 1] == str2[j - 1])
                    {
                        distances[i, j] = distances[i - 1, j - 1];
                        path[i, j] = 'M';
                    }
                    else
                    {
                        distances[i, j] = distances[i - 1, j - 1] + 1;
                        path[i, j] = 'S';

                        if (distances[i - 1, j] + 1 < distances[i, j])
                        {
                            distances[i, j] = distances[i - 1, j] + 1;
                            path[i, j] = 'D';
                        }

                        if (distances[i, j - 1] + 1 < distances[i, j])
                        {
                            distances[i, j] = distances[i, j - 1] + 1;
                            path[i, j] = 'I';
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

        private string[] TraceBackAndAlignStrings(char[,] path, string str1, string str2)
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
                    case 'M':
                        i--;
                        j--;
                        break;
                    case 'S':
                        i--;
                        j--;
                        break;
                    case 'D':
                        newStr2.Insert(j, '*');
                        i--;
                        break;
                    case 'I':
                        newStr1.Insert(i, '*');
                        j--;
                        break;
                    default:
                        throw new ArgumentException("Invalidate operation for edit distance trace back: " + path[i, j]);
                }
            }

            for (int k = 0; k < i; k++)
            {
                newStr2.Insert(k, '*');
            }

            for (int k = 0; k < j; k++)
            {
                newStr1.Insert(k, '*');
            }

            alignedStrs[0] = newStr1.ToString();
            alignedStrs[1] = newStr2.ToString();

            return alignedStrs;
        }

        private int RefreshEditDistance(string alignedStr1, string alginedStr2, int basicEditDistance)
        {
            if (alignedStr1.Length != alginedStr2.Length)
            {
                throw new ArgumentException("The lengths of two aligned strings are not same!");
            }

            int sameSubstitution = 0;
            for (int i = 0; i < alginedStr2.Length; i++)
            {
                if (alignedStr1[i] != alginedStr2[i])
                {
                    if (alignedStr1[i] == '*' && SpecialCharacters.Contains(alginedStr2[i]))
                    {
                        sameSubstitution += 1;
                    }
                    else if (SpecialCharacters.Contains(alignedStr1[i]) && alginedStr2[i] == '*')
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
}