namespace FFXIVAPP.Plugin.Discord.ChatHandler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using WindowsInput;
    using WindowsInput.Native;

    using FFXIVAPP.IPluginInterface;
    using FFXIVAPP.IPluginInterface.Events;
    using FFXIVAPP.Plugin.Discord.Properties;

    using NLog;

    public class FFXIV
    {
        private readonly string _chatChannel = Settings.Default.FFXIV__ChannelID;

        private readonly InputSimulator _inputSimulator;
        private IDiscord _discordHandler;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly Character _currentCharacter = new Character("", "");

        public FFXIV(IPluginHost host)
        {
            host.ChatLogItemReceived += (sender, e) => {
                try {
                    OnChatMessageReceived(sender, e);
                }
                catch (Exception exception) {
                    string data = exception.ToString();
                    if (exception.InnerException != null)
                    {
                        data += "\r\nInner exception: " + exception.InnerException;
                    }
                    _logger.Error($"[FFXIV::ChatLogItem] {data}");
                }
            };
            host.ConstantsUpdated += (sender, e) => {
                try
                {
                    OnConstantsUpdated(e);
                }
                catch (Exception exception)
                {
                    string data = exception.ToString();
                    if (exception.InnerException != null)
                    {
                        data += "\r\nInner exception: " + exception.InnerException;
                    }
                    _logger.Error($"[FFXIV::Constants] {data}");
                }
            };
            host.CurrentPlayerUpdated += (sender, e) => {
                try {
                    OnCurrentPlayerUpdated(e);
                }
                catch (Exception exception) {
                    string data = exception.ToString();
                    if (exception.InnerException != null) {
                        data += "\r\nInner exception: " + exception.InnerException;
                    }

                    _logger.Error($"[FFXIV::CurrentPlayer] {data}");
                }
            };

            _inputSimulator = new InputSimulator();
        }

        private void OnCurrentPlayerUpdated(CurrentPlayerEvent e)
        {
            _currentCharacter.CharacterName = e.CurrentPlayer.Name;
        }

        private void OnConstantsUpdated(ConstantsEntityEvent e)
        {
            _currentCharacter.WorldName = e.ConstantsEntity.ServerName;
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
            private static readonly byte?[] ItemLinkStartPattern = { 0x02, 0x48 };
            private static readonly byte?[] ItemLinkEndPattern = { 0x01, 0x03, 0x02, 0x13 };

            private static readonly byte?[] BufferPrecedingItemNameLengthPattern = { 0x02, 0x48 };
            private static readonly byte?[] ItemNameEndPattern = { 0x02, 0x27, 0x07 };

            public struct ItemReplacement
            {
                public int StartIndex;
                public int EndIndex;
                private string _itemName;
                public string ItemName
                {
                    get => $"[[{_itemName}]]";
                    set => _itemName = value;
                }
            }

            private static List<ItemReplacement> GetReplacementsFromText(byte[] rawMessage)
            {
                List<ItemReplacement> result = new List<ItemReplacement>();

                var replacements = rawMessage.Locate(ItemLinkStartPattern);
                if (replacements.Length < 3)
                {
                    return result;
                }

                var itemLinkBufferStartPositions = replacements.Where((x, i) => i % 3 == 0);

                foreach (var itemLinkBufferStartPosition in itemLinkBufferStartPositions)
                {
                    var itemLinkBufferPreEnd = rawMessage.Skip(itemLinkBufferStartPosition).ToArray().Locate(ItemLinkEndPattern).Last() + ItemLinkEndPattern.Length;
                    var itemLinkBufferEnd = itemLinkBufferPreEnd + rawMessage.Skip(itemLinkBufferStartPosition + itemLinkBufferPreEnd).ToArray().Locate(new byte?[] { 0x03 }).Last() + 1;
                    var itemLinkBuffer = rawMessage.Skip(itemLinkBufferStartPosition).Take(itemLinkBufferEnd).ToArray();

                    var bufferPrecedingItemNameLength = itemLinkBuffer.Locate(BufferPrecedingItemNameLengthPattern).Last() + BufferPrecedingItemNameLengthPattern.Length;
                    var bufferPrecedingItemName = itemLinkBuffer[bufferPrecedingItemNameLength];
                    
                    var itemNameEndPosition = itemLinkBuffer.Locate(ItemNameEndPattern).Last();
                    var itemNameStartIndex = bufferPrecedingItemNameLength + 1 + bufferPrecedingItemName;
                    var itemName = itemLinkBuffer.Skip(itemNameStartIndex).Take(itemNameEndPosition - (itemNameStartIndex)).ToArray();

                    result.Add(new ItemReplacement
                    {
                        StartIndex = itemLinkBufferStartPosition,
                        EndIndex = itemLinkBufferStartPosition + itemLinkBufferEnd,
                        ItemName = Encoding.UTF8.GetString(itemName),
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
                    var mid = Encoding.UTF8.GetBytes(replacement.ItemName);
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
                    set => _rawPFEntry = value;
                }
            }

            private static List<PFReplacement> GetReplacementsFromText(byte[] rawMessage)
            {
                List<PFReplacement> result = new List<PFReplacement>();

                var itemLinkStartPositions = rawMessage.Locate(PFLinkStartPattern);

                foreach (var itemLinkStartPosition in itemLinkStartPositions)
                {
                    var itemNameEndPosition = rawMessage.Skip(itemLinkStartPosition).ToArray().Locate(PFNameEndPattern)[0];

                    var itemLinkBuffer = rawMessage.Skip(itemLinkStartPosition).Take(itemNameEndPosition).ToArray();
                    var itemNameStartPosition = itemLinkBuffer.Locate(PFNameStartPattern).Last() + PFNameStartPattern.Length;
                    var itemName = itemLinkBuffer.Skip(itemNameStartPosition).Take(itemNameEndPosition).ToArray();

                    var fullStop = rawMessage.Skip(itemLinkStartPosition + itemNameStartPosition).ToArray().Locate(PFLinkEndPattern)[0];

                    result.Add(new PFReplacement
                    {
                        StartIndex = itemLinkStartPosition,
                        EndIndex = itemLinkStartPosition + itemNameStartPosition + fullStop + PFLinkEndPattern.Length,
                        PFEntry = Encoding.UTF8.GetString(itemName),
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
                    var mid = Encoding.UTF8.GetBytes(replacement.PFEntry);
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
                CharacterName = characterName;
                WorldName = worldName;
            }
            public string CharacterName;
            public string WorldName;
            public override string ToString() => $"<{CharacterName}@{WorldName}>";
        }

        private void OnChatMessageReceived(object sender, ChatLogItemEvent e) {
            if (sender == null)
            {
                return;
            }

            if (e.ChatLogItem.Code != _chatChannel)
            {
                return;
            }

            if (e.ChatLogItem.Line.StartsWith(_currentCharacter.CharacterName) && !e.ChatLogItem.Line.Contains("FORCEEXEC"))
            {
                return;
            }

            _logger.Trace($"[CHATLOG]: '{BitConverter.ToString(e.ChatLogItem.Bytes)}'");

            byte[] utf8Message = ItemLinkReplacer.ReplaceItemReferences(e.ChatLogItem.Bytes);
            utf8Message = PFLinkReplacer.ReplaceItemReferences(utf8Message);

            try
            {
                var split = Split(utf8Message, 0x1F);

                Character logCharacter = null;
                string logMessage = null;

                // cross-world user require special treatment
                switch (split[1].Count(b => b == 0x03))
                {
                    case 3:
                        {
                            var crossWorldInfoSplit = Split(split[1], 0x03);
                            logCharacter = new Character(
                                Encoding.UTF8.GetString(crossWorldInfoSplit[1].TakeWhile(item => item != 0x02).ToArray()),
                                Encoding.UTF8.GetString(crossWorldInfoSplit[3])
                            );
                            logMessage = Encoding.UTF8.GetString(split[2]);
                        }
                        break;
                    case 2:
                        {
                            var crossWorldInfoSplit = Split(split[1], 0x03);
                            logCharacter = new Character(
                                Encoding.UTF8.GetString(crossWorldInfoSplit[1].TakeWhile(item => item != 0x02).ToArray()),
                                _currentCharacter.WorldName
                            );
                            logMessage = Encoding.UTF8.GetString(split[2]);
                        }
                        break;
                    case 0:
                        logCharacter = new Character(
                            Encoding.UTF8.GetString(split[1]),
                            _currentCharacter.WorldName
                        );
                        logMessage = Encoding.UTF8.GetString(split[2]);
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

                _discordHandler.Broadcast($"{logCharacter} {logMessage}").Wait();
            }
            catch (Exception ex) {
                string errorOutput = ex.ToString();
                if (ex.InnerException != null)
                {
                    errorOutput += "\r\n" + ex.InnerException;
                }
                _logger.Error(errorOutput);
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

                    line += $"{word} ";
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
                _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                await Task.Delay(300);
                _inputSimulator.Keyboard.Sleep(300);
                _inputSimulator.Keyboard.TextEntry($"[{authorName}]: {wrappedMessage}   ");
                await Task.Delay(300);
                _inputSimulator.Keyboard.Sleep(300);
                _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                await Task.Delay(500);
                _inputSimulator.Keyboard.Sleep(500);
            }
        }

        public Task MainAsync(IDiscord discordHandler)
        {
            _discordHandler = discordHandler;

            return Task.CompletedTask;
        }
    }
}
