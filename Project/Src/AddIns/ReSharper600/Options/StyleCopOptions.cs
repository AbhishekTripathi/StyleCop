﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StyleCopOptions.cs" company="http://stylecop.codeplex.com">
//   MS-PL
// </copyright>
// <license>
//   This source code is subject to terms and conditions of the Microsoft 
//   Public License. A copy of the license can be found in the License.html 
//   file at the root of this distribution. If you cannot locate the  
//   Microsoft Public License, please send an email to dlr@microsoft.com. 
//   By using this source code in any fashion, you are agreeing to be bound 
//   by the terms of the Microsoft Public License. You must not remove this 
//   notice, or any other, from this software.
// </license>
// <summary>
//   Class to hold all of the Configurable options for this addin.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
extern alias JB;

namespace StyleCop.ReSharper600.Options
{
    #region Using Directives

    using System;
    using System.Windows.Forms;
    using System.Xml;

    using JetBrains.Application;
    using JetBrains.Application.Components;
    using JetBrains.Application.Configuration;

    using Microsoft.Win32;

    using StyleCop.ReSharper600.Core;

    #endregion

    /// <summary>
    /// Class to hold all of the Configurable options for this addin.
    /// </summary>
    [ShellComponent(ProgramConfigurations.VS_ADDIN)]
    public class StyleCopOptions : IXmlExternalizable, IDisposable
    {
        #region Fields

        private bool alwaysCheckForUpdatesWhenVisualStudioStarts;

        /// <summary>
        /// Set to true when we've attempted to get the StyleCop path.
        /// </summary>
        private bool attemptedToGetStyleCopPath;

        /// <summary>
        /// Tracks whether we should check for updates.
        /// </summary>
        private bool automaticallyCheckForUpdates;

        /// <summary>
        /// The number of days between update checks.
        /// </summary>
        private int daysBetweenUpdateChecks;

