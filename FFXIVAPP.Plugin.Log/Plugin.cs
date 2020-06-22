// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Plugin.cs" company="SyndicatedLife">
//   Copyright(c) 2018 Ryan Wilson &amp;lt;syndicated.life@gmail.com&amp;gt; (http://syndicated.life/)
//   Licensed under the MIT license. See LICENSE.md in the solution root for full license information.
// </copyright>
// <summary>
//   Plugin.cs Implementation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;

static class ByteArrayRocks
{
    static readonly int[] Empty = new int[0];

    public static int[] Locate(this byte[] self, byte?[] candidate)
    {
        if (IsEmptyLocate(self, candidate))
            return Empty;

        var list = new List<int>();

        for (int i = 0; i < self.Length; i++)
        {
            if (!IsMatch(self, i, candidate))
                continue;

            list.Add(i);
        }

        return list.Count == 0 ? Empty : list.ToArray();
    }

    static bool IsMatch(byte[] array, int position, byte?[] candidate)
    {
        if (candidate.Length > (array.Length - position))
            return false;

        for (int i = 0; i < candidate.Length; i++)
        {
            if (candidate[i] == null)
                continue;

            if (array[position + i] != candidate[i])
                return false;
        }

        return true;
    }

    static bool IsEmptyLocate(byte[] array, byte?[] candidate)
    {
        return array == null
            || candidate == null
            || array.Length == 0
            || candidate.Length == 0
            || candidate.Length > array.Length;
    }
}

