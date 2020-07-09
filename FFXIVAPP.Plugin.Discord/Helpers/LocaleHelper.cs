﻿namespace FFXIVAPP.Plugin.Discord.Helpers {
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Windows;

    using FFXIVAPP.Plugin.Discord.Localization;

    internal static class LocaleHelper {
        /// <summary>
        /// </summary>
        /// <param name="cultureInfo"> </param>
        public static Dictionary<string, string> Update(CultureInfo cultureInfo) {
            var culture = cultureInfo.TwoLetterISOLanguageName;
            ResourceDictionary dictionary;
            if (Constants.Supported.Contains(culture)) {
                switch (culture) {
                    case "fr":
                        dictionary = French.Context();
                        break;
                    case "ja":
                        dictionary = Japanese.Context();
                        break;
                    case "de":
                        dictionary = German.Context();
                        break;
                    case "zh":
                        dictionary = Chinese.Context();
                        break;
                    case "ru":
                        dictionary = Russian.Context();
                        break;
                    default:
                        dictionary = English.Context();
                        break;
                }
            }
            else {
                dictionary = English.Context();
            }

            return dictionary.Cast<DictionaryEntry>().ToDictionary(item => (string) item.Key, item => (string) item.Value);
        }
    }
}