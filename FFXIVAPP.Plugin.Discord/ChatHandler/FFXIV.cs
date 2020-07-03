using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FFXIVAPP.Plugin.Discord.ChatHandler
{
    public class FFXIV
    {
        private readonly string _chatChannel = Properties.Settings.Default.FFXIV__ChannelID;

        private readonly WindowsInput.InputSimulator inputSimulator;
        private IDiscord _discordHandler;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Character _currentCharacter;

        public FFXIV(FFXIVAPP.IPluginInterface.IPluginHost host)
        {
            _currentCharacter = new Character("", "");
            host.ChatLogItemReceived += (sender, e) => {
                try {
                    OnChatMessageReceived(sender, e);
                }
                catch (Exception exception) {
                    string data = exception.ToString();
                    if (exception.InnerException != null)
                    {
                        data += "\r\nInner exception: " + exception.InnerException.ToString();
                    }
                    logger.Error($"[FFXIV::ChatLogItem] {data}");
                }
            };
            host.ConstantsUpdated += (sender, e) => {
                try
                {
                    OnConstantsUpdated(sender, e);
                }
                catch (Exception exception)
                {
                    string data = exception.ToString();
                    if (exception.InnerException != null)
                    {
                        data += "\r\nInner exception: " + exception.InnerException.ToString();
                    }
                    logger.Error($"[FFXIV::Constants] {data}");
                }
            };
            host.CurrentPlayerUpdated += (sender, e) => {
                try {
                    OnCurrentPlayerUpdated(sender, e);
                }
                catch (Exception exception) {
                    string data = exception.ToString();
                    if (exception.InnerException != null) {
                        data += "\r\nInner exception: " + exception.InnerException.ToString();
                    }

                    logger.Error($"[FFXIV::CurrentPlayer] {data}");
                }
            };

            inputSimulator = new WindowsInput.InputSimulator();
        }

        private void OnCurrentPlayerUpdated(object sender, FFXIVAPP.IPluginInterface.Events.CurrentPlayerEvent e)
        {
            if (_currentCharacter._characterName == e.CurrentPlayer.Name)
            {
                return;
            }

            _currentCharacter._characterName = e.CurrentPlayer.Name;
        }

        private void OnConstantsUpdated(object sender, FFXIVAPP.IPluginInterface.Events.ConstantsEntityEvent e)
        {
            if (_currentCharacter._worldName == e.ConstantsEntity.ServerName)
            {
                return;
            }

            _currentCharacter._worldName = e.ConstantsEntity.ServerName;
        }

        public static byte[][] Split(byte[] input, byte separator, bool ignoreEmptyEntries = false)
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

        public class ItemLinkReplacer
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
                    get => $"[[{_itemName}]]";
                    set
                    {
                        _itemName = value;
                    }
                }
            };

            private static List<ItemReplacement> GetReplacementsFromText(byte[] rawMessage)
            {
                List<ItemReplacement> result = new List<ItemReplacement>();

                var itemLinkStartPositions = rawMessage.Locate(ItemLinkStartPattern);

                foreach (var itemLinkStartPosition in itemLinkStartPositions)
                {
                    var itemNameEndPosition = rawMessage.Skip(itemLinkStartPosition).ToArray().Locate(ItemNameEndPattern)[0];

                    var itemLinkBuffer = rawMessage.Skip(itemLinkStartPosition).Take(itemNameEndPosition).ToArray();
                    var itemNameStartPosition = itemLinkBuffer.Locate(ItemNameStartPattern).Last() + ItemNameStartPattern.Length + 1; // +1 as the character before the item name is always random but not part of the name
                    var itemName = itemLinkBuffer.Skip(itemNameStartPosition).Take(itemNameEndPosition).ToArray();

                    var fullStop = ByteArrayExtensions.Locate(rawMessage.Skip(itemLinkStartPosition).ToArray(), ItemLinkEndPattern)[0];

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

        public class PFLinkReplacer
        {
            private static readonly byte?[] PFLinkStartPattern = { 0x02, 0x27, 0x08, 0x0A };
            private static readonly byte?[] PFLinkEndPattern = { 0x01, 0x03 };

            private static readonly byte?[] PFNameStartPattern = { 0x48, 0x02, 0x01, 0x03 };
            private static readonly byte?[] PFNameEndPattern = { 0x20, 0x02, 0x12, 0x02, 0x59, 0x03, 0x02, 0x27, 0x07 };

            public struct PFReplacement
            {
                public int StartIndex;
                public int EndIndex;
                private string _rawPFEntry;
                public string PFEntry
                {
                    get => $"{{{_rawPFEntry}}}";
                    set
                    {
                        _rawPFEntry = value;
                    }
                }
            };

            private static List<PFReplacement> GetReplacementsFromText(byte[] rawMessage)
            {
                List<PFReplacement> result = new List<PFReplacement>();

                var itemLinkStartPositions = ByteArrayExtensions.Locate(rawMessage, PFLinkStartPattern);

                foreach (var itemLinkStartPosition in itemLinkStartPositions)
                {
                    var itemNameEndPosition = ByteArrayExtensions.Locate(rawMessage.Skip(itemLinkStartPosition).ToArray(), PFNameEndPattern)[0];

                    var itemLinkBuffer = rawMessage.Skip(itemLinkStartPosition).Take(itemNameEndPosition).ToArray();
                    var itemNameStartPosition = ByteArrayExtensions.Locate(itemLinkBuffer, PFNameStartPattern).Last() + PFNameStartPattern.Length;
                    var itemName = itemLinkBuffer.Skip(itemNameStartPosition).Take(itemNameEndPosition).ToArray();

                    var fullStop = ByteArrayExtensions.Locate(rawMessage.Skip(itemLinkStartPosition + itemNameStartPosition).ToArray(), PFLinkEndPattern)[0];

                    result.Add(new PFReplacement
                    {
                        StartIndex = itemLinkStartPosition,
                        EndIndex = itemLinkStartPosition + itemNameStartPosition + fullStop + PFLinkEndPattern.Length,
                        PFEntry = System.Text.Encoding.UTF8.GetString(itemName),
                    });
                }

                return result;
            }

            public static byte[] ReplaceItemReferences(byte[] rawMessage)
            {
                var messageCopy = rawMessage.ToArray();

                List<PFReplacement> replacements = GetReplacementsFromText(messageCopy);

                // apply the replacements in reverse to not change positional indices
                replacements.Reverse();
                foreach (var replacement in replacements)
                {
                    var before = new ArraySegment<byte>(messageCopy, 0, replacement.StartIndex);
                    var mid = System.Text.Encoding.UTF8.GetBytes(replacement.PFEntry);
                    var after = new ArraySegment<byte>(messageCopy, replacement.EndIndex, messageCopy.Length - (replacement.EndIndex));
                    messageCopy = before.Concat(mid).Concat(after).ToArray();
                }

                return messageCopy;
            }
        }

        private class Character
        {
            public Character(string characterName, string worldName)
            {
                _characterName = characterName;
                _worldName = worldName;
            }
            public string _characterName;
            public string _worldName;
            public override string ToString() => $"<{_characterName}@{_worldName}>";
        }

        private void OnChatMessageReceived(object sender, FFXIVAPP.IPluginInterface.Events.ChatLogItemEvent e) {
            if (sender == null)
            {
                return;
            }

            if (e.ChatLogItem.Code != _chatChannel)
            {
                return;
            }

            if (e.ChatLogItem.Line.StartsWith(_currentCharacter._characterName) && !e.ChatLogItem.Line.Contains("FORCEEXEC"))
            {
                return;
            }

            logger.Trace($"[CHATLOG]: '${e.ChatLogItem.Bytes}'");

            byte[] utf8Message = ItemLinkReplacer.ReplaceItemReferences(e.ChatLogItem.Bytes);
            utf8Message = PFLinkReplacer.ReplaceItemReferences(utf8Message);

            try
            {
                var split = Split(utf8Message, 0x1F);

                Character logCharacter = null;
                string logMessage = null;

                // cross-world user require special treatment
                switch (split[1].ToList().Count(b => b == 0x03))
                {
                    case 3:
                        {
                            var crossWorldInfoSplit = Split(split[1], 0x03);
                            logCharacter = new Character(
                                System.Text.Encoding.UTF8.GetString(crossWorldInfoSplit[1].TakeWhile(item => item != 0x02).ToArray()),
                                System.Text.Encoding.UTF8.GetString(crossWorldInfoSplit[3])
                            );
                            logMessage = System.Text.Encoding.UTF8.GetString(split[2]);
                        }
                        break;
                    case 2:
                        {
                            var crossWorldInfoSplit = Split(split[1], 0x03);
                            logCharacter = new Character(
                                System.Text.Encoding.UTF8.GetString(crossWorldInfoSplit[1].TakeWhile(item => item != 0x02).ToArray()),
                                _currentCharacter._worldName
                            );
                            logMessage = System.Text.Encoding.UTF8.GetString(split[2]);
                        }
                        break;
                    case 0:
                        logCharacter = new Character(
                            System.Text.Encoding.UTF8.GetString(split[1]),
                            _currentCharacter._worldName
                        );
                        logMessage = System.Text.Encoding.UTF8.GetString(split[2]);
                        break;
                }

                if (logCharacter == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(logMessage))
                {
                    return;
                }

                _discordHandler.Broadcast($"{logCharacter} {logMessage}");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Discord\\a.txt", ex.ToString());
                System.IO.File.AppendAllText(System.IO.Directory.GetCurrentDirectory() + "\\Plugins\\FFXIVAPP.Plugin.Discord\\a.txt", ex.InnerException.ToString());
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

        public Task MainAsync(IDiscord discordHandler)
        {
            _discordHandler = discordHandler;

            return Task.CompletedTask;
        }
    }
}
