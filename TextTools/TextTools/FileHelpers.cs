using System;
using System.IO;
using System.Linq;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace TextTools
{
    internal static class FileHelpers
    {
        private const string _propKey = "TrailingEnabled";

        public static bool IsFileSupported(string fileName)
        {
            System.Collections.Generic.IEnumerable<string> patterns = Config.GetIgnorePatterns();
            if (patterns.Any(p => fileName.IndexOf(p, StringComparison.OrdinalIgnoreCase) > -1))
                return false;
            return true;
        }

        public static bool IsFileSupported(ITextBuffer buffer)
        {
            try
            {
                if (buffer == null || buffer.Properties == null)
                    return false;

                // 不要总是查询.
                if (buffer.Properties.TryGetProperty(_propKey, out bool isEnabled))
                    return isEnabled;

                string fileName = buffer.GetFilePath();

                // 检查是否为真实存在的文件.
                if (string.IsNullOrWhiteSpace(fileName) || !Path.IsPathRooted(fileName))
                    return PersistantReturnValue(buffer, false);

                // 检查文件是否支持.
                if (!IsFileSupported(fileName))
                    return PersistantReturnValue(buffer, false);

                return PersistantReturnValue(buffer, true);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool PersistantReturnValue(ITextBuffer buffer, bool value)
        {
            if (!buffer.Properties.ContainsProperty(_propKey))
                buffer.Properties.AddProperty(_propKey, value);

            return value;
        }

        public static string GetFilePath(this ITextBuffer buffer)
        {
            if (!buffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer bufferAdapter))
                return null;

            if (bufferAdapter == null)
                return null;

            var persistFileFormat = bufferAdapter as IPersistFileFormat;

            if (persistFileFormat == null)
                return null;

            string ppzsFilename = null;

            try
            {
                persistFileFormat.GetCurFile(out ppzsFilename, out uint iii);
                return ppzsFilename;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
                return null;
            }
        }
    }
}
