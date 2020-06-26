// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Widgets.cs" company="SyndicatedLife">
//   Copyright(c) 2018 Ryan Wilson &amp;lt;syndicated.life@gmail.com&amp;gt; (http://syndicated.life/)
//   Licensed under the MIT license. See LICENSE.md in the solution root for full license information.
// </copyright>
// <summary>
//   Widgets.cs Implementation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace FFXIVAPP.Plugin.Log {
    using System;

    using FFXIVAPP.Common.Models;
    using FFXIVAPP.Common.Utilities;

    using NLog;

    public class Widgets {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static Lazy<Widgets> _instance = new Lazy<Widgets>(() => new Widgets());


        public static Widgets Instance {
            get {
                return _instance.Value;
            }
        }
    }
}