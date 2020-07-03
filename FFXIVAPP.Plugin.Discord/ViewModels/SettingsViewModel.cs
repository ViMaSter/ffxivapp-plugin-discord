namespace FFXIVAPP.Plugin.Discord.ViewModels {
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows.Input;

    using FFXIVAPP.Common.ViewModelBase;
    using FFXIVAPP.Plugin.Discord.Helpers;
    using FFXIVAPP.Plugin.Discord.Properties;
    using FFXIVAPP.Plugin.Discord.Views;

    internal sealed class SettingsViewModel : INotifyPropertyChanged {
        private static Lazy<SettingsViewModel> _instance = new Lazy<SettingsViewModel>(() => new SettingsViewModel());

        public SettingsViewModel() {
            this.AddTabCommand = new DelegateCommand(AddTab);
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public static SettingsViewModel Instance {
            get {
                return _instance.Value;
            }
        }

        public ICommand AddTabCommand { get; private set; }

        /// <summary>
        /// </summary>
        private static void AddTab() {
            var xKey = SettingsView.View.TName.Text;
            string xValue;
            var xRegularExpression = SettingsView.View.TRegEx.Text;
            if (SettingsView.View.Codes.SelectedItems.Count < 1) {
                xValue = string.Empty;
            }
            else {
                xValue = SettingsView.View.Codes.SelectedItems.Cast<object>().Aggregate(string.Empty, (current, item) => current + item.ToString().Split(',')[0] + ",").Replace("[", string.Empty);
                xValue = xValue.Substring(0, xValue.Length - 1);
            }

            if (xKey == string.Empty || xValue == string.Empty || xRegularExpression == string.Empty) { }
            else {
                TabItemHelper.AddTabByName(xKey, xValue, xRegularExpression);
                SettingsView.View.TName.Text = string.Empty;
                SettingsView.View.Codes.UnselectAll();
                MainView.View.MainViewTC.SelectedIndex = MainView.View.MainViewTC.Items.Count - 1;
                ShellView.View.ShellViewTC.SelectedIndex = 0;
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string caller = "") {
            this.PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }
    }
}