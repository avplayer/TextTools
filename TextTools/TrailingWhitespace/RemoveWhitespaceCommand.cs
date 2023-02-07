using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace TextTools
{
    internal class RemoveWhitespaceCommand : RemoveWhiteSpace, IOleCommandTarget
    {
        private DTE2 dte;
        private IWpfTextView textView;
        private IVsTextView textViewAdapter;

        public IOleCommandTarget NextCommandTarget;

        public RemoveWhitespaceCommand(IVsTextView textViewAdapter, IWpfTextView textView, DTE2 dte)
        {
            this.textViewAdapter = textViewAdapter;
            this.textView = textView;
            this.dte = dte;
            textViewAdapter.AddCommandFilter(this, out NextCommandTarget);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, Microsoft.VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText)
        {
            return NextCommandTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == new Guid("1496A755-94DE-11D0-8C3F-00C04FC2AAE2") && nCmdID == (uint)VSConstants.VSStd2KCmdID.DELETEWHITESPACE)
            {
                ITextBuffer buffer = textView.TextBuffer;

                if (buffer.CheckEditAccess())
                {
                    RemoveTrailingWhitespace(buffer);
                    return VSConstants.S_OK;
                }
            }
            return NextCommandTarget.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

    }
}