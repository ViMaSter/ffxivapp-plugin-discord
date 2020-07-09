namespace FFXIVAPP.Plugin.Discord
{
    using System.Collections.Generic;

    using NLog;
    using NLog.Targets;

    [Target("PulsewayNLog")]
    public sealed class PulsewayNLogTarget : TargetWithLayout  //or inherit from Target
    {
        private readonly Dictionary<LogLevel, Pulseway.Priority> _logLevelWhitelist = new Dictionary<LogLevel, Pulseway.Priority>(){
            { LogLevel.Error, Pulseway.Priority.Elevated },
            { LogLevel.Fatal, Pulseway.Priority.Critical },
            { LogLevel.Warn,  Pulseway.Priority.Normal   }
        };

        protected override void Write(LogEventInfo logEvent) {
            if (!this._logLevelWhitelist.ContainsKey(logEvent.Level))
            {
                return;
            }

            string logMessage = this.Layout.Render(logEvent);
            Pulseway.SendMessage("FFXIV/Discord :: " + logEvent.Level.Name, logMessage, this._logLevelWhitelist[logEvent.Level]);
        }
    }
}
