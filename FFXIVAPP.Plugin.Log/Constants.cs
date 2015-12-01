﻿// FFXIVAPP.Plugin.Log
// FFXIVAPP & Related Plugins/Modules
// Copyright © 2007 - 2015 Ryan Wilson - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using FFXIVAPP.Common.Helpers;

namespace FFXIVAPP.Plugin.Log
{
    public static class Constants
    {
        #region Declarations

        public const string LibraryPack = "pack://application:,,,/FFXIVAPP.Plugin.Log;component/";

        public static readonly string[] Supported =
        {
            "ja", "fr", "en", "de", "ru"
        };

        public static StringComparison InvariantComparer = StringComparison.InvariantCultureIgnoreCase;

        public static readonly string[] ChatPublic =
        {
            "000A", "000C", "000D", "000E"
        };

        public static readonly string[] ChatSay =
        {
            "000A"
        };

        public static readonly string[] ChatTell =
        {
            "000C", "000D"
        };

        public static readonly string[] ChatParty =
        {
            "000E"
        };

        public static readonly string[] ChatShout =
        {
            "000B"
        };

        public static readonly string[] ChatYell =
        {
            "001E"
        };

        public static readonly string[] ChatLS =
        {
            "0010", "0011", "0012", "0013", "0014", "0015", "0016", "0017"
        };

        public static readonly string[] ChatAlliance =
        {
            "000F"
        };

        public static readonly string[] ChatFC =
        {
            "0018"
        };

        public static string BaseDirectory
        {
            get
            {
                var appDirectory = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly()
                                                                         .CodeBase).LocalPath);
                return Path.Combine(appDirectory, "Plugins", Plugin.PName);
            }
        }

        public static Dictionary<string, string> Linkshells
        {
            get
            {
                var linkshells = new Dictionary<string, string>();
                linkshells.Add("0010", "[1] ");
                linkshells.Add("0011", "[2] ");
                linkshells.Add("0012", "[3] ");
                linkshells.Add("0013", "[4] ");
                linkshells.Add("0014", "[5] ");
                linkshells.Add("0015", "[6] ");
                linkshells.Add("0016", "[7] ");
                linkshells.Add("0017", "[8] ");
                linkshells.Add("0018", "[FC] ");
                return linkshells;
            }
        }

        #endregion

        #region Property Bindings

        private static XDocument _xSettings;
        private static List<string> _settings;

        public static XDocument XSettings
        {
            get
            {
                var file = Path.Combine(Common.Constants.PluginsSettingsPath, "FFXIVAPP.Plugin.Log.xml");
                var legacyFile = "./Plugins/FFXIVAPP.Plugin.Log/Settings.xml";
                if (_xSettings != null)
                {
                    return _xSettings;
                }
                try
                {
                    var found = File.Exists(file);
                    if (found)
                    {
                        _xSettings = XDocument.Load(file);
                    }
                    else
                    {
                        found = File.Exists(legacyFile);
                        _xSettings = found ? XDocument.Load(legacyFile) : ResourceHelper.XDocResource(LibraryPack + "/Defaults/Settings.xml");
                    }
                }
                catch (Exception ex)
                {
                    _xSettings = ResourceHelper.XDocResource(LibraryPack + "/Defaults/Settings.xml");
                }
                return _xSettings;
            }
            set { _xSettings = value; }
        }

        public static List<string> Settings
        {
            get { return _settings ?? (_settings = new List<string>()); }
            set { _settings = value; }
        }

        #endregion

        #region Property Bindings

        private static Dictionary<string, string> _autoTranslate;
        private static Dictionary<string, string> _chatCodes;
        private static Dictionary<string, string[]> _colors;
        private static CultureInfo _cultureInfo;

        public static Dictionary<string, string> AutoTranslate
        {
            get { return _autoTranslate ?? (_autoTranslate = new Dictionary<string, string>()); }
            set { _autoTranslate = value; }
        }

        public static Dictionary<string, string> ChatCodes
        {
            get { return _chatCodes ?? (_chatCodes = new Dictionary<string, string>()); }
            set { _chatCodes = value; }
        }

        public static string ChatCodesXml { get; set; }

        public static Dictionary<string, string[]> Colors
        {
            get { return _colors ?? (_colors = new Dictionary<string, string[]>()); }
            set { _colors = value; }
        }

        public static CultureInfo CultureInfo
        {
            get { return _cultureInfo ?? (_cultureInfo = new CultureInfo("en")); }
            set { _cultureInfo = value; }
        }

        #endregion

        #region Auto-Properties

        public static string CharacterName { get; set; }

        public static string ServerName { get; set; }

        public static string GameLanguage { get; set; }

        public static bool EnableHelpLabels { get; set; }

        public static string Theme { get; set; }

        #endregion
    }
}
