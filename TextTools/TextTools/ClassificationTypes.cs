using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace TextTools
{
    static class TrailingClassificationTypes
    {
        public const string Whitespace = "TextTools";

        [Export, Name(TrailingClassificationTypes.Whitespace)]
        public static ClassificationTypeDefinition TextTools { get; set; }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = TrailingClassificationTypes.Whitespace)]
    [Name(TrailingClassificationTypes.Whitespace)]
    [Order(After = Priority.Default)]
    [UserVisible(true)]
    sealed class TextToolsFormatDefinition : ClassificationFormatDefinition
    {
        public static bool IsChineseSimple()
        {
            return System.Threading.Thread.CurrentThread.CurrentCulture.Name == "zh-CN";
        }

        public TextToolsFormatDefinition()
        {
            BackgroundColor = Color.FromRgb(255, 145, 145);
            if (IsChineseSimple())
                DisplayName = "行尾空白";
            else
                DisplayName = "Trailing Whitespace";
        }
    }
}