        /// <summary>
        /// The value of the detected path for StyleCop.
        /// </summary>
        private string styleCopDetectedPath;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="StyleCopOptions"/> class. 
        /// Set to max performance by default.
        /// </summary>
        public StyleCopOptions()
        {
            this.ParsingPerformance = 9;
            this.SpecifiedAssemblyPath = string.Empty;
            this.InsertTextIntoDocumentation = true;
            this.AutomaticallyCheckForUpdates = true;
            this.AlwaysCheckForUpdatesWhenVisualStudioStarts = false;
            this.DaysBetweenUpdateChecks = 7;
            this.LastUpdateCheckDate = "1900-01-01";
            this.DashesCountInFileHeader = 116;
            this.UseExcludeFromStyleCopSetting = true;
            this.SuppressStyleCopAttributeJustificationText = "Reviewed. Suppression is OK here.";
            this.UseSingleLineDeclarationComments = false;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static StyleCopOptions Instance
        {
            get
            {
                return Shell.Instance.GetComponent<StyleCopOptions>();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether AlwaysCheckForUpdatesWhenVisualStudioStarts.
        /// </summary>
        [XmlExternalizable(true)]
        public bool AlwaysCheckForUpdatesWhenVisualStudioStarts
        {
            get
            {
                return this.alwaysCheckForUpdatesWhenVisualStudioStarts;
            }

            set
            {
                this.alwaysCheckForUpdatesWhenVisualStudioStarts = value;

                SetRegistry("AlwaysCheckForUpdatesWhenVisualStudioStarts", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether we check for updates when plugin starts.
        /// </summary>
        [XmlExternalizable(true)]
        public bool AutomaticallyCheckForUpdates
        {
            get
            {
                return this.automaticallyCheckForUpdates;
            }

            set
            {
                this.automaticallyCheckForUpdates = value;
                SetRegistry("AutomaticallyCheckForUpdates", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Gets or sets DashesCountInFileHeader.
        /// </summary>
        [XmlExternalizable(116)]
        public int DashesCountInFileHeader { get; set; }

        /// <summary>
        /// Gets or sets DaysBetweenUpdateChecks.
        /// </summary>
        [XmlExternalizable(2)]
        public int DaysBetweenUpdateChecks
        {
            get
            {
                return this.daysBetweenUpdateChecks;
            }

            set
            {
                this.daysBetweenUpdateChecks = value;
                SetRegistry("DaysBetweenUpdateChecks", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether descriptive text should be inserted into missing documentation headers.
        /// </summary>
        [XmlExternalizable(true)]
        public bool InsertTextIntoDocumentation { get; set; }

        /// <summary>
        /// Gets or sets the last update check date.
        /// </summary>
        [XmlExternalizable("1900-01-01")]
        public string LastUpdateCheckDate { get; set; }

        /// <summary>
        /// Gets or sets the ParsingPerformance value.
        /// </summary>
        /// <value>
        /// The performance value.
        /// </value>
        [XmlExternalizable(9)]
        public int ParsingPerformance { get; set; }

        /// <summary>
        /// Gets the Scope that defines which store the data goes into.
        /// Must not be
        /// <c>0.</c>.
        /// </summary>
        /// <value>
        /// The Scope.
        /// </value>
        public XmlExternalizationScope Scope
        {
            get
            {
                return XmlExternalizationScope.UserSettings;
            }
        }

        /// <summary>
        /// Gets or sets the Specified Assembly Path.
        /// </summary>
        /// <value>
        /// The allow null attribute.
        /// </value>
        [XmlExternalizable("")]
        public string SpecifiedAssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets the text for inserting suppress message attributes.
        /// </summary>
        [XmlExternalizable("Reviewed. Suppression is OK here.")]
        public string SuppressStyleCopAttributeJustificationText { get; set; }

        /// <summary>
        /// Gets the name of the tag.
        /// </summary>
        /// <value>
        /// The name of the tag.
        /// </value>
        public string TagName
        {
            get
            {
                return "StyleCop.ReSharper";
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use exclude from style cop setting.
        /// </summary>
        [XmlExternalizable(true)]
        public bool UseExcludeFromStyleCopSetting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether declaration comments should be multi line or single line.
        /// </summary>
        [XmlExternalizable(false)]
        public bool UseSingleLineDeclarationComments { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Detects the style cop path.
        /// </summary>
        /// <returns>
        /// The path to the detected StyleCop assembly.
        /// </returns>
        public string DetectStyleCopPath()
        {
            string assemblyPath = StyleCopLocator.GetStyleCopPath();
            return StyleCopReferenceHelper.LocationValid(assemblyPath) ? assemblyPath : null;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        /// <returns>
        /// The path to the StyleCop assembly.
        /// </returns>
        public string GetAssemblyPath()
        {
            if (!this.attemptedToGetStyleCopPath)
            {
                this.attemptedToGetStyleCopPath = true;

                if (!string.IsNullOrEmpty(this.SpecifiedAssemblyPath))
                {
                    if (StyleCopReferenceHelper.LocationValid(this.SpecifiedAssemblyPath))
                    {
                        this.styleCopDetectedPath = this.SpecifiedAssemblyPath;
                        return this.styleCopDetectedPath;
                    }

                    // Location not valid. Blank it and automatically get location
                    this.SpecifiedAssemblyPath = null;
                }

                this.styleCopDetectedPath = this.DetectStyleCopPath();

                if (string.IsNullOrEmpty(this.styleCopDetectedPath))
                {
                    MessageBox.Show(
                        string.Format("Failed to find the StyleCop Assembly. Please check your StyleCop installation."), 
                        "Error Finding StyleCop Assembly", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Error);
                }
            }

            return this.styleCopDetectedPath;
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public void Init()
        {
        }

        #endregion

        #region Explicit Interface Methods

        /// <summary>
        /// Reads the settings from the XML.
        /// </summary>
        /// <param name="element">
        /// The XmlElement to read from.
        /// </param>
        void IXmlExternalizable.ReadFromXml(XmlElement element)
        {
            if (element == null)
            {
                return;
            }

            XmlExternalizationUtil.ReadFromXml(element, this);
        }

        /// <summary>
        /// Writes the settings to XML.
        /// </summary>
        /// <param name="element">
        /// The element.
        /// </param>
        void IXmlExternalizable.WriteToXml(XmlElement element)
        {
            XmlExternalizationUtil.WriteToXml(element, this);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets a registry key value in the registry.
        /// </summary>
        /// <param name="key">
        /// The sub key to create.
        /// </param>
        /// <param name="value">
        /// The value to use.
        /// </param>
        /// <param name="valueKind">
        /// The type of registry key value to set.
        /// </param>
        private static void SetRegistry(string key, object value, RegistryValueKind valueKind)
        {
            const string SubKey = @"SOFTWARE\CodePlex\StyleCop";

            RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(SubKey);
            if (registryKey != null)
            {
                registryKey.SetValue(key, value, valueKind);
            }
        }

        #endregion
    }
}