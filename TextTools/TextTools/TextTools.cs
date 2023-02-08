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
                tools.SetValue("addbom", false, RegistryValueKind.DWord);
                tools.SetValue("eol", EnumEOL.Smart, RegistryValueKind.DWord);
                tools.SetValue("resetva", false, RegistryValueKind.DWord);
            }
        }

        public static bool RWS
        {
            get { return Convert.ToBoolean(tools.GetValue("rws", true)); }
            set { tools.SetValue("rws", value, RegistryValueKind.DWord); }
        }
        public static bool BOM
        {
            get { return Convert.ToBoolean(tools.GetValue("addbom", true)); }
            set { tools.SetValue("addbom", value, RegistryValueKind.DWord); }
        }
        public static EnumEOL EOL
        {
            get { return (EnumEOL)Convert.ToInt32(tools.GetValue("eol", true)); }
            set { tools.SetValue("eol", value, RegistryValueKind.DWord); }
        }
        public static string IgnorePatterns
        {
            get { return Convert.ToString(tools.GetValue("ignorePatterns", @".conf, .ini, .md, .txt, .log, \node_modules\")); }
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
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(OptionPageGrid), "TextTools", "Option", 0, 0, true)]
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

            try
            {
                var reader = new StreamReader(stream, new UTF8Encoding(false, true));
                text = reader.ReadToEnd();
            }
            catch(DecoderFallbackException)
            {
                stream.Position = 0;
                var reader = new StreamReader(stream, Encoding.Default, true);
                text = reader.ReadToEnd();
            }
            stream.Close();

            var encoding = new UTF8Encoding(Options.OptionBOM, false);
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
            [Category("TextTools")]
            [DisplayName("Convert end of line")]
            [Description("0: keep line ending. |1: convert to \\r\\n.|2: convert to \\n. |3: smart line ending(less changes)")]
            public Config.EnumEOL OptionEOL
            {
                get { return Config.EOL; }
                set { Config.EOL = value; }
            }

            [Category("TextTools")]
            [DisplayName("add BOM for utf8")]
            [Description("Whether add BOM to file head")]
            public bool OptionBOM
            {
                get { return Config.BOM; }
                set { Config.BOM = value; }
            }

            [Category("TextTools")]
            [DisplayName("Remove trailing white spaces")]
            [Description("Whether remove trailing white spaces")]
            public bool OptionRemoveTrailingWhiteSpace
            {
                get { return Config.RWS; }
                set { Config.RWS = value; }
            }

            [Category("TextTools")]
            [DisplayName("Reset vassistx")]
            [Description("Resets the trial period for Visual Assist X")]
            public bool OptionResetVA
            {
                get { return Config.Reset; }
                set { Config.Reset = value; }
            }

            [Category("TextTools")]
            [DisplayName("Ignore pattern")]
            [Description("A comma-separated list of strings. Any file containing one of the strings in the path will be ignored.")]
            [DefaultValue(@".conf, .ini, .md, .txt, .log, \node_modules\")]
            public string OptionIgnorePatterns
            {
                get { return Config.IgnorePatterns; }
                set { Config.IgnorePatterns = value; }
            }
        }
#endregion
    }

}