namespace Bot
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.IO;
    using Newtonsoft.Json;
        
    #region Discord
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Discord;
    using Discord.WebSocket;
    using FFXIVAPP.Plugin.Log.Properties;

    class FFXIVHandler
    {
        private static class ClipHelper
        {
            private static string Run(string filename, string arguments)
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = filename,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                    }
                };
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }

            private static string Bat(string cmd)
            {
                var escapedArgs = cmd.Replace("\"", "\\\"");
                string result = Run("cmd.exe", $"/c \"{escapedArgs}\"");
                return result;
            }

            public static void Copy(string val)
            {
                Bat($"echo {val} | clip");
            }
        }

        private readonly string ChatChannel = Settings.Default.FFXIV__ChannelID;
      
        private readonly WindowsInput.InputSimulator inputSimulator;
        private DiscordHandler _discordHandler;

        public FFXIVHandler(FFXIVAPP.IPluginInterface.IPluginHost host)
        {
            host.ChatLogItemReceived += OnChatMessageReceived;
            inputSimulator = new WindowsInput.InputSimulator();
        }

        public byte[][] Split(byte[] input, byte separator, bool ignoreEmptyEntries = false)
        {
            var subArrays = new List<byte[]>();
            var start = 0;
            for (var i = 0; i <= input.Length; ++i)
            {
                if (input.Length == i || input[i] == separator)
                {
                    if (i - start > 0 || ignoreEmptyEntries)
                    {
                        var destination = new byte[i - start];
                        Array.Copy(input, start, destination, 0, i - start);
                        subArrays.Add(destination);
                    }
                    start = i + 1;
                }
            }

            return subArrays.ToArray();
        }

        class ItemLinkReplacer
        {
            private static readonly byte?[] ItemLinkStartPattern = { 0x02, 0x48, 0x04, 0xf2, 0x02, null, 0x03 };
            private static readonly byte?[] ItemLinkEndPattern = { 0x01, 0x03, 0x02, 0x13, 0x02, 0xEC, 0x03 };

            private static readonly byte?[] ItemNameStartPattern = { 0x02, 0x01, 0xff };
            private static readonly byte?[] ItemNameEndPattern = { 0x03, 0x02, 0x48, 0x04 };

            public struct ItemReplacement
            {
                public int StartIndex;
                public int EndIndex;
                private string _itemName;
                public string ItemName
                {
                    get => $" [[{_itemName}]] ";
                    set
                    {
                        _itemName = value;
                    }
                }
            };

            private static List<ItemReplacement> GetReplacementsFromText(byte[] rawMessage)
            {
                List<ItemReplacement> result = new List<ItemReplacement>();

                var itemLinkStartPositions = ByteArrayRocks.Locate(rawMessage, ItemLinkStartPattern);

                foreach (var itemLinkStartPosition in itemLinkStartPositions)
                {
                    var itemNameEndPosition = ByteArrayRocks.Locate(rawMessage.Skip(itemLinkStartPosition).ToArray(), ItemNameEndPattern)[0];
                    
                    var itemLinkBuffer = rawMessage.Skip(itemLinkStartPosition).Take(itemNameEndPosition).ToArray();
                    var itemNameStartPosition = ByteArrayRocks.Locate(itemLinkBuffer, ItemNameStartPattern).Last() + ItemNameStartPattern.Length + 1; // +1 as the character before the item name is always random but not part of the name
                    var itemName = itemLinkBuffer.Skip(itemNameStartPosition).Take(itemNameEndPosition).ToArray();
                    
                    var fullStop = ByteArrayRocks.Locate(rawMessage.Skip(itemLinkStartPosition).ToArray(), ItemLinkEndPattern)[0];
                    
                    result.Add(new ItemReplacement
                    {
                        StartIndex = itemLinkStartPosition,
                        EndIndex = itemLinkStartPosition + fullStop + ItemLinkEndPattern.Length,
                        ItemName = System.Text.Encoding.UTF8.GetString(itemName),
                    });
                }

                return result;
            }

            public static byte[] ReplaceItemReferences(byte[] rawMessage)
            {
                var messageCopy = rawMessage.ToArray();

                List<ItemReplacement> replacements = GetReplacementsFromText(messageCopy);

                // apply the replacements in reverse to not change positional indices
                replacements.Reverse();
                foreach (var replacement in replacements)
                {
                    var before = new ArraySegment<byte>(messageCopy, 0, replacement.StartIndex);
                    var mid = System.Text.Encoding.UTF8.GetBytes(replacement.ItemName);
                    var after = new ArraySegment<byte>(messageCopy, replacement.EndIndex, messageCopy.Length - (replacement.EndIndex));
                    messageCopy = before.Concat(mid).Concat(after).ToArray();
                }

                return messageCopy;
            }
        }


        private void OnChatMessageReceived(object sender, FFXIVAPP.IPluginInterface.Events.ChatLogItemEvent e)
        {
            if (sender == null)
            {
                return;
            }

            if (e.ChatLogItem.Code != ChatChannel)
            {
                return;
            }

            if (e.ChatLogItem.Code != ChatChannel)
            {
                return;
            }

            if (e.ChatLogItem.Line.StartsWith(Settings.Default.FFXIV__CharacterName))
            {
                return;
            }

            byte[] utf8Message = ItemLinkReplacer.ReplaceItemReferences(e.ChatLogItem.Bytes);

            try
            {
                System.IO.File.WriteAllBytes(System.IO.Directory.GetCurrentDirectory() + $"\\Plugins\\FFXIVAPP.Plugin.Log\\b-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", e.ChatLogItem.Bytes);
                System.IO.File.WriteAllText(System.IO.Directory.GetCurrentDirectory() + $"\\Plugins\\FFXIVAPP.Plugin.Log\\l-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.txt", e.ChatLogItem.Raw);
                var split = Split(utf8Message, 0x1F);

                // cross-world user require special treatment
                switch (split[1].ToList().Count(b => b== 0x03))
                {
                    case 3:
                        {

                            var crossWorldInfoSplit = Split(split[1], 0x03);
                            var characterName = crossWorldInfoSplit[1].TakeWhile(item => item != 0x02).ToArray();
                            var realmName = crossWorldInfoSplit[3];
                            _discordHandler.Broadcast($"<{System.Text.Encoding.UTF8.GetString(characterName)}@{System.Text.Encoding.UTF8.GetString(realmName)}> {System.Text.Encoding.UTF8.GetString(split[2])}");
                            break;
                        }
                    case 2:
                        {
                            var crossWorldInfoSplit = Split(split[1], 0x03);
                            var characterName = crossWorldInfoSplit[1].TakeWhile(item => item != 0x02).ToArray();
                            _discordHandler.Broadcast($"<{System.Text.Encoding.UTF8.GetString(characterName)}> {System.Text.Encoding.UTF8.GetString(split[2])}");
                            break;
                        }
                    case 0:
                        _discordHandler.Broadcast($"<{System.Text.Encoding.UTF8.GetString(split[1])}> {System.Text.Encoding.UTF8.GetString(split[2])}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\a.txt", ex.ToString());
                System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\a.txt", ex.InnerException.ToString());
            }
        }

        private static List<string> WordWrap(string input, int maxCharacters)
        {
            List<string> lines = new List<string>();

            if (!input.Contains(" "))
            {
                int start = 0;
                while (start < input.Length)
                {
                    lines.Add(input.Substring(start, Math.Min(maxCharacters, input.Length - start)));
                    start += maxCharacters;
                }
            }
            else
            {
                string[] words = input.Split(' ');

                string line = "";
                foreach (string word in words)
                {
                    if ((line + word).Length > maxCharacters)
                    {
                        lines.Add(line.Trim());
                        line = "";
                    }

                    line += string.Format("{0} ", word);
                }

                if (line.Length > 0)
                {
                    lines.Add(line.Trim());
                }
            }

            return lines;
        }

        public async Task Broadcast(string authorName, string message)
        {
            var wrappedMessages = WordWrap(message, 500 - (authorName.Length + 4));
            foreach (var wrappedMessage in wrappedMessages)
            {
                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
                await Task.Delay(300);
                inputSimulator.Keyboard.Sleep(300);
                inputSimulator.Keyboard.TextEntry($"[{authorName}]: {wrappedMessage}   ");
                await Task.Delay(300);
                inputSimulator.Keyboard.Sleep(300);
                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
                await Task.Delay(500);
                inputSimulator.Keyboard.Sleep(500);
            }
        }

        public Task MainAsync(DiscordHandler discordHandler)
        {
            _discordHandler = discordHandler;

            return Task.CompletedTask;
        }
    }

    class DiscordHandler
    {
        private string _token;
        private Func<string, string, Task> _onMessageHandler;
        private Func<Dictionary<string, string>> _gatherDebugInfoHandler;

        private readonly DiscordSocketClient _client;

        private static readonly ulong[] AdminUserIDs = Settings.Default.Discord__AdminUserIDs.Split(',').Where(entry=>!string.IsNullOrEmpty(entry)).Select(entry=>ulong.Parse(entry.Trim())).ToArray();

        private readonly ulong GuildID = ulong.Parse(Settings.Default.Discord__GuildID);
        private readonly ulong ChannelID = ulong.Parse(Settings.Default.Discord__ChannelID);

        public DiscordHandler(string token)
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
            System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\d.txt", log.ToString());
        }

        private async Task ReadyAsync()
        {
            System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\e.txt", DateTime.Now.ToString("o"));
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
            var libraryPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "Plugins", "FFXIVAPP.Plugin.Log", "FFXIVAPP.Plugin.Log.dll");

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

            var mergedInfo = new Dictionary<string, string>[]{ generalInfo, discordInfo, ffxivInfo}.SelectMany(dict => dict).ToDictionary(pair => pair.Key, pair => pair.Value);

            int longestKey = mergedInfo.Keys.Select(item => item.Length).OrderByDescending(item => item).First();

            return $@"Instance info: ```{
                string.Join($"\r\n{"".PadRight((longestKey*2)+2, '-')}\r\n", new Dictionary<string, string>[] { generalInfo, discordInfo, ffxivInfo }.Select(
                dictionary => string.Join("\r\n",
                    dictionary.Select(item => $"{item.Key.PadRight(longestKey)} = '{item.Value.Trim()}'")
                )
            ))}```";
        }

        enum OnlineStatus : byte
        {
            Online = 0x80,
            Offline = 0x00
        };

        enum Rank : byte
        {
            Master = 0x03,
            Leader = 0x02,
            Memver = 0x01,
            InvitePending = 0x00
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct Character
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
            byte[] a;
            [MarshalAs(UnmanagedType.U1, SizeConst = 1)]
            public OnlineStatus OnlineStatus;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            byte[] b;
            [MarshalAs(UnmanagedType.U1, SizeConst = 1)]
            public Rank Rank;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            byte[] c;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string CharacterName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
            byte[] d;

            public bool IsValid => CharacterName.Length > 0 && CharacterName != Settings.Default.FFXIV__CharacterName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);
        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public enum DeviceCap
        {
            DESKTOPVERTRES = 117,
            DESKTOPHORZRES = 118,
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
            public int Width => Right - Left;
            public int Height => Bottom - Top;
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


        const int colorThreshold = 2;
        // ARGB format
        private readonly byte[] pixelOrder = new byte[][]
        {
            new byte[]{181, 182, 181, 255},
            new byte[]{129, 132, 129, 255},
            new byte[]{24, 24, 24, 255},
            new byte[]{41, 44, 41, 255 }
        }.SelectMany(i => i).ToArray();

        private readonly Point refreshPixelOffset = new Point(-386, 236);
        private readonly System.Drawing.Color refreshColor = System.Drawing.Color.FromArgb(255, 153, 152, 153);

        private Point FindInBitmap(Bitmap b)
        {
            System.Drawing.Imaging.BitmapData data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, b.PixelFormat);
            unsafe
            {
                for (int y = 0; y < data.Height; ++y)
                {
                    byte* pRow = (byte*)data.Scan0 + y * data.Stride;
                    for (int x = 0; x < data.Width - ((pixelOrder.Length / 4) - 1); ++x)
                    {
                        bool match = true;
                        for (int i = 0; i < pixelOrder.Length; i++)
                        {
                            if (pRow[(x * 4) + i] < (pixelOrder[i] - colorThreshold) || pRow[(x * 4) + i] > (pixelOrder[i] + colorThreshold))
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            return new Point(x, y);
                        }
                    }
                }
            }

            b.UnlockBits(data);
            return new Point(-1, -1);
        }

        public async Task<string> GetOnlineStatus()
        {
            GetCursorPos(out POINT previousMousePosition);

            var ffProc = Process.GetProcessesByName("ffxiv_dx11")[0] ?? Process.GetProcessesByName("ffxiv")[0];

            Point point = new Point(0, 0);
            ClientToScreen(ffProc.MainWindowHandle, ref point);
            GetClientRect(ffProc.MainWindowHandle, out RECT area);

            Point refreshButton = new Point();

            using (Bitmap bitmap = new Bitmap(area.Width, area.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(new Point(point.X, point.Y), Point.Empty, new Size(area.Width, area.Height));
                    refreshButton = FindInBitmap(bitmap);
                }
            }

            if (refreshButton.X == -1)
            {
                return "CWL Kontaktliste konnte nicht akutallisiert werden. [1]";
            }

            var absoluteRefreshButton = new Point(
                refreshButton.X + point.X,
                refreshButton.Y + point.Y
            );

            Graphics gr = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr desktop = gr.GetHdc();

            new WindowsInput.InputSimulator().Mouse.MoveMouseTo(
                absoluteRefreshButton.X * 65535 / GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPHORZRES),
                absoluteRefreshButton.Y * 65535 / GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES)
            ).LeftButtonDown().Sleep(100).LeftButtonUp();

            const int exitThresholdInSeconds = 5;
            DateTime startedAt = DateTime.Now;
            bool hasRefreshed = false;
            bool exceededThreshold = false;

            using (Bitmap bitmap = new Bitmap(area.Width, area.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    while (exceededThreshold == false && hasRefreshed == false)
                    {
                        g.CopyFromScreen(new Point(point.X, point.Y), Point.Empty, new Size(area.Width, area.Height));
                        hasRefreshed = bitmap.GetPixel(refreshButton.X + refreshPixelOffset.X, refreshButton.Y + refreshPixelOffset.Y) == refreshColor;

                        if (DateTime.Now > startedAt.AddSeconds(exitThresholdInSeconds))
                        {
                            exceededThreshold = true;
                            break;
                        }

                        await Task.Delay(10);
                    }

                    while (exceededThreshold == false && hasRefreshed == true)
                    {
                        g.CopyFromScreen(new Point(point.X, point.Y), Point.Empty, new Size(area.Width, area.Height));
                        hasRefreshed = bitmap.GetPixel(refreshButton.X + refreshPixelOffset.X, refreshButton.Y + refreshPixelOffset.Y) == refreshColor;

                        if (DateTime.Now > startedAt.AddSeconds(exitThresholdInSeconds))
                        {
                            exceededThreshold = true;
                            break;
                        }

                        await Task.Delay(10);
                    }
                }
            }

            if (!Sharlayan.Scanner.Instance.Locations.ContainsKey("CWL"))
            {
                return "CWL Kontaktliste konnte nicht akutallisiert werden. [2]";
            }

            List<Character> cwlMember = new List<Character>();

            const int length = 96;

            int currentMemberCount = Sharlayan.MemoryHandler.Instance.GetByteArray(
                new IntPtr(Sharlayan.Scanner.Instance.Locations["CWL"].GetAddress().ToInt64() - Sharlayan.Scanner.Instance.Locations["CWL"].Offset - 0x72CE8),
                1
            )[0];

            for (int i = 0; i < currentMemberCount; ++i)
            {
                cwlMember.Add(ByteArrayToStructure<Character>(Sharlayan.MemoryHandler.Instance.GetByteArray(
                    new IntPtr(Sharlayan.Scanner.Instance.Locations["CWL"].GetAddress().ToInt64() - 10 + (length * i)),
                    length
                )));
            }

            string content = "";

            var orderedMembers = cwlMember.Where(member => member.IsValid).GroupBy(member => member.OnlineStatus).ToDictionary(entry => entry.Key, entry => entry.ToList());
            content += "```";
            if (orderedMembers[OnlineStatus.Online].Count > 0)
            {
                foreach (var member in orderedMembers[OnlineStatus.Online])
                {
                    content += $" + {member.CharacterName}\r\n";
                }
            }
            content += "\r\n";
            if (orderedMembers[OnlineStatus.Offline].Count > 0)
            {
                foreach (var member in orderedMembers[OnlineStatus.Offline])
                {
                    content += $" - {member.CharacterName}\r\n";
                }
            }
            content += "```";

            if (exceededThreshold)
            {
                content += "(Online-status might not have updated properly; re-entering the command might help)";
            }

            new WindowsInput.InputSimulator().Mouse.MoveMouseTo(
                previousMousePosition.X * 65535 / GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPHORZRES),
                previousMousePosition.Y * 65535 / GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES)
            ).LeftButtonDown().Sleep(100).LeftButtonUp();

            return content;
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
                content = content.Replace(match.Value, "@"+this._client.GetUser(ulong.Parse(match.Groups[1].Value)).Username);
            }

            foreach (Match match in rooms.Matches(content))
            {
                content = content.Replace(match.Value, "#"+this._client.GetGuild(GuildID).GetTextChannel(ulong.Parse(match.Groups[1].Value)).Name);
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
#endregion

#region Pulseway
    class Pulseway
    {
        private const string ENDPOINT = "https://api.pulseway.com/v2/";
        private static readonly string USERNAME = Settings.Default.Pulseway__Username;
        private static readonly string PASSWORD = Settings.Default.Pulseway__Password;

        public class NotifyRequest
        {
            public string instance_id { get; set; }
            public string title { get; set; }
            public string message { get; set; }
            public string priority { get; set; }
        }

        public enum Priority
        {
            low,
            normal,
            elevated,
            critical
        }

        public static bool IsActive()
        {
            return !string.IsNullOrEmpty(USERNAME);
        }

        public static void SendMessage(string title, string message, Priority priority)
        {
            if (!IsActive())
            {
                return;
            }

            var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(USERNAME + ":" + PASSWORD)));
            var response = client.PostAsync(ENDPOINT + "notifications", new StringContent(JsonConvert.SerializeObject(new NotifyRequest
            {
                title = title,
                message = message,
                priority = priority.ToString(),
                instance_id = Settings.Default.Pulseway__InstanceID
            }), System.Text.Encoding.UTF8, "application/json")).Result;

            Console.WriteLine(response);
        }
    }
