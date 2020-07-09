using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FFXIVAPP.Plugin.Discord.ChatHandler
{
    using NLog;

    public class Discord : IDiscord
    {
        private readonly string _token;
        private Func<string, string, Task> _onMessageHandler;
        private Func<Dictionary<string, string>> _gatherDebugInfoHandler;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly DiscordSocketClient _client;

        private static readonly ulong[] AdminUserIDs = Properties.Settings.Default.Discord__AdminUserIDs.Split(',').Where(entry => !string.IsNullOrEmpty(entry)).Select(entry => ulong.Parse(entry.Trim())).ToArray();

        private readonly ulong _guildID = ulong.Parse(Properties.Settings.Default.Discord__GuildID);
        private readonly ulong _channelID = ulong.Parse(Properties.Settings.Default.Discord__ChannelID);

        public Discord(string token)
        {
            _token = token;

            _client = new DiscordSocketClient();

#pragma warning disable 1998
            _client.Log += async (log) => _logger.Trace(log.ToString());
            _client.Ready += async () => _logger.Trace($"Ready at: {DateTime.Now:o}");
#pragma warning restore 1998

            _client.MessageReceived += MessageReceivedAsync;
        }

        public async Task MainAsync(Func<string, string, Task> onMessageHandler, Func<Dictionary<string, string>> gatherDebugInfoHandler)
        {
            _onMessageHandler = onMessageHandler;
            _gatherDebugInfoHandler = gatherDebugInfoHandler;

            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        public void SetIsActive(bool active)
        {
            if (!Pulseway.IsActive())
            {
                return;
            }

            if (!active)
            {
                Pulseway.SendMessage("FFXIV - Lost chat connection", "FFXIV is running but no longer connected to the chat", Pulseway.Priority.Critical);
            }
            else
            {
                Pulseway.SendMessage("FFXIV - Restored chat connection", "FFXIVApp has restored connection the chat server", Pulseway.Priority.Low);
            }
            _client.SetActivityAsync(new Game(active ? "Final Fantasy XIV" : "nothing, really :(")).Wait();
            _client.SetStatusAsync(active ? UserStatus.Online : UserStatus.AFK).Wait();
        }

        readonly Dictionary<string, string> _specialFFXIVCharacters = new Dictionary<string, string>
        {
            {"", ""}, // Item tag icon
            {"", "(HQ)"} // HQ icon
        };

        private void ConvertFFXIVSymbols(ref string message)
        {
            foreach (var specialFFXIVCharacter in _specialFFXIVCharacters)
            {
                message = message.Replace(specialFFXIVCharacter.Key, specialFFXIVCharacter.Value);
            }
        }

        public async Task Broadcast(string message)
        {
            ConvertFFXIVSymbols(ref message);
            await _client.GetGuild(_guildID).GetTextChannel(_channelID).SendMessageAsync(message);
        }

        private string GenerateSystemInfo()
        {
            var libraryPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location) ?? "n/a", "Plugins", "FFXIVAPP.Plugin.Discord", "FFXIVAPP.Plugin.Discord.dll");

            var generalInfo = new Dictionary<string, string>
            {
                { "IP", new WebClient().DownloadString("http://icanhazip.com") },
                { "Machine name", Environment.MachineName },
                { "Username", Environment.UserName },
                { "Process ID", Process.GetCurrentProcess().Id.ToString() },
                { "Process started at", Process.GetCurrentProcess().StartTime.ToString("O") },
                { "Module path", libraryPath },
                { "Module modified at", new FileInfo(libraryPath).LastWriteTime.ToString("O") },
            };

            var discordInfo = new Dictionary<string, string>
            {
                { "Discord server ID", _guildID.ToString() },
                { "Discord channel ID", _channelID.ToString() },
                { "Discord server name", _client.GetGuild(_guildID).Name },
                { "Discord channel name", _client.GetGuild(_guildID).GetTextChannel(_channelID).Name },
            };

            var ffxivInfo = _gatherDebugInfoHandler();

            var mergedInfo = new[] { generalInfo, discordInfo, ffxivInfo }.SelectMany(dict => dict).ToDictionary(pair => pair.Key, pair => pair.Value);

            int longestKey = mergedInfo.Keys.Select(item => item.Length).OrderByDescending(item => item).First();

            return $@"Instance info: ```{
                string.Join($"\r\n{"".PadRight((longestKey * 2) + 2, '-')}\r\n", new[] { generalInfo, discordInfo, ffxivInfo }.Select(
                dictionary => string.Join("\r\n",
                    dictionary.Select(item => $"{item.Key.PadRight(longestKey)} = '{item.Value.Trim()}'")
                )
            ))}```";
        }

        private async Task HandleDM(SocketMessage message)
        {
            if (!AdminUserIDs.Contains(message.Author.Id))
            {
                return;
            }

            await message.Channel.SendMessageAsync(GenerateSystemInfo());
        }

        private readonly Regex _emoji = new Regex("\\<.?\\:([^:]*)\\:\\d*\\>");
        private string ResolveEmojis(string content)
        {
            foreach (Match match in _emoji.Matches(content))
            {
                content = content.Replace(match.Value, $":{match.Groups[1].Value}:");
            }
            return content;
        }

        private readonly Regex _usernames = new Regex("\\<\\@\\!(\\d*)\\>");
        private readonly Regex _rooms = new Regex("\\<\\#(\\d*)\\>");
        private string ResolveMentions(string content)
        {
            foreach (Match match in _usernames.Matches(content))
            {
                content = content.Replace(match.Value, "@" + this._client.GetUser(ulong.Parse(match.Groups[1].Value)).Username);
            }

            foreach (Match match in _rooms.Matches(content))
            {
                content = content.Replace(match.Value, "#" + this._client.GetGuild(_guildID).GetTextChannel(ulong.Parse(match.Groups[1].Value)).Name);
            }
            return content;
        }

        private async Task HandleChannelMessage(SocketMessage message)
        {
            if (((SocketGuildChannel)message.Channel).Guild.Id != _guildID)
            {
                return;
            }

            if (message.Channel.Id != _channelID)
            {
                return;
            }

            if (message.Source != MessageSource.User)
            {
                return;
            }

            string formattedMessage = message.Content;
            formattedMessage = ResolveEmojis(formattedMessage);
            formattedMessage = ResolveMentions(formattedMessage);

            await _onMessageHandler(message.Author.Username, formattedMessage);
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            if (message.Channel is SocketDMChannel)
            {
                await HandleDM(message);
            }

            if (message.Channel is SocketGuildChannel)
            {
                await HandleChannelMessage(message);
            }
        }
    }
}
