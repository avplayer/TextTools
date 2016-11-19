using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using System.Windows;
using System.Linq;

namespace TextTools
{

    internal class MultiEditTextFilter : IOleCommandTarget
    {
        private IWpfTextView textView;

        public bool Added { set; get; }

        internal class Tracking
        {
            public bool IsSelection { get; set; }
            public ITrackingPoint Start { get; set; }
            public ITrackingSpan Span { get; set; }
            public bool IsReversed { get; set; }

            public Tracking(ITrackingPoint start)
            {
                Start = start;
            }

            public Tracking(ITrackingSpan span, bool isReversed)
            {
                Span = span;
                IsSelection = true;
                IsReversed = isReversed;
            }

            public void ClearSelection(IWpfTextView textView)
            {
                Start = textView.TextSnapshot.CreateTrackingPoint(Span.GetStartPoint(textView.TextSnapshot), PointTrackingMode.Positive);
                IsSelection = false;
            }

            public void Apply(IWpfTextView textView)
            {
                if(IsSelection)
                {
                    if(IsReversed)
                    {
                        textView.Caret.MoveTo(Span.GetStartPoint(textView.TextSnapshot));
                    }
                    else
                    {
                        textView.Caret.MoveTo(Span.GetEndPoint(textView.TextSnapshot));
                    }
                    textView.Selection.Select(Span.GetSpan(textView.TextSnapshot), IsReversed);
                }
                else
                {
                    textView.Caret.MoveTo(Start.GetPoint(textView.TextSnapshot));
                }
            }
        }
        public IOleCommandTarget NextTarget { get; set; }

        private List<Tracking> points;
        private Dictionary<string, int> positionHash = new Dictionary<string, int>();
        private IAdornmentLayer adornmentLayer;
        private DTE2 dte;

        private CaretPosition lastCaretPosition = new CaretPosition();

        public MultiEditTextFilter(IWpfTextView textView)
        {
            this.textView = textView;
            this.points = new List<Tracking>();

            adornmentLayer = textView.GetAdornmentLayer("MultiEditLayer");

            dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
            lastCaretPosition = textView.Caret.Position;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    switch (prgCmds[i].cmdID)
                    {
                        case ((uint)VSConstants.VSStd2KCmdID.TYPECHAR):
                        case ((uint)VSConstants.VSStd2KCmdID.BACKSPACE):
                        case ((uint)VSConstants.VSStd2KCmdID.TAB):
                        case ((uint)VSConstants.VSStd2KCmdID.LEFT):
                        case ((uint)VSConstants.VSStd2KCmdID.RIGHT):
                        case ((uint)VSConstants.VSStd2KCmdID.LEFT_EXT):
                        case ((uint)VSConstants.VSStd2KCmdID.RIGHT_EXT):
                        case ((uint)VSConstants.VSStd2KCmdID.UP_EXT):
                        case ((uint)VSConstants.VSStd2KCmdID.DOWN_EXT):
                        case ((uint)VSConstants.VSStd2KCmdID.UP):
                        case ((uint)VSConstants.VSStd2KCmdID.DOWN):
                        case ((uint)VSConstants.VSStd2KCmdID.END):
                        case ((uint)VSConstants.VSStd2KCmdID.HOME):
                        case ((uint)VSConstants.VSStd2KCmdID.PAGEDN):
                        case ((uint)VSConstants.VSStd2KCmdID.PAGEUP):
                        case ((uint)VSConstants.VSStd2KCmdID.PASTE):
                        case ((uint)VSConstants.VSStd2KCmdID.PASTEASHTML):
                        case ((uint)VSConstants.VSStd2KCmdID.BOL):
                        case ((uint)VSConstants.VSStd2KCmdID.EOL):
                        case ((uint)VSConstants.VSStd2KCmdID.RETURN):
                        case ((uint)VSConstants.VSStd2KCmdID.BACKTAB):
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }
            }

