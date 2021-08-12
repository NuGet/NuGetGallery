// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text;

namespace NuGetGallery
{
    public static class TyposquattingStringNormalization
    {
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
            {'k', "KĶķĸƘƙǨǩΚκϏЌКкќҚқҜҝҞҟҠҡԞԟK"},
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

        public static string NormalizeString(string str)
        {
            var normalizedStr = new StringBuilder(str);
            for (var i = 0; i < normalizedStr.Length; i++)
            {
                if (NormalizedMappingDictionary.TryGetValue(normalizedStr[i], out var normalizedCharacter))
                {
                    normalizedStr[i] = normalizedCharacter;
                }
            }

            return normalizedStr.ToString();
        }

        private static Dictionary<char, char> GetNormalizedMappingDictionary(IReadOnlyDictionary<char, string> similarCharacterDictionary)
        {
            var normalizedMappingDictionary = new Dictionary<char, char>();
            foreach (var item in similarCharacterDictionary)
            {
                foreach (var c in item.Value)
                {
                    normalizedMappingDictionary[c] = item.Key;
                }
            }

            return normalizedMappingDictionary;
        }
    }
}