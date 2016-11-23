using System;
using EnvDTE80;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace TextTools
{
    internal class QuoteItCommand : IOleCommandTarget
    {
        private DTE2 dte;
        private IOleCommandTarget NextCommandTarget;
        private IWpfTextView textView;
        private IVsTextView textViewAdapter;

        private static Dictionary<char, char> chars = new Dictionary<char, char> {
            { '\'', '\'' },
            { '"', '"' },
            { '{', '}' },
            { '(', ')' },
            { '[', ']' },
            { '`', '`' },
        };

        public QuoteItCommand(IVsTextView textViewAdapter, IWpfTextView textView, DTE2 dte)
        {
            this.textViewAdapter = textViewAdapter;
            this.textView = textView;
            this.dte = dte;
            textViewAdapter.AddCommandFilter(this, out NextCommandTarget);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if(pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                if(nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
                {
                    var ch = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                    if(chars.ContainsKey(ch) && !textView.Selection.IsEmpty)
                    {
                        var edit = textView.TextBuffer.CreateEdit();
                        var sel = textView.Selection;
                        edit.Insert(sel.Start.Position, ch.ToString());
                        edit.Insert(sel.End.Position, chars[ch].ToString());
                        edit.Apply();
                        sel.Select(sel.Start, new VirtualSnapshotPoint(textView.TextSnapshot, sel.End.Position.Position - 1));
                        return VSConstants.S_OK;
                    }
                }
            }
            return NextCommandTarget.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            return NextCommandTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}