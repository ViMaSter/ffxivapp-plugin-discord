using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FFXIVAPP.Plugin.Discord.ChatHandler
{
    using NLog;

    public class Discord : IDiscord
    {
        private string _token;
        private Func<string, string, Task> _onMessageHandler;
        private Func<Dictionary<string, string>> _gatherDebugInfoHandler;

        private readonly DiscordSocketClient _client;

        private static readonly ulong[] AdminUserIDs = Properties.Settings.Default.Discord__AdminUserIDs.Split(',').Where(entry => !string.IsNullOrEmpty(entry)).Select(entry => ulong.Parse(entry.Trim())).ToArray();

        private readonly ulong GuildID = ulong.Parse(Properties.Settings.Default.Discord__GuildID);
        private readonly ulong ChannelID = ulong.Parse(Properties.Settings.Default.Discord__ChannelID);

        public Discord(string token)
        {
            _token = token;

            _client = new DiscordSocketClient();

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
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

        private async Task LogAsync(LogMessage log)
        {
            System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Discord\\d.txt", log.ToString());
        }

        private async Task ReadyAsync()
        {
            System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Discord\\x.txt", DateTime.Now.ToString("o"));
        }

        public void SetIsActive(bool active)
        {
            if (Pulseway.IsActive())
            {
                if (!active)
                {
                    Pulseway.SendMessage("FFXIV - Lost chat connection", "FFXIV is running but no longer connected to the chat", Pulseway.Priority.critical);
                }
                else
                {
                    Pulseway.SendMessage("FFXIV - Restored chat connection", "FFXIVApp has restored connection the chat server", Pulseway.Priority.low);
                }
                _client.SetActivityAsync(new Game(active ? "Final Fantasy XIV" : "nothing, really :(")).Wait();
                _client.SetStatusAsync(active ? UserStatus.Online : UserStatus.AFK).Wait();
            }
        }

        Dictionary<string, string> SpecialFFXIVCharacters = new Dictionary<string, string>
        {
            {"", ""}, // Item tag icon
            {"", "(HQ)"} // HQ icon
        };

        private void ConvertFFXIVSymbols(ref string message)
        {
            foreach (var specialFFXIVCharacter in SpecialFFXIVCharacters)
            {
                message = message.Replace(specialFFXIVCharacter.Key, specialFFXIVCharacter.Value);
            }
        }

        public void Broadcast(string message)
        {
            ConvertFFXIVSymbols(ref message);
            var result = _client.GetGuild(GuildID).GetTextChannel(ChannelID).SendMessageAsync(message).Result;
        }

        private string GenerateSystemInfo()
        {
            var libraryPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "Plugins", "FFXIVAPP.Plugin.Discord", "FFXIVAPP.Plugin.Discord.dll");

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
                { "Discord server ID", GuildID.ToString() },
                { "Discord channel ID", ChannelID.ToString() },
                { "Discord server name", _client.GetGuild(GuildID).Name },
                { "Discord channel name", _client.GetGuild(GuildID).GetTextChannel(ChannelID).Name },
            };

            var ffxivInfo = _gatherDebugInfoHandler();

            var mergedInfo = new Dictionary<string, string>[] { generalInfo, discordInfo, ffxivInfo }.SelectMany(dict => dict).ToDictionary(pair => pair.Key, pair => pair.Value);

            int longestKey = mergedInfo.Keys.Select(item => item.Length).OrderByDescending(item => item).First();

            return $@"Instance info: ```{
                string.Join($"\r\n{"".PadRight((longestKey * 2) + 2, '-')}\r\n", new Dictionary<string, string>[] { generalInfo, discordInfo, ffxivInfo }.Select(
                dictionary => string.Join("\r\n",
                    dictionary.Select(item => $"{item.Key.PadRight(longestKey)} = '{item.Value.Trim()}'")
                )
            ))}```";
        }

        public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        private async Task HandleDM(SocketMessage message)
        {
            if (!AdminUserIDs.Contains(message.Author.Id))
            {
                return;
            }

            await message.Channel.SendMessageAsync(GenerateSystemInfo());
        }

        private readonly Regex emoji = new Regex("\\<.?\\:([^:]*)\\:\\d*\\>");
        private string ResolveEmojis(string content)
        {
            foreach (Match match in emoji.Matches(content))
            {
                content = content.Replace(match.Value, $":{match.Groups[1].Value}:");
            }
            return content;
        }

        private readonly Regex usernames = new Regex("\\<\\@\\!(\\d*)\\>");
        private readonly Regex rooms = new Regex("\\<\\#(\\d*)\\>");
        private string ResolveMentions(string content)
        {
            foreach (Match match in usernames.Matches(content))
            {
                content = content.Replace(match.Value, "@" + this._client.GetUser(ulong.Parse(match.Groups[1].Value)).Username);
            }

            foreach (Match match in rooms.Matches(content))
            {
                content = content.Replace(match.Value, "#" + this._client.GetGuild(GuildID).GetTextChannel(ulong.Parse(match.Groups[1].Value)).Name);
            }
            return content;
        }

        private async Task HandleChannelMessage(SocketMessage message)
        {
            if (((SocketGuildChannel)message.Channel).Guild.Id != GuildID)
            {
                return;
            }

            if (message.Channel.Id != ChannelID)
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
