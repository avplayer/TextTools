using System;
using EnvDTE80;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Linq;

namespace TextTools
{
    internal class RemoveWhitespaceOnSave : RemoveWhiteSpace, IOleCommandTarget
    {
        private ITextDocument doc;
        private DTE2 dte;
        private IOleCommandTarget NextCommandTarget;
        private IWpfTextView textView;
        private IVsTextView textViewAdapter;

        public RemoveWhitespaceOnSave(IVsTextView textViewAdapter, IWpfTextView textView, DTE2 dte, ITextDocument doc)
        {
            this.textViewAdapter = textViewAdapter;
            this.textView = textView;
            this.dte = dte;
            this.doc = doc;

            textViewAdapter.AddCommandFilter(this, out NextCommandTarget);
        }

        private static uint[] _cmds = new uint[] { (uint)VSConstants.VSStd97CmdID.SaveProjectItem, (uint)VSConstants.VSStd97CmdID.SaveSolution };
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == typeof(VSConstants.VSStd97CmdID).GUID && _cmds.Contains(nCmdID))
            {
                ITextBuffer buffer = textView.TextBuffer;

                if (buffer != null && buffer.CheckEditAccess())
                {
                    if (FileHelpers.IsFileSupported(buffer))
                        RemoveTrailingWhitespace(buffer);
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