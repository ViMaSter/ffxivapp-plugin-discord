namespace FFXIVAPP.Plugin.Discord
{
    using System.Collections.Generic;

    using NLog;
    using NLog.Targets;

    [Target("PulsewayNLog")]
    public sealed class PulsewayNLogTarget : TargetWithLayout  //or inherit from Target
    {
        private readonly Dictionary<LogLevel, Pulseway.Priority> logLevelWhitelist = new Dictionary<LogLevel, Pulseway.Priority>(){
            { LogLevel.Error, Pulseway.Priority.elevated },
            { LogLevel.Fatal, Pulseway.Priority.critical },
            { LogLevel.Warn,  Pulseway.Priority.normal   }
        };

        protected override void Write(LogEventInfo logEvent) {
            if (!this.logLevelWhitelist.ContainsKey(logEvent.Level))
            {
                return;
            }

            string logMessage = this.Layout.Render(logEvent);
            Pulseway.SendMessage("FFXIV/Discord :: " + logEvent.Level.Name, logMessage, this.logLevelWhitelist[logEvent.Level]);
        }
    }
}
