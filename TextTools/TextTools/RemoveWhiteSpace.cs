using Microsoft.VisualStudio.Text;
using System;
using System.IO;

namespace TextTools
{
    class RemoveWhiteSpace
    {
        // String literal and c++ raw string state machine.
        public enum StringliteralState
        {
            None,
            Quote,
        };

        public enum RawStringState
        {
            None,
            Prefix,
            RawString,
            Suffix,
        };

        public static void RemoveTrailingWhitespace(ITextBuffer buffer)
        {
            if (Config.RWS)
            {
                string fileName = buffer.GetFilePath();
                if (string.IsNullOrWhiteSpace(fileName) || !Path.IsPathRooted(fileName))
                    return;

                bool csharp = Path.GetExtension(fileName).Equals(".cs", StringComparison.OrdinalIgnoreCase);

                using (var edit = buffer.CreateEdit())
                {
                    var snap = edit.Snapshot;

                    StringliteralState stringState = StringliteralState.None;
                    RawStringState rawState = RawStringState.None;

                    string prefix = "";
                    string suffix = "";

                    const char zeroChar = '\0';
                    char backChar = zeroChar;
                    int numSpace = 0;
                    bool verbatimString = false;
                    bool comment = false;
                    bool multilineComment = false;

                    foreach (var line in snap.Lines)
                    {
                        string text = line.GetText();
                        comment = false;

                        foreach (char c in text)
                        {
                            switch (rawState)
                            {
                                case RawStringState.None:
                                    {
                                        if (comment)
                                        {
                                            continue;
                                        }

                                        if (verbatimString)
                                        {
                                            if (c == '\"')
                                            {
                                                backChar = zeroChar;
                                                numSpace = 0;
                                                verbatimString = false;
                                            }

                                            continue;
                                        }

                                        // Skip all \" substring
                                        if (backChar == '\\' && c == '\"')
                                        {
                                            backChar = c;
                                            if (numSpace != 0)
                                                numSpace = 0;
                                            continue;
                                        }
                                        // Skip \\ substring
                                        else if (backChar == '\\' && c == '\\')
                                        {
                                            backChar = zeroChar;
                                            if (numSpace != 0)
                                                numSpace = 0;
                                            continue;
                                        }
                                        // Skip comments
                                        else if (backChar == '/' && c == '*')
                                        {
                                            if (stringState == StringliteralState.None)
                                                multilineComment = true;
                                        }
                                        else if (backChar == '*' && c == '/')
                                        {
                                            if (multilineComment)
                                            {
                                                multilineComment = false;
                                                continue;
                                            }
                                        }

                                        if (multilineComment)
                                        {
                                            backChar = c;
                                            continue;
                                        }

                                        if (backChar == '/' && c == '/')
                                        {
                                            comment = true;
                                        }

                                        // C++ raw string literal
                                        if (backChar == 'R' && c == '\"')
                                        {
                                            rawState = RawStringState.Prefix;
                                            continue;
                                        }

                                        // C# verbatim string
                                        if (csharp && backChar == '@' && c == '\"')
                                        {
                                            verbatimString = true;
                                            continue;
                                        }

                                        backChar = c;

                                        // String literal
                                        switch (stringState)
                                        {
                                            case StringliteralState.None:
                                                if (c == '\"')
                                                {
                                                    stringState = StringliteralState.Quote;
                                                    if (numSpace != 0)
                                                        numSpace = 0;
                                                    continue;
                                                }
                                                break;
                                            case StringliteralState.Quote:
                                                if (c == '\"')
                                                    stringState = StringliteralState.None;
                                                continue;
                                        }

                                        if (Char.IsWhiteSpace(c))
                                            numSpace++;
                                        else
                                            numSpace = 0;
                                    }
                                    break;
                                case RawStringState.Prefix:
                                    if (c == '(')
                                        rawState = RawStringState.RawString;
                                    else
                                        prefix = prefix + c;
                                    continue;
                                case RawStringState.RawString:
                                    if (c == ')')
                                        rawState = RawStringState.Suffix;
                                    continue;
                                case RawStringState.Suffix:
                                    if (c != '\"')
                                        suffix = suffix + c;
                                    else if (suffix == prefix)
                                    {
                                        rawState = RawStringState.None;
                                        prefix = suffix = "";
                                        numSpace = 0;
                                    }
                                    else
                                    {
                                        suffix = "";
                                        rawState = RawStringState.RawString;
                                    }
                                    continue;
                            }
                        }

                        if (numSpace != 0)
                        {
                            int start = line.Start.Position + text.Length - numSpace;
                            edit.Delete(start, numSpace);
                        }

                        numSpace = 0;
                    }

                    edit.Apply();
                }
            }
        }
    }
}
