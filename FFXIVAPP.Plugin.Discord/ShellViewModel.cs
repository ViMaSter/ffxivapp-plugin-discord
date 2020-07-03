using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

using FFXIVAPP.Common.Models;
using FFXIVAPP.Common.Utilities;
using FFXIVAPP.Plugin.Discord.Properties;
using FFXIVAPP.Plugin.Discord.Views;

using NLog;

namespace FFXIVAPP.Plugin.Discord {

    public sealed class ShellViewModel : INotifyPropertyChanged {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static Lazy<ShellViewModel> _instance = new Lazy<ShellViewModel>(() => new ShellViewModel());

        public ShellViewModel() {
            Initializer.LoadSettings();
            Initializer.LoadTabs();
            Settings.Default.PropertyChanged += DefaultOnPropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public static ShellViewModel Instance {
            get {
                return _instance.Value;
            }
        }

        internal static void Loaded(object sender, RoutedEventArgs e) {
            ShellView.View.Loaded -= Loaded;
            Initializer.ApplyTheming();
            MainView.View.MainViewTC.SelectedIndex = Settings.Default.EnableAll
                                                         ? 0
                                                         : 1;
        }

        private static void DefaultOnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "EnableAll":
                    try {
                        if (MainView.View.MainViewTC.SelectedIndex == 0 && !Settings.Default.EnableAll) {
                            MainView.View.MainViewTC.SelectedIndex = 1;
                        }
                    }
                    catch (Exception ex) {
                        Logging.Log(Logger, new LogItem(ex, true));
                    }

                    break;
                case "EnableDebug":
                    try {
                        if (MainView.View.MainViewTC.SelectedIndex == 2 && !Settings.Default.EnableDebug) {
                            MainView.View.MainViewTC.SelectedIndex = 1;
                        }
                    }
                    catch (Exception ex) {
                        Logging.Log(Logger, new LogItem(ex, true));
                    }

                    break;
                case "TranslationWidgetUIScale":
                    try {
                        Settings.Default.TranslationWidgetWidth = (int) (600 * double.Parse(Settings.Default.TranslationWidgetUIScale));
                        Settings.Default.TranslationWidgetHeight = (int) (400 * double.Parse(Settings.Default.TranslationWidgetUIScale));
                    }
                    catch (Exception) {
                        Settings.Default.TranslationWidgetWidth = 600;
                        Settings.Default.TranslationWidgetHeight = 400;
                    }

                    break;
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string caller = "") {
            this.PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }
    }
}