using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextTools
{
    class RemoveWhiteSpace
    {
        public static void RemoveTrailingWhitespace(ITextBuffer buffer)
        {
            if (Config.RWS)
            {
                using (var edit = buffer.CreateEdit())
                {
                    var snap = edit.Snapshot;
                    var isVerbatimString = false;
                    foreach (var line in snap.Lines)
                    {
                        string text = line.GetText();
                        if (text.Contains("@\"") && text.Count(f => f == '"') == 1)
                            isVerbatimString = true;
                        else if (isVerbatimString && text.Contains("\""))
                            isVerbatimString = false;

                        if (!isVerbatimString)
                        {
                            int length = text.Length;
                            while (--length >= 0 && Char.IsWhiteSpace(text[length])) ;
                            if (length < text.Length - 1)
                            {
                                int start = line.Start.Position;
                                edit.Delete(start + length + 1, text.Length - length - 1);
                            }
                        }
                    }
                    edit.Apply();
                }
            }
        }
    }
}
