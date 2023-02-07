//------------------------------------------------------------------------------
// <copyright file="VSPackage1.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

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

namespace TextTools
{
    public static class Config
    {
        public enum EnumCRLF
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
                tools.SetValue("crlf", EnumCRLF.Smart, RegistryValueKind.DWord);
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
        public static EnumCRLF CRLF
        {
            get { return (EnumCRLF)Convert.ToInt32(tools.GetValue("crlf", true)); }
            set { tools.SetValue("crlf", value, RegistryValueKind.DWord); }
        }
    }

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(OptionPageGrid), "TextTools", "PostSave", 0, 0, true)]
    [Guid(PostSaveProcess.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class PostSaveProcess : AsyncPackage
    {
        public const string PackageGuidString = "624A1C84-1E89-4FC9-8863-4FF2242FFB2B";

        #region Package Members

        private static OptionPageGrid Options { get; set; }
        private DocumentEvents documentEvents;
        public PostSaveProcess()
        { }

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
                Options = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));

            if (doc.Kind != "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}")
                return;

            var path = doc.FullName;
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
            switch (Options.OptionCRLF)
            {
                case Config.EnumCRLF.CRLF:
                    text = ConvertToCRLF(text);
                    break;
                case Config.EnumCRLF.LF:
                    text = ConvertToLF(text);
                    break;
                case Config.EnumCRLF.Smart:
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
            [DisplayName("convert to crlf")]
            [Description("0: keep line ending. |1: convert to \\r\\n.|2: convert to \\n. |3: smart line ending(less changes)")]
            public Config.EnumCRLF OptionCRLF
            {
                get { return Config.CRLF; }
                set { Config.CRLF = value; }
            }

            [Category("TextTools")]
            [DisplayName("add BOM")]
            [Description("Whether add BOM to file")]
            public bool OptionBOM
            {
                get { return Config.BOM; }
                set { Config.BOM = value; }
            }

            [Category("TextTools")]
            [DisplayName("remove trailing white spaces")]
            [Description("Whether remove trailing white spaces")]
            public bool OptionRemoveTrailingWhiteSpace
            {
                get { return Config.RWS; }
                set { Config.RWS = value; }
            }
        }
#endregion
    }

}