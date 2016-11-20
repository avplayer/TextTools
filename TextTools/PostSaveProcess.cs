//------------------------------------------------------------------------------
// <copyright file="VSPackage1.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using EnvDTE;
using EnvDTE80;
using System.IO;
using System.Text;
using System.ComponentModel;

namespace TextTools
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(UIContextGuids.SolutionExists)]
    [ProvideOptionPage(typeof(OptionPageGrid), "TextTools", "PostSave", 0, 0, true)]
    [Guid(PostSaveProcess.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class PostSaveProcess : Package
    {
        /// <summary>
        /// VSPackage1 GUID string.
        /// </summary>
        public const string PackageGuidString = "624A1C84-1E89-4FC9-8863-4FF2242FFB2B";

        /// <summary>
        /// Initializes a new instance of the <see cref="PostSaveProcess"/> class.
        /// </summary>
        public PostSaveProcess()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            Debug.WriteLine("Construct");
        }

        #region Package Members

        private DocumentEvents documentEvents;

        private OptionPageGrid.EnumCRLF OptionCRLF
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.OptionCRLF;
            }
        }

        private bool OptionBOM
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.OptionBOM;
            }
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine("Initialize");
            base.Initialize();
            //AddNextOccurCommand.Initialize(this);

            var dte = GetService(typeof(DTE)) as DTE2;
            documentEvents = dte.Events.DocumentEvents;
            documentEvents.DocumentSaved += OnDocumentSaved;


        }

        void OnDocumentSaved(Document doc)
        {
            if (doc.Kind != "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}")
            {
                return;
            }
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
                var reader = new StreamReader(stream, Encoding.Default);
                text = reader.ReadToEnd();
            }
            stream.Close();

            var encoding = new UTF8Encoding(OptionBOM, false);
            switch (OptionCRLF)
            {
                case OptionPageGrid.EnumCRLF.CRLF:
                    text = ConvertToCRLF(text);
                    break;
                case OptionPageGrid.EnumCRLF.LF:
                    text = ConvertToLF(text);
                    break;
                case OptionPageGrid.EnumCRLF.Smart:
                    var crln = text.Length - text.Replace("\r\n", "\n").Length;
                    var ln = text.Split('\n').Length - 1 - crln;

                    if (crln > ln)
                    {
                        text = ConvertToCRLF(text);
                    }
                    else
                    {
                        text = ConvertToLF(text);
                    }

                    break;
                default:
                    break;
            }
            stream = File.Open(path, FileMode.Truncate | FileMode.OpenOrCreate);
            var writer = new BinaryWriter(stream);
            writer.Write(encoding.GetPreamble());
            writer.Write(encoding.GetBytes(text));
            writer.Close();

            Debug.WriteLine("Convert to UTF-8");
        }

        private static string ConvertToLF(string text)
        {
            text = text.Replace("\r\n", "\n");
            Debug.WriteLine("Convert to LF");
            return text;
        }

        private static string ConvertToCRLF(string text)
        {
            text = text.Replace("\r\n", "\n");
            text = text.Replace("\n", "\r\n");
            Debug.WriteLine("Convert to CRLF");
            return text;
        }

        class OptionPageGrid : DialogPage
        {
            public enum EnumCRLF
            {
                Keep,
                CRLF,
                LF,
                Smart,
            }
            private EnumCRLF optionCrLf = 0;

            [Category("TextTools")]
            [DisplayName("convert to crlf")]
            [Description("0: keep line ending. |1: convert to \\r\\n.|2: convert to \\n. |3: smart line ending(less changes)")]
            public EnumCRLF OptionCRLF
            {
                get { return optionCrLf; }
                set { optionCrLf = value; }
            }

            private bool optionBOM = false;

            [Category("TextTools")]
            [DisplayName("add BOM")]
            [Description("Whether add BOM to file")]
            public bool OptionBOM
            {
                get { return optionBOM; }
                set { optionBOM = value; }
            }
        }
        #endregion
    }

}