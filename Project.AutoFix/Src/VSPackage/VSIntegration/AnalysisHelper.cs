﻿//--------------------------------------------------------------------------
// <copyright file="AnalysisHelper.cs">
//   MS-PL
// </copyright>
// <license>
//   This source code is subject to terms and conditions of the Microsoft 
//   Public License. A copy of the license can be found in the License.html 
//   file at the root of this distribution. 
//   By using this source code in any fashion, you are agreeing to be bound 
//   by the terms of the Microsoft Public License. You must not remove this 
//   notice, or any other, from this software.
// </license>
//-----------------------------------------------------------------------
namespace StyleCop.VisualStudio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Windows.Forms;
    using System.Xml;
    using EnvDTE;

    /// <summary>
    /// Helper class that facilitates the analysis for the package.
    /// </summary>
    internal abstract class AnalysisHelper : IDisposable
    {
        #region Private Constants

        /// <summary>
        /// The default maximum number of violations we can encounter in one run before we quit.
        /// </summary>
        private const int DefaultMaxViolationCount = 1000;

        #endregion Private Constants

        #region Private Fields

        /// <summary>
        /// The StyleCop core object.
        /// </summary>
        private StyleCopCore core;

        /// <summary>
        /// System service provider.
        /// </summary>
        private IServiceProvider serviceProvider;

        /// <summary>
        /// The current violation count.
        /// </summary>
        private int violationCount;

        /// <summary>
        /// The maximum number of violations we can encounter in one run before we quit.
        /// </summary>
        private int maxViolationCount = DefaultMaxViolationCount;

        /// <summary>
        /// Stores the list of violations encountered.
        /// </summary>
        private List<ViolationInfo> violations;

        /// <summary>
        /// The collection of known VS project types and their properties.
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> projectTypes = new Dictionary<string, Dictionary<string, string>>();

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the AnalysisHelper class.
        /// </summary>
        /// <param name="serviceProvider">System service provider.</param>
        /// <param name="core">StyleCop engine.</param>
        internal AnalysisHelper(IServiceProvider serviceProvider, StyleCopCore core)
        {
            Param.AssertNotNull(serviceProvider, "serviceProvider");
            Param.AssertNotNull(core, "core");

            this.serviceProvider = serviceProvider;
            this.core = core;
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// Gets the core instance.
        /// </summary>
        internal StyleCopCore Core
        {
            get
            {
                return this.core;
            }
        }

        /// <summary>
        /// Gets the list of known VS project types supported, and their properties.
        /// </summary>
        internal Dictionary<string, Dictionary<string, string>> ProjectTypes
        {
            get
            {
                return this.projectTypes;
            }
        }

        /// <summary>
        /// Gets the system service provider.
        /// </summary>
        protected IServiceProvider ServiceProvider
        {
            get { return this.serviceProvider; }
        }

        #endregion Properties

        #region IDisposable Members

        /// <summary>
        /// Disposed the object.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Initializes the object.
        /// </summary>
        internal void Initialize()
        {
            // Register for StyleCop events.
            this.core.ViolationEncountered += this.CoreViolationEncountered;
            this.core.OutputGenerated += this.CoreOutputGenerated;

            this.RegisterEnvironmentEvents();

            this.InitializeMaxViolationCount();

            // Extract language specific information from the parsers configurations.
            this.RetrieveParserConfiguration();
        }

        /// <summary>
        /// Gathers the list of files to analyze and kicks off the worker thread.
        /// </summary>
        /// <param name="full">True if a full analyze should be performed.</param>
        /// <param name="autoFix">True if auto-fix should be performed.</param>
        /// <param name="type">Type of files that should be analyzed.</param>
        internal void Analyze(bool full, bool autoFix, AnalysisType type)
        {
            Param.Ignore(full, autoFix, type);

            // Save any documents that have been changed.
            if (this.SaveOpenDocuments())
            {
                // Get the list of projects to be analyzed.
                IList<CodeProject> projects = ProjectUtilities.GetProjectList(this.core, type);

                this.ClearEnvironmentPriorToAnalysis();

                this.SignalAnalysisStarted();

                this.violationCount = 0;

                if (projects.Count == 0)
                {
                    this.NoFilesToAnalyze();
                }
                else
                {
                    AnalysisThread analyze = new AnalysisThread(full, autoFix, projects, this.core);
                    analyze.Complete += new EventHandler(this.AnalyzeComplete);
                    System.Threading.Thread thread = new System.Threading.Thread(new ThreadStart(analyze.AnalyzeProc));

                    if (thread != null)
                    {
                        thread.IsBackground = true;

                        this.violations = new List<ViolationInfo>();

#if DEBUGTHREADING
                    analyze.AnalyzeProc();
#else
                        thread.Start();
#endif
                    }
                }
            }
        }

        /// <summary>
        /// Displays the settings for the local settings file.
        /// </summary>
        internal void LocalProjectSettings()
        {
            // Get the active project.
            Project project = ProjectUtilities.GetActiveProject();

            // Get the path to the local settings file for this project.
            string localSettingsFileFolder = ProjectUtilities.GetProjectPath(project);
            if (localSettingsFileFolder == null)
            {
                AlertDialog.Show(
                    this.core,
                    null,
                    Strings.CantGetProjectPath,
                    Strings.Title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            else
            {
                // Show the local settings dialog.
                string settingsFilePath = Path.Combine(localSettingsFileFolder, Settings.DefaultFileName);
                if (!File.Exists(settingsFilePath))
                {
                    string deprecatedSettingsFile = Path.Combine(localSettingsFileFolder, Settings.AlternateFileName);
                    if (File.Exists(deprecatedSettingsFile))
                    {
                        settingsFilePath = deprecatedSettingsFile;
                    }
                    else
                    {
                        deprecatedSettingsFile = Path.Combine(localSettingsFileFolder, V40Settings.DefaultFileName);
                        if (File.Exists(deprecatedSettingsFile))
                        {
                            settingsFilePath = deprecatedSettingsFile;
                        }
                    }
                }

                this.core.ShowSettings(settingsFilePath);
            }
        }

        /// <summary>
        /// Cancels the currently running analysis.
        /// </summary>
        internal void Cancel()
        {
            this.core.Cancel = true;
        }

        #endregion Internal Methods

        #region Protected Methods

        /// <summary>
        /// Disposes the contents of the class.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            Param.Ignore(disposing);

            if (disposing)
            {
                if (this.core != null)
                {
                    // Unregister for StyleCop events.
                    this.core.ViolationEncountered -= this.CoreViolationEncountered;
                    this.core.OutputGenerated -= this.CoreOutputGenerated;
                    this.core.Dispose();
                    this.core = null;
                }
            }
        }

        /// <summary>
        /// Clears the environment prior to analysis.
        /// </summary>
        protected virtual void ClearEnvironmentPriorToAnalysis()
        {
        }

        /// <summary>
        /// Signals the helper to output that analysis has begun.
        /// </summary>
        protected virtual void SignalAnalysisStarted()
        {
        }

        /// <summary>
        /// Signals the helper to indicate that no files were available for analysis.
        /// </summary>
        protected virtual void NoFilesToAnalyze()
        {
        }

        /// <summary>
        /// Saves any open document that matches a type specified by one of the parsers.
        /// </summary>
        /// <returns>Returns true if all documents were saved, or false if one or more
        /// documents were unable to be saved.</returns>
        protected virtual bool SaveOpenDocuments()
        {
            return true;
        }

        /// <summary>
        /// Register for the environment events.
        /// </summary>
        protected virtual void RegisterEnvironmentEvents()
        {
        }

        /// <summary>
        /// Provides the end analysis result to the user.
        /// </summary>
        /// <param name="violationsResult">The violations.</param>
        protected virtual void ProvideEndAnalysisResult(List<ViolationInfo> violationsResult)
        {
            Param.Ignore(violationsResult);
        }

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        /// Reads parser configuration xml and initializes the dictionary 'projectFullPaths' based on the information in it.
        /// </summary>
        /// <remarks>
        /// Format of the xml that must be found in "SourceParser" tag in the Parser xml:
        ///   <![CDATA[
        ///   <VsProjectLocation>
        ///     <!-- PropertyName is the property name in the EnvDTE.Project's properties collection that specifies the full path to the directory of the project file. -->
        ///     <PropertyName>FullPath</PropertyName>
        ///     <!-- ProjectKind is the guid that identifies the project kind, or in other words the language type C#/C++ etc.
        ///          The C# language kind is defined by prjKindCSharpProject in VSLangProj.DLL.
        ///          The VB language kind is defined by prjKindVBProject  in VSLangProj.DLL.
        ///          Also look at dte.idl for various vsProjectKind* constants that could be returned by other projects.  
        ///     -->
        ///     <ProjectKind>FAE04EC0-301F-11D3-BF4B-00C04F79EFBC</ProjectKind>
        ///   </VsProjectLocation>
        ///   ]]>
        /// </remarks>
        private void RetrieveParserConfiguration()
        {
            Debug.Assert(this.core != null, "core has not been initialized");
            Debug.Assert(this.core.Parsers != null, "core parsers has not been initialized.");

            foreach (SourceParser parser in this.core.Parsers)
            {
                XmlDocument document = StyleCopCore.LoadAddInResourceXml(parser.GetType(), null);

                // Using plenty of exceptions with regards to the information drawn from the configuration xml.
                // An alternative would of course be to validate against a schema.
                XmlNodeList projectTypeNodes = document.DocumentElement.SelectNodes("VsProjectTypes/VsProjectType");

                if (projectTypeNodes != null)
                {
                    foreach (XmlNode projectTypeNode in projectTypeNodes)
                    {
                        // Find the ProjectKind attribute
                        XmlNode projectKindNode = projectTypeNode.Attributes["ProjectKind"];
                        if (projectKindNode == null)
                        {
                            string errorMessage = Strings.MalFormedVsProjectLocationNode;
                            errorMessage = string.Format(CultureInfo.CurrentCulture, errorMessage, Strings.NoProjectKindNodeFound);
                            throw new InvalidDataException(errorMessage);
                        }

                        // Determine the project kind 
                        string projectKind = projectKindNode.InnerText.Trim();
                        if (string.IsNullOrEmpty(projectKind))
                        {
                            string errorMessage = Strings.MalFormedVsProjectLocationNode;
                            string subMessage = string.Format(CultureInfo.CurrentCulture, Strings.EmptyChildNode, "ProjectKind");
                            errorMessage = string.Format(CultureInfo.CurrentCulture, errorMessage, subMessage);
                            throw new InvalidDataException(errorMessage);
                        }

                        // Get or create the property collection for the project type.
                        Dictionary<string, string> projectProperties = null;
                        if (!this.projectTypes.TryGetValue(projectKind, out projectProperties))
                        {
                            projectProperties = new Dictionary<string, string>();
                            this.projectTypes.Add(projectKind, projectProperties);
                        }

                        // Determine the project path property name value
                        string projectPathPropertyName = null;
                        XmlNode projectPathPropertyNameNode = projectTypeNode.SelectSingleNode("ProjectPathPropertyName");
                        if (projectPathPropertyNameNode != null)
                        {
                            projectPathPropertyName = projectPathPropertyNameNode.InnerText.Trim();
                            if (string.IsNullOrEmpty(projectPathPropertyName))
                            {
                                string errorMessage = Strings.MalFormedVsProjectLocationNode;
                                string subMessage = string.Format(CultureInfo.CurrentCulture, Strings.EmptyChildNode, "PropertyName");
                                errorMessage = string.Format(CultureInfo.CurrentCulture, errorMessage, subMessage);
                                throw new InvalidDataException(errorMessage);
                            }
                        }

                        string existingPropertyName = null;
                        if (!projectProperties.TryGetValue("ProjectPath", out existingPropertyName))
                        {
                            // Add the new information to the dictionary
                            projectProperties.Add("ProjectPath", projectPathPropertyName);
                        }
                        else
                        {
                            // Allow this to succeed at runtime.
                            Debug.Fail("A previously loaded parser already registered a property name for the Project Full Path with regards to the project kind: " + projectKind);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the max violations value from the registry if it exists.
        /// </summary>
        private void InitializeMaxViolationCount()
        {
            object value = this.core.Registry.CUGetValue("MaxViolations");
            if (value != null)
            {
                try
                {
                    this.maxViolationCount = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException)
                {
                    this.maxViolationCount = DefaultMaxViolationCount;
                }
                catch (FormatException)
                {
                    this.maxViolationCount = DefaultMaxViolationCount;
                }
                catch (OverflowException)
                {
                    this.maxViolationCount = DefaultMaxViolationCount;
                }
            }
        }

        /// <summary>
        /// Called when output should be added to the Ouput pane.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">Contains the output string.</param>
        private void CoreOutputGenerated(object sender, OutputEventArgs e)
        {
            Param.Ignore(sender, e);

            // Make sure this is running on the main thread.
            if (InvisibleForm.Instance.InvokeRequired)
            {
                EventHandler<OutputEventArgs> outputDelegate = new EventHandler<OutputEventArgs>(
                    this.CoreOutputGenerated);

                InvisibleForm.Instance.Invoke(outputDelegate, sender, e);
            }
            else
            {
                EnvDTE.OutputWindowPane pane = VSWindows.GetInstance(this.serviceProvider).OutputPane;
                if (pane != null)
                {
                    pane.OutputString(e.Output);
                }
            }
        }

        /// <summary>
        /// Called when the analyze thread has completed.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void AnalyzeComplete(object sender, EventArgs e)
        {
            Param.AssertNotNull(sender, "sender");
            Param.Ignore(e);

            if (InvisibleForm.Instance.InvokeRequired)
            {
                EventHandler complete = new EventHandler(this.AnalyzeCompleteMain);
                InvisibleForm.Instance.Invoke(complete, sender, e);
            }
            else
            {
                this.AnalyzeCompleteMain(sender, e);
            }
        }

        /// <summary>
        /// Called when the analyze thread has completed.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void AnalyzeCompleteMain(object sender, EventArgs e)
        {
            Param.AssertNotNull(sender, "sender");
            Param.Ignore(e);

            EnvDTE.OutputWindowPane pane = VSWindows.GetInstance(this.serviceProvider).OutputPane;
            if (pane != null)
            {
                if (this.core.Cancel)
                {
                    pane.OutputString(string.Format(
                        CultureInfo.InvariantCulture, Strings.LogBreak, Strings.Cancelled));
                }
                else
                {
                    pane.OutputString(string.Format(
                        CultureInfo.InvariantCulture, Strings.LogBreak, Strings.Done));

                    pane.OutputString(string.Format(CultureInfo.InvariantCulture, Strings.ViolationCount, this.violationCount) + "\n\n\n");
                }
            }

            if (this.violationCount > 0)
            {
                this.ProvideEndAnalysisResult(this.violations);
                this.violations = null;
            }
        }

        /// <summary>
        /// Called when a violation is found.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CoreViolationEncountered(object sender, ViolationEventArgs e)
        {
            Param.AssertNotNull(e, "e");
            Param.Ignore(sender);

            // Make sure this is running on the main thread.
            if (InvisibleForm.Instance.InvokeRequired)
            {
                EventHandler<ViolationEventArgs> violationDelegate = this.CoreViolationEncountered;
                InvisibleForm.Instance.Invoke(violationDelegate, sender, e);
            }
            else
            {
                if (!e.Warning)
                {
                    ++this.violationCount;
                }

                // Check the count. At some point we don't allow any more violations 
                // so we cancel the analyze run.
                if (this.maxViolationCount > 0 && this.violationCount == this.maxViolationCount)
                {
                    this.Cancel();
                }

                ICodeElement element = e.Element;
                ViolationInfo violation = new ViolationInfo();
                violation.Description = string.Concat(e.Violation.Rule.CheckId, ": ", e.Message);
                violation.LineNumber = e.LineNumber;
                violation.Rule = e.Violation.Rule;

                if (element != null)
                {
                    violation.File = element.Document.SourceCode.Path;
                }
                else
                {
                    string file = string.Empty;
                    if (e.SourceCode != null)
                    {
                        file = e.SourceCode.Path;
                    }

                    violation.File = file;
                }

                this.violations.Add(violation);
            }
        }

        #endregion Private Methods
    }
}
