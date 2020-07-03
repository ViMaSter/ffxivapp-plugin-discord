using System;

using NLog;

namespace FFXIVAPP.Plugin.Discord {
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