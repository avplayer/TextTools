using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Text;

namespace TextTools
{
    class QuoteItCommand : IOleCommandTarget
    {
        private DTE2 dte;
        private IOleCommandTarget NextCommandTarget;
        private IWpfTextView textView;
        private IVsTextView textViewAdapter;

        private Dictionary<char, char> items = new Dictionary<char, char>{ { '"','"' }, { '(', ')' }, { '{', '}' }, { '[', ']' } };

        public QuoteItCommand(IVsTextView textViewAdapter, IWpfTextView textView, DTE2 dte)
        {
            this.textViewAdapter = textViewAdapter;
            this.textView = textView;
            this.dte = dte;
            textViewAdapter.AddCommandFilter(this, out NextCommandTarget);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if(pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
            {
                var typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                if(items.ContainsKey(typedChar))
                {
                    var selection = textView.Selection;
                    if(!selection.IsEmpty)
                    {
                        var edit = textView.TextBuffer.CreateEdit();
                        edit.Insert(textView.Selection.Start.Position, typedChar.ToString());
                        edit.Insert(textView.Selection.End.Position, items[typedChar].ToString());
                        edit.Apply();
                        var snap = textView.TextSnapshot;
                        textView.Selection.Select(textView.Selection.Start, new VirtualSnapshotPoint(snap, textView.Selection.End.Position - 1));
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
