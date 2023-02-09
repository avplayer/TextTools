using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using EnvDTE;
using EnvDTE80;
using System.IO;
using System.Text;
using System.ComponentModel;
using Microsoft;
using System.Collections.Generic;

namespace TextTools
{
    public static class Config
    {
        public enum EnumEOL
        {
            Keep,
            CRLF,
            LF,
            Smart,
        }

        public enum EnumUTF8
        {
            Keep,
            UTF8,
            UTF8BOM
        }

        private static RegistryKey tools;

        static Config()
        {
            RegistryKey key = Registry.CurrentUser;
            bool exist = false;

            try
            {
                tools = key.OpenSubKey("software\\TextTools", true);
                if (tools != null)
                    exist = true;
                else
                    tools = key.CreateSubKey("software\\TextTools");
            }
            catch (ObjectDisposedException)
            {
                tools = key.CreateSubKey("software\\TextTools");
            }

            if (!exist)
            {
                tools.SetValue("rws", true, RegistryValueKind.DWord);
                tools.SetValue("utf8", false, RegistryValueKind.DWord);
                tools.SetValue("eol", EnumEOL.Smart, RegistryValueKind.DWord);
                tools.SetValue("resetva", false, RegistryValueKind.DWord);
            }
        }

        public static bool RWS
        {
            get { return Convert.ToBoolean(tools.GetValue("rws", true)); }
            set { tools.SetValue("rws", value, RegistryValueKind.DWord); }
        }
        public static EnumUTF8 Utf8Encoding
        {
            get { return (EnumUTF8)(tools.GetValue("utf8", EnumUTF8.UTF8)); }
            set { tools.SetValue("utf8", value, RegistryValueKind.DWord); }
        }
        public static EnumEOL EOL
        {
            get { return (EnumEOL)Convert.ToInt32(tools.GetValue("eol", true)); }
            set { tools.SetValue("eol", value, RegistryValueKind.DWord); }
        }
        public static string IgnorePatterns
        {
            get { return Convert.ToString(tools.GetValue("ignorePatterns", @".conf, .ini, .md, .txt, .log, .bat, \node_modules\")); }
            set { tools.SetValue("ignorePatterns", value, RegistryValueKind.String); }
        }
        public static bool Reset
        {
            get { return Convert.ToBoolean(tools.GetValue("resetva", false)); }
            set { tools.SetValue("resetva", value, RegistryValueKind.DWord); }
        }

        public static IEnumerable<string> GetIgnorePatterns()
        {
            var raw = IgnorePatterns.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string pattern in raw)
                yield return pattern.Trim();
        }

    }

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(UIContextGuids.DesignMode, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(OptionPageGrid), "文本工具", "选项", 0, 0, true)]
    [Guid(TextTools.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class TextTools : AsyncPackage
    {
        public const string PackageGuidString = "624A1C84-1E89-4FC9-8863-4FF2242FFB2B";

        #region Package Members

        private static OptionPageGrid Options { get; set; }
        private DocumentEvents documentEvents;
        public TextTools()
        {}

        protected override async System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            var dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            Assumes.Present(dte);

            documentEvents = dte.Events.DocumentEvents;
            documentEvents.DocumentSaved += OnDocumentSaved;
        }

        void OnDocumentSaved(Document doc)
        {
            if (Options == null)
            {
                Options = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));

                if (Options.OptionResetVA)
                {
                    string tmpfile = Path.GetTempPath() + "1489AFE4.TMP";
                    if (File.Exists(tmpfile))
                        File.Delete(tmpfile);

                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE", true))
                    {
                        if (key != null)
                            key.DeleteSubKeyTree("Licenses");
                    }
                }
            }

            if (doc.Kind != "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}")
                return;

            var path = doc.FullName;

            if (!FileHelpers.IsFileSupported(path))
                return;

            var stream = new FileStream(path, FileMode.Open);

            string text;
            stream.Position = 0;
            Encoding currentEncoding;

            try
            {
                var reader = new StreamReader(stream, new UTF8Encoding(false, true));
                text = reader.ReadToEnd();
                currentEncoding = reader.CurrentEncoding;
            }
            catch (DecoderFallbackException)
            {
                stream.Position = 0;
                var reader = new StreamReader(stream, Encoding.Default, true);
                text = reader.ReadToEnd();
                currentEncoding = reader.CurrentEncoding;
            }
            stream.Close();

            switch (Options.OptionEOL)
            {
                case Config.EnumEOL.CRLF:
                    text = ConvertToCRLF(text);
                    break;
                case Config.EnumEOL.LF:
                    text = ConvertToLF(text);
                    break;
                case Config.EnumEOL.Smart:
                    var crln = text.Length - text.Replace("\r\n", "\n").Length;
                    var ln = text.Split('\n').Length - 1 - crln;

                    if (crln > ln)
                        text = ConvertToCRLF(text);
                    else
                        text = ConvertToLF(text);

                    break;
                default:
                    break;
            }

            stream = File.Open(path, FileMode.Truncate | FileMode.OpenOrCreate);
            var writer = new BinaryWriter(stream);

            if (Options.OptionUTF8 == Config.EnumUTF8.Keep)
            {
                writer.Write(currentEncoding.GetPreamble());
                writer.Write(currentEncoding.GetBytes(text));
                writer.Close();
                return;
            }

            bool bom = Options.OptionUTF8 == Config.EnumUTF8.UTF8BOM;
            UTF8Encoding encoding = new UTF8Encoding(bom, false);

            writer.Write(encoding.GetPreamble());
            writer.Write(encoding.GetBytes(text));

            writer.Close();
        }

        private static string ConvertToLF(string text)
        {
            text = text.Replace("\r\n", "\n");
            return text;
        }

        private static string ConvertToCRLF(string text)
        {
            text = text.Replace("\r\n", "\n");
            text = text.Replace("\n", "\r\n");
            return text;
        }

        public class OptionPageGrid : DialogPage
        {
            [Category("文本工具")]
            [DisplayName("转换换行符")]
            [Description(@"Smart 表示自动转换(统一为文本中最多的换行符)")]
            public Config.EnumEOL OptionEOL
            {
                get { return Config.EOL; }
                set { Config.EOL = value; }
            }

            [Category("文本工具")]
            [DisplayName("自动转换为UTF8编码")]
            [Description(@"将当前源文件自动转换为UTF8编码")]
            public Config.EnumUTF8 OptionUTF8
            {
                get { return Config.Utf8Encoding; }
                set { Config.Utf8Encoding = value; }
            }

            [Category("文本工具")]
            [DisplayName("删除行尾空白")]
            [Description("是否开启删除行尾空白")]
            public bool OptionRemoveTrailingWhiteSpace
            {
                get { return Config.RWS; }
                set { Config.RWS = value; }
            }

            [Category("文本工具")]
            [DisplayName("重置VAX插件试用")]
            [Description("自动重置VAX插件试用，推荐购买正版VAX")]
            public bool OptionResetVA
            {
                get { return Config.Reset; }
                set { Config.Reset = value; }
            }

            [Category("文本工具")]
            [DisplayName("忽略文件")]
            [Description("忽略列表, 以逗号分隔, 匹配规则为完整文件名中包含列表中某一项将被忽略")]
            [DefaultValue(@".conf, .ini, .md, .txt, .log, .bat, \node_modules\")]
            public string OptionIgnorePatterns
            {
                get { return Config.IgnorePatterns; }
                set { Config.IgnorePatterns = value; }
            }
        }
#endregion
    }

}