            return NextTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            Debug.WriteLine(String.Format("{0}, {1}", pguidCmdGroup.ToString(), nCmdID));
            if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                switch (nCmdID)
                {
                    case ((uint)VSConstants.VSStd2KCmdID.TYPECHAR):
                    case ((uint)VSConstants.VSStd2KCmdID.BACKSPACE):
                    case ((uint)VSConstants.VSStd2KCmdID.TAB):
                    case ((uint)VSConstants.VSStd2KCmdID.LEFT):
                    case ((uint)VSConstants.VSStd2KCmdID.RIGHT):
                    case ((uint)VSConstants.VSStd2KCmdID.UP):
                    case ((uint)VSConstants.VSStd2KCmdID.DOWN):
                    case ((uint)VSConstants.VSStd2KCmdID.END):
                    case ((uint)VSConstants.VSStd2KCmdID.HOME):
                    case ((uint)VSConstants.VSStd2KCmdID.PAGEDN):
                    case ((uint)VSConstants.VSStd2KCmdID.PAGEUP):
                    case ((uint)VSConstants.VSStd2KCmdID.PASTEASHTML):
                    case ((uint)VSConstants.VSStd2KCmdID.BOL):
                    case ((uint)VSConstants.VSStd2KCmdID.EOL):
                    case ((uint)VSConstants.VSStd2KCmdID.RETURN):
                    case ((uint)VSConstants.VSStd2KCmdID.BACKTAB):
                    case ((uint)VSConstants.VSStd2KCmdID.LEFT_EXT):
                    case ((uint)VSConstants.VSStd2KCmdID.RIGHT_EXT):
                    case ((uint)VSConstants.VSStd2KCmdID.UP_EXT):
                    case ((uint)VSConstants.VSStd2KCmdID.DOWN_EXT):
                        if (points.Count > 0)
                        {
                            return SyncedOperation(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        }
                        break;
                    case ((uint)VSConstants.VSStd2KCmdID.PASTE):
                        return Paste(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                    default:
                        break;
                }
                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL)
                {
                    points.Clear();
                    RedrawScreen();
                }
            }
            else if (pguidCmdGroup == typeof(VSConstants.VSStd97CmdID).GUID)
            {
                switch((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.Paste:
                        if (points.Count > 0)
                        {
                            return Paste(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        }
                        break;
                    default:
                        break;
                }
            }


            return NextTarget.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private int Paste(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (Clipboard.ContainsText() && points.Any())
            {
                var text = Clipboard.GetText();
                var lines = text.Replace("\r", "").Split('\n');

                if(lines.Length == points.Count)
                {
                    ITextCaret caret = textView.Caret;
                    var tmpPoints = points;
                    points = new List<Tracking>();

                    dte.UndoContext.Open("Multiedit");
                    var i = 0;
                    foreach (var tracking in tmpPoints)
                    {
                        Clipboard.SetText(lines[i]);
                        tracking.Apply(textView);
                        NextTarget.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        AddSyncPoint(caret.Position);
                        i++;
                    }
                    Clipboard.SetText(text);

                    dte.UndoContext.Close();
                    RedrawScreen();
                    return VSConstants.S_OK;
                }
                else
                {
                    return SyncedOperation(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }
            }
            else
            {
                return SyncedOperation(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
        }

        private int SyncedOperation(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            Debug.WriteLine("SyncedOperation");

            ITextCaret caret = textView.Caret;
            var tmpPoints = points;
            points = new List<Tracking>();

            dte.UndoContext.Open("Multiedit");

            try
            {

                foreach (var track in tmpPoints)
                {
                    track.Apply(textView);
                    NextTarget.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                    if (!textView.Selection.IsEmpty)
                    {
                        AddSyncPoint(textView.Selection.Start.Position, textView.Selection.End.Position, textView.Selection.IsReversed);
                        textView.Selection.Clear();
                    }
                    else
                    {
                        AddSyncPoint(caret.Position);
                    }
                }
            }
            finally
            {
                dte.UndoContext.Close();
                RedrawScreen();
            }

            return VSConstants.S_OK;
        }

        internal void HandleClick(bool addCursor)
        {
            if(addCursor)
            {
                if (textView.Selection.SelectedSpans.Count == 1)
                {
                    if (points.Count == 0)
                        AddSyncPoint(lastCaretPosition);

                    AddSyncPoint(textView.Caret.Position);
                }
                else
                {
                    foreach (var span in textView.Selection.SelectedSpans)
                    {
                        if (span.Length == 0)
                        {
                            AddSyncPoint(span.Start.Position);
                        }
                        else
                        { 
                            AddSyncPoint(span.Start.Position, span.End.Position, textView.Selection.IsReversed);
                        }
                    }

                    textView.Selection.Clear();
                }
                RedrawScreen();
            }
            else if(points.Any())
            {
                points.Clear();
                RedrawScreen();
            }
            
            lastCaretPosition = textView.Caret.Position;
        }

        private void AddSyncPoint(int start, int end, bool IsReversed)
        {
            Debug.WriteLine("AddSyncPoint");
            var span = textView.TextSnapshot.CreateTrackingSpan(start, end - start, SpanTrackingMode.EdgePositive);
            points.Add(new Tracking(span, IsReversed));
        }
        private void AddSyncPoint(CaretPosition position)
        {
            AddSyncPoint(position.BufferPosition.Position);
        }

        private void AddSyncPoint(int position)
        {
            Debug.WriteLine("AddSyncPoint");
            var curTrackingPoint = textView.TextSnapshot.CreateTrackingPoint(position, PointTrackingMode.Positive);
            points.Add(new Tracking(curTrackingPoint));
            RedrawScreen();
        }


        private void RedrawScreen()
        {
            adornmentLayer.RemoveAllAdornments();
            positionHash.Clear();
            List<Tracking> newTrackList = new List<Tracking>();
            foreach (var track in points)
            {
                int curPosition;
                if(!track.IsSelection)
                {
                    curPosition = track.Start.GetPosition(textView.TextSnapshot);
                }
                else
                {
                    curPosition = track.Span.GetStartPoint(textView.TextSnapshot).Position;
                }
                IncrementCount(positionHash, curPosition.ToString());
                if (positionHash[curPosition.ToString()] > 1)
                    continue;
                DrawSingleSyncPoint(track);
                newTrackList.Add(track);
            }

            points = newTrackList;

        }

        private void IncrementCount(Dictionary<string, int> someDictionary, string id)
        {
            if (!someDictionary.ContainsKey(id))
                someDictionary[id] = 0;

            someDictionary[id]++;
        }


        private void DrawSingleSyncPoint(Tracking track)
        {
            bool isSelection = false;
            SnapshotSpan span;
            if (track.IsSelection)
            {
                isSelection = true;
                span = track.Span.GetSpan(textView.TextSnapshot);
            }
            else
            {
                if (track.Start.GetPosition(textView.TextSnapshot) >= textView.TextSnapshot.Length)
                    return;
                span = new SnapshotSpan(track.Start.GetPoint(textView.TextSnapshot), 1);
            }
            
            
            Brush brush = Brushes.LightBlue.Clone();
            brush.Opacity = 0.3;
            var geom = textView.TextViewLines.GetLineMarkerGeometry(span);
            GeometryDrawing drawing = new GeometryDrawing(brush, null, geom);

            if (drawing.Bounds.IsEmpty)
                return;

            Rectangle rect = new Rectangle()
            {
                Fill = brush,
                //Width = drawing.Bounds.Width / 6,
                Width = isSelection ? geom.Bounds.Width : drawing.Bounds.Width / 6,
                Height = drawing.Bounds.Height - 4,
                Margin = new System.Windows.Thickness(0, 2, 0, 0),
            };

            var clip = geom.Clone();
            clip.Transform = new TranslateTransform(-geom.Bounds.Left, -geom.Bounds.Top);
            rect.Clip = clip;
            Canvas.SetLeft(rect, geom.Bounds.Left);
            Canvas.SetTop(rect, geom.Bounds.Top);
            adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, "MultiEditLayer", rect, null);

        }
    }
}
