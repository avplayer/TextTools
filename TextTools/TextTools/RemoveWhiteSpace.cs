using Microsoft.VisualStudio.Text;
using System;

namespace TextTools
{
    class RemoveWhiteSpace
    {
        // String literal and c++ raw string state machine.
        public enum StringliteralState
        {
            Nono,
            Quote,
        };

        public enum RawStringState
        {
            Nono,
            Prefix,
            RawString,
            Suffix,
        };

        public static void RemoveTrailingWhitespace(ITextBuffer buffer)
        {
            if (Config.RWS)
            {
                using (var edit = buffer.CreateEdit())
                {
                    var snap = edit.Snapshot;

                    StringliteralState stringState = StringliteralState.Nono;
                    RawStringState rawString = RawStringState.Nono;

                    string prefix = "";
                    string suffix = "";

                    char backChar = '\0';
                    int spaceStart = 0;

                    foreach (var line in snap.Lines)
                    {
                        string text = line.GetText();
                        foreach (char c in text)
                        {
                            if (rawString != RawStringState.Nono)
                            {
                                switch (rawString)
                                {
                                    case RawStringState.Prefix:
                                        if (c == '(')
                                        {
                                            rawString = RawStringState.RawString;
                                            continue;
                                        }
                                        else
                                        {
                                            prefix = prefix + c;
                                            continue;
                                        }
                                    case RawStringState.RawString:
                                        if (c != ')')
                                            continue;
                                        else
                                            rawString = RawStringState.Suffix;
                                        continue;
                                    case RawStringState.Suffix:
                                        if (c != '\"')
                                            suffix = suffix + c;
                                        else if (suffix == prefix)
                                        {
                                            rawString = RawStringState.Nono;
                                            prefix = suffix = "";
                                            spaceStart = 0;
                                        }
                                        else
                                        {
                                            suffix = "";
                                            rawString = RawStringState.RawString;
                                        }
                                        continue;
                                }
                            }

                            // Skip all \" substring
                            if (backChar == '\\' && c == '\"')
                            {
                                backChar = c;
                                if (spaceStart != 0)
                                    spaceStart = 0;
                                continue;
                            }
                            // Skip \\ substring
                            else if (backChar == '\\' && c == '\\')
                            {
                                backChar = ' ';
                                if (spaceStart != 0)
                                    spaceStart = 0;
                                continue;
                            }

                            // C++ raw string literal
                            if (backChar == 'R' && c == '\"')
                            {
                                rawString = RawStringState.Prefix;
                                continue;
                            }

                            backChar = c;

                            // String literal
                            switch (stringState)
                            {
                                case StringliteralState.Nono:
                                    if (c == '\"')
                                    {
                                        stringState = StringliteralState.Quote;
                                        if (spaceStart != 0)
                                            spaceStart = 0;
                                        continue;
                                    }
                                    break;
                                case StringliteralState.Quote:
                                    if (c == '\"')
                                        stringState = StringliteralState.Nono;
                                    continue;
                            }

                            if (Char.IsWhiteSpace(c))
                            {
                                spaceStart++;
                                continue;
                            }
                            else
                            {
                                spaceStart = 0;
                                continue;
                            }
                        }

                        if (spaceStart != 0)
                        {
                            int start = line.Start.Position + text.Length - spaceStart;
                            edit.Delete(start, spaceStart);
                        }

                        spaceStart = 0;
                    }

                    edit.Apply();
                }
            }
        }
    }
}
