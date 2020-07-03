using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using FFXIVAPP.Common.Events;
using FFXIVAPP.Common.Helpers;
using FFXIVAPP.Common.Models;
using FFXIVAPP.Common.Utilities;
using FFXIVAPP.Plugin.Discord.Helpers;
using FFXIVAPP.Plugin.Discord.Properties;
using NLog;

using FFXIVAPP.IPluginInterface;

namespace FFXIVAPP.Plugin.Discord
{
    using NLog.Config;

    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, INotifyPropertyChanged
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private IPluginHost _host;

        private Dictionary<string, string> _locale;

        private string _name;

        private MessageBoxResult _popupResult;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public static IPluginHost PHost { get; private set; }

        public static string PName { get; private set; }

        public string Copyright { get; private set; }

        public string Description { get; private set; }

        public string FriendlyName { get; set; }

        public IPluginHost Host
        {
            get
            {
                return this._host;
            }

            set
            {
                PHost = this._host = value;
            }
        }

        public string Icon { get; private set; }

        public Dictionary<string, string> Locale
        {
            get
            {
                return this._locale ?? (this._locale = new Dictionary<string, string>());
            }

            set
            {
                this._locale = value;
                Dictionary<string, string> locale = LocaleHelper.Update(Constants.CultureInfo);
                foreach (KeyValuePair<string, string> resource in locale)
                {
                    try
                    {
                        if (this._locale.ContainsKey(resource.Key))
                        {
                            this._locale[resource.Key] = resource.Value;
                        }
                        else
                        {
                            this._locale.Add(resource.Key, resource.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logger, new LogItem(ex, true));
                    }
                }

                PluginViewModel.Instance.Locale = this._locale;
                this.RaisePropertyChanged();
            }
        }

        public string Name
        {
            get
            {
                return this._name;
            }

            private set
            {
                PName = this._name = value;
            }
        }

        public string Notice { get; private set; }

        public MessageBoxResult PopupResult
        {
            get
            {
                return this._popupResult;
            }

            set
            {
                this._popupResult = value;
                PluginViewModel.Instance.OnPopupResultChanged(new PopupResultEvent(value));
            }
        }

        public Exception Trace { get; private set; }

        public string Version { get; private set; }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error($"[UNHANDLED EXCEPTION] : '${e.ExceptionObject}'");
        }

        public void InitiateBot()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            try
            {
                var discordHandler = new ChatHandler.Discord(Settings.Default.Discord__APIKey);
                var ffxivHandler = new ChatHandler.FFXIV(this.Host);

                this.Host.ConstantsUpdated += (object sender, IPluginInterface.Events.ConstantsEntityEvent e) =>
                {
                    if (worldName == e.ConstantsEntity.ServerName)
                    {
                        return;
                    }

                    worldName = e.ConstantsEntity.ServerName;
                };

                this.Host.CurrentPlayerUpdated += (object sender, FFXIVAPP.IPluginInterface.Events.CurrentPlayerEvent e) =>
                {
                    if (characterName == e.CurrentPlayer.Name)
                    {
                        return;
                    }

                    characterName = e.CurrentPlayer.Name;

                    discordHandler.SetIsActive(!string.IsNullOrEmpty(e.CurrentPlayer.Name));
                };

                Task.Run(async () => {
                    try
                    {
                        await discordHandler.MainAsync(ffxivHandler.Broadcast, GatherDebugInfo);
                    }
                    catch (Exception e)
                    {
                        string data = e.ToString();
                        if (e.InnerException != null)
                        {
                            data += "\r\nInner exception: " + e.InnerException.ToString();
                        }
                        Logger.Error($"[DISCORD] {data}");
                    }
                });

                Task.Run(async () => {
                    try {
                        await ffxivHandler.MainAsync(discordHandler);
                    }
                    catch (Exception e)
                    {
                        string data = e.ToString();
                        if (e.InnerException != null)
                        {
                            data += "\r\nInner exception: " + e.InnerException.ToString();
                        }
                        Logger.Error($"[FFXIV] {data}");
                    }
                });
            }
            catch (Exception e)
            {
                string data = e.ToString();
                if (e.InnerException != null)
                {
                    data += "\r\nInner exception: " + e.InnerException.ToString();
                }
                Logger.Error($"[GLOBAL] {data}");
            }
        }

        public TabItem CreateTab()
        {
            this.Locale = LocaleHelper.Update(Constants.CultureInfo);
            var content = new ShellView();
            content.Loaded += ShellViewModel.Loaded;
            var tabItem = new TabItem
            {
                Header = this.Name,
                Content = content
            };

            // do your gui stuff here
            EventSubscriber.Subscribe();

            InitiateBot();

            // content gives you access to the base xaml
            return tabItem;
        }

        public void Dispose(bool isUpdating = false)
        {
            EventSubscriber.UnSubscribe();

            /*
             * If the isUpdating is true it means the application will be force closing/killed.
             * You wil have to choose what you want to do in this case.
             * By default the settings class clears the settings object and recreates it; but if killed untimely it will not save.
             * 
             * Suggested use is to not save settings if updating. Other disposing events could happen based on your needs.
             */
            if (isUpdating)
            {
                return;
            }

            Settings.Default.Save();
        }

        public Dictionary<string, string> GatherDebugInfo()
        {
            return new Dictionary<string, string>
            {
                { "Character name", characterName },
                { "World name", worldName },
            };
        }

        public string characterName = "";
        public string worldName = "";

        private void SetupPulsewayLogging()
        {
            ExtendNLogConfig();
            LogManager.ConfigurationReloaded += (sender, args) => {
                ExtendNLogConfig();
            };

            NLog.Config.ConfigurationItemFactory.Default.Targets.RegisterDefinition("PulsewayNLog", typeof(PulsewayNLogTarget));
        }

        private void ExtendNLogConfig() {
            var logTarget = new PulsewayNLogTarget
            {
                Layout = "${longdate} ${message}"
            };

            var loggingRule = new LoggingRule("*", LogLevel.Trace, logTarget);

            LogManager.Configuration.AddTarget("PulsewayNLog", logTarget);
            LogManager.Configuration.LoggingRules.Add(loggingRule);
            LogManager.ReconfigExistingLoggers();
        }

        public void Initialize(IPluginHost pluginHost)
        {
            SetupPulsewayLogging();

            this.Host = pluginHost;
            this.FriendlyName = "Log";
            this.Name = AssemblyHelper.Name;
            this.Icon = "Logo.png";
            this.Description = AssemblyHelper.Description;
            this.Copyright = AssemblyHelper.Copyright;
            this.Version = AssemblyHelper.Version.ToString();
            this.Notice = string.Empty;
        }

        private void RaisePropertyChanged([CallerMemberName] string caller = "")
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }
    }
}