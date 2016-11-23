using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace TextTools
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class MultiEditTextProvider : IVsTextViewCreationListener
    {
        [Import(typeof(IVsEditorAdaptersFactoryService))]
        internal IVsEditorAdaptersFactoryService editorFactory = null;

        [Export(typeof(AdornmentLayerDefinition))]
        [Name("MultiEditLayer")]
        [TextViewRole(PredefinedTextViewRoles.Editable)]
        internal AdornmentLayerDefinition multiEditAdornmentLayer = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView textView = editorFactory.GetWpfTextView(textViewAdapter);

            if (textView != null)
                AddCommandFilter(textViewAdapter, textView, new MultiEditTextFilter(textView));
        }

        private void AddCommandFilter(IVsTextView textViewAdapter, IWpfTextView textView, MultiEditTextFilter commandFilter)
        {
            IOleCommandTarget next;
            int result = textViewAdapter.AddCommandFilter(commandFilter, out next);
            if(result == VSConstants.S_OK)
            {
                commandFilter.Added = true;
                textView.Properties.AddProperty(typeof(MultiEditTextFilter), commandFilter);

                if (next != null)
                {
                    commandFilter.NextTarget = next;
                }
            }
        }
        
    }
}