#endregion
}

namespace FFXIVAPP.Plugin.Log
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Configuration;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;

    using FFXIVAPP.Common.Events;
    using FFXIVAPP.Common.Helpers;
    using FFXIVAPP.Common.Models;
    using FFXIVAPP.Common.Utilities;
    using FFXIVAPP.IPluginInterface;
    using FFXIVAPP.Plugin.Log.Helpers;
    using FFXIVAPP.Plugin.Log.Properties;

    using NLog;

    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, INotifyPropertyChanged
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

        public void InitiateBot()
        {
            try
            {
                var discordHandler = new Bot.DiscordHandler(Settings.Default.Discord__APIKey);
                var ffxivHandler = new Bot.FFXIVHandler(this.Host);
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
                        System.IO.File.WriteAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\c.txt", e.ToString());
                        System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\c.txt", e.InnerException.ToString());
                    }
                });

                Task.Run(async () => {
                    try
                    {
                        await ffxivHandler.MainAsync(discordHandler);
                    }
                    catch (Exception e)
                    {
                        System.IO.File.WriteAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\d.txt", e.ToString());
                        System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\d.txt", e.InnerException.ToString());
                    }
                });
            }
            catch (Exception e)
            {
                System.IO.File.WriteAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\e.txt", e.ToString());
                System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\e.txt", e.InnerException.ToString());
            }

            System.IO.File.WriteAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\f.txt", "f");
            System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Log\\f.txt", "f");
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
                { "Character name", characterName }
            };
        }

        public string characterName = "";

        public void Initialize(IPluginHost pluginHost)
        {
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