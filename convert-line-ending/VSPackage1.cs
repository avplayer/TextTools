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
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.ComponentModel;
 
namespace convert_line_ending
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
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [ProvideOptionPage(typeof(OptionPageGrid), "convert-line-ending", "My Grid Page", 0, 0, true)]
    [Guid(VSPackage1.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class VSPackage1 : Package
    {
        /// <summary>
        /// VSPackage1 GUID string.
        /// </summary>
        public const string PackageGuidString = "9f2d6689-46fe-4abe-94c4-78a3fe42afd8";

        /// <summary>
        /// Initializes a new instance of the <see cref="VSPackage1"/> class.
        /// </summary>
        public VSPackage1()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            Debug.WriteLine("Construct");
        }

        #region Package Members

        private DocumentEvents documentEvents;

        private bool OptionCRLF
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

            var dte = GetService(typeof(DTE)) as DTE2;
            documentEvents = dte.Events.DocumentEvents;
            documentEvents.DocumentSaved += OnDocumentSaved;
        }

        void OnDocumentSaved(Document doc)
        {
            if(doc.Kind != "{8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A}")
            {
                return;
            }
            var path = doc.FullName;
            var stream = new FileStream(path, FileMode.Open);

            string text;
            stream.Position = 0;
            var reader = new StreamReader(stream, Encoding.Default);
            text = reader.ReadToEnd();
            stream.Close();
             
            var encoding = new UTF8Encoding(OptionBOM, false);
            if(OptionCRLF)
            {
                text = text.Replace("\r\n", "\n");
                text = text.Replace("\n", "\r\n");
                Debug.WriteLine("Convert to CRLF");
            }
            else
            {
                text = text.Replace("\r\n", "\n");
                Debug.WriteLine("Convert to LF");
            }
            var bytes = encoding.GetBytes(text);

            File.WriteAllBytes(path, bytes);

            Debug.WriteLine("Convert to UTF-8");
        }

        class OptionPageGrid : DialogPage
        {
            private bool optionCrLf = false;

            [Category("convert-line-ending")]
            [DisplayName("convert to crlf")]
            [Description("convert to \\r\\n if true, convert to \\n if false")]
            public bool OptionCRLF
            {
                get { return optionCrLf; }
                set { optionCrLf = value; }
            }

            private bool optionBOM = false;

            [Category("convert-line-ending")]
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
