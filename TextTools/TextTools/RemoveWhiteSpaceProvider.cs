using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Shell;
using EnvDTE80;
using EnvDTE;

namespace TextTools
{
    [Export(typeof(IVsTextViewCreationListener))]
    [Export(typeof(IWpfTextViewConnectionListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class RemoveWhiteSpaceProvider : IVsTextViewCreationListener, IWpfTextViewConnectionListener
    {
        [Import]
        public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }

        [Import]
        public SVsServiceProvider serviceProvider { get; set; }

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public async void SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
        {
            foreach (ITextBuffer buffer in subjectBuffers)
            {
                if (buffer.Properties.TryGetProperty(typeof(TrailingClassifier), out TrailingClassifier classifier))
                    await classifier.SetTextViewAsync(textView);
            }
        }

        public void SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
        {
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            DTE2 dte = serviceProvider.GetService(typeof(DTE)) as DTE2;
            IWpfTextView textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);

            textView.Properties.GetOrCreateSingletonProperty(() => new RemoveWhitespaceCommand(textViewAdapter, textView, dte));

            ITextDocument doc;

            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out doc))
                textView.Properties.GetOrCreateSingletonProperty(() => new RemoveWhitespaceOnSave(textViewAdapter, textView, dte, doc));
        }
    }
}
