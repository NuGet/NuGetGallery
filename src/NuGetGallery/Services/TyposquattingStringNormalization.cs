// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace NuGetGallery
{
    public static class TyposquattingStringNormalization
    {
        /// <summary>
        /// The following dictionary is built through picking up similar characters manually from wiki unicode page.
        /// https://en.wikipedia.org/wiki/List_of_Unicode_characters
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> SimilarCharacterDictionary = new Dictionary<string, string>()
        {
            { "a", "AΑАαаÀÁÂÃÄÅàáâãäåĀāĂăĄąǍǎǞǟǠǡǺǻȀȁȂȃȦȧȺΆάἀἁἂἃἄἅἆἇἈἉἊἋἌΆἍἎἏӐӑӒӓὰάᾀᾁᾂᾃᾄᾅᾆᾇᾈᾊᾋᾌᾍᾎᾏᾰᾱᾲᾳᾴᾶᾷᾸᾹᾺᾼДд"},
            { "b", "BΒВЪЬƀƁƂƃƄƅɃḂḃϦЂБвъьѢѣҌҍႦႪხҔҕӃӄ"},
            { "c", "CСсϹϲÇçĆćĈĉĊċČčƇƈȻȼҪҫ𐒨"},
            { "d", "DƊԁÐĎďĐđƉƋƌǷḊḋԀԂԃ"},
            { "e", "EΕЕеÈÉÊËèéêëĒēĔĕĖėĘęĚěȄȅȆȇȨȩɆɇΈЀЁЄѐёҼҽҾҿӖӗἘἙἚἛἜἝῈΈ"},
            { "f", "FϜƑƒḞḟϝҒғӺӻ"},
            { "g", "GǤԌĜĝĞğĠġĢģƓǥǦǧǴǵԍ"},
            { "h", "HΗНһհҺĤĥħǶȞȟΉἨἩἪἫἬἭἮἯᾘᾙᾚᾛᾜᾝᾞᾟῊΉῌЋнћҢңҤҥӇӈӉӊԊԋԦԧԨԩႬႹ𐒅𐒌𐒎𐒣"},
            { "i", "IΙІӀ¡ìíîïǐȉȋΐίιϊіїὶίῐῑῒΐῖῗΊΪȊȈἰἱἲἳἴἵἶἷἸἹἺἻἼἽἾἿῘῙῚΊЇӏÌÍÎÏĨĩĪīĬĭĮįİǏ"},
            { "j", "JЈͿϳĴĵǰȷ"},
            { "k", "KΚКKĶķĸƘƙǨǩκϏЌкќҚқҜҝҞҟҠҡԞԟ"},
            { "l", "LĹĺĻļĽľĿŀŁłſƖƪȴẛ"},
            { "m", "MΜМṀṁϺϻмӍӎ𐒄"},
            { "n", "NΝпÑñŃńŅņŇňŉƝǸǹᾐᾑᾒᾓᾔᾕᾖᾗῂῃῄῆῇԤԥԮԯ𐒐"},
            { "o", "OΟОՕჿоοÒÓÔÕÖðòóôõöøŌōŎŏŐőƠơǑǒǪǫǬǭȌȍȎȏȪȫȬȭȮȯȰȱΌδόϘϙὀὁὂὃὄὅὈὉὊὋὌὍὸόῸΌӦӧჾ𐒆𐒠0"},
            { "p", "PΡРрρÞþƤƥƿṖṗϷϸῤῥῬҎҏႲႼ"},
            { "q", "QգԛȡɊɋԚႭႳ"},
            { "r", "RгŔŕŖŗŘřƦȐȑȒȓɌɼѓ"},
            { "s", "SЅѕՏႽჽŚśŜŝŞşŠšȘșȿṠṡ𐒖𐒡"},
            { "t", "TΤТͲͳŢţŤťŦŧƬƭƮȚțȾṪṫτтҬҭէ"},
            { "u", "UՍႮÙÚÛÜùúûüŨũŪūŬŭŮůŰűŲųƯưǓǔǕǖǗǘǙǚǛǜȔȕȖȗμυϋύὐὑὒὓὔὕὖὗὺύῠῡῢΰῦῧ𐒩"},
            { "v", "VνѴѵƔƲѶѷ"},
            { "w", "WωшԜԝŴŵƜẀẁẂẃẄẅώШЩщѡѿὠὡὢὣὤὥὦὧὼώᾠᾡᾢᾣᾤᾥᾦᾧῲῳῴῶῷ"},
            { "x", "XХΧх×χҲҳӼӽӾӿჯ"},
            { "y", "YΥҮƳуУÝýÿŶŷŸƴȲȳɎɏỲỳΎΫγϒϓϔЎЧўүҶҷҸҹӋӌӮӯӰӱӲӳӴӵὙὛὝὟῨῩῪΎႯႸ𐒋𐒦"},
            { "z", "ZΖჍŹźŻżŽžƵƶȤȥ"},
            { "3", "ƷЗʒӡჳǮǯȜȝзэӞӟӠ"},
            { "8", "Ȣȣ"},
            { "_", ".-" }
        };

        private static readonly IReadOnlyDictionary<string, string> NormalizedMappingDictionary = GetNormalizedMappingDictionary(SimilarCharacterDictionary);

        public static string NormalizeString(string str)
        {
            var normalizedString = new StringBuilder();
            var textElementEnumerator = StringInfo.GetTextElementEnumerator(str);
            while (textElementEnumerator.MoveNext())
            {
                var textElement = textElementEnumerator.GetTextElement();
                if (NormalizedMappingDictionary.TryGetValue(textElement, out var normalizedTextElement))
                {
                    normalizedString.Append(normalizedTextElement);
                }
                else
                {
                    normalizedString.Append(textElement);
                }
            }

            return normalizedString.ToString();
        }

        private static Dictionary<string, string> GetNormalizedMappingDictionary(IReadOnlyDictionary<string, string> similarCharacterDictionary)
        {
            var normalizedMappingDictionary = new Dictionary<string, string>();
            foreach (var item in similarCharacterDictionary)
            {
                var textElementEnumerator = StringInfo.GetTextElementEnumerator(item.Value);
                while (textElementEnumerator.MoveNext())
                {
                    normalizedMappingDictionary[textElementEnumerator.GetTextElement()] = item.Key;
                }
            }

            return normalizedMappingDictionary;
        }
    }
}