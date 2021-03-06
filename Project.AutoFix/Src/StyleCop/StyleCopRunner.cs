﻿//-----------------------------------------------------------------------
// <copyright file="StyleCopRunner.cs">
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
namespace StyleCop
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Security;
    using System.Text;
    using System.Xml;

    /// <summary>
    /// Object model for hosting StyleCop in a simplified manner.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "StyleCop", Justification = "This is the correct casing.")]
    public abstract class StyleCopRunner : IDisposable
    {
        #region Private Fields

        /// <summary>
        /// The violation count.
        /// </summary>
        private int violationCount;

        /// <summary>
        /// The violations document.
        /// </summary>
        private XmlDocument violations = new XmlDocument();

        /// <summary>
        /// The StyleCop core instance.
        /// </summary>
        private StyleCopCore core;

        #endregion Private Fields

        #region Protected Constructors

        /// <summary>
        /// Initializes a new instance of the StyleCopRunner class.
        /// </summary>
        protected StyleCopRunner()
        {
            XmlElement root = this.violations.CreateElement("StyleCopViolations");
            this.violations.AppendChild(root);
        }

        #endregion Public Constructors

        #region Public Events

        /// <summary>
        /// Event that is fired when output is generated from the console during an analysis.
        /// </summary>
        public event EventHandler<OutputEventArgs> OutputGenerated;

        /// <summary>
        /// Event that is fired when output is generated from the console during an analysis.
        /// </summary>
        public event EventHandler<ViolationEventArgs> ViolationEncountered;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Gets or sets the StyleCop core instance.
        /// </summary>
        public StyleCopCore Core
        {
            get
            {
                return this.core;
            }

            protected set
            {
                this.core = value;
            }
        }

        /// <summary>
        /// Gets the current environment.
        /// </summary>
        public StyleCopEnvironment Environment
        {
            get
            {
                return this.core == null ? null : this.core.Environment;
            }
        }

        #endregion Public Properties

        #region Protected Properties

        /// <summary>
        /// Gets or sets the violation count.
        /// </summary>
        protected int ViolationCount
        {
            get
            {
                return this.violationCount;
            }

            set
            {
                Param.AssertGreaterThanOrEqualToZero(value, "ViolationCount");
                this.violationCount = value;
            }
        }

        /// <summary>
        /// Gets the violations document.
        /// </summary>
        protected XmlDocument Violations
        {
            get
            {
                return this.violations;
            }
        }

        #endregion Protected Properties

        #region Public Methods

        /// <summary>
        /// Disposes the contents of the class.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (this.core != null)
            {
                this.core.Dispose();
            }
        }

        /// <summary>
        /// Adds a source code document to the given project.
        /// </summary>
        /// <param name="project">The project which should contain the source code instance.</param>
        /// <param name="path">The path to the source code document to add.</param>
        /// <param name="context">Optional context information.</param>
        /// <returns>Returns true if any source code documents were added to the project.</returns>
        public bool AddSourceCode(CodeProject project, string path, object context)
        {
            Param.RequireNotNull(project, "project");
            Param.RequireValidString(path, "path");
            Param.Ignore(context);

            if (this.Environment == null)
            {
                throw new InvalidOperationException();
            }

            return this.Environment.AddSourceCode(project, path, context);
        }

        #endregion Public Methods

        #region Protected Virtual Methods

        /// <summary>
        /// Initializes the core module.
        /// </summary>
        protected virtual void InitCore()
        {
            this.core.DisplayUI = false;
            this.core.ViolationEncountered += new EventHandler<ViolationEventArgs>(this.CoreViolationEncountered);
            this.core.OutputGenerated += new EventHandler<OutputEventArgs>(this.CoreOutputGenerated);
        }

        #endregion Protected Virtual Methods

        #region Protected Methods

        /// <summary>
        /// Called when output is generated during an analysis.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected void OnOutputGenerated(OutputEventArgs e)
        {
            Param.RequireNotNull(e, "e");

            if (this.OutputGenerated != null)
            {
                this.OutputGenerated(this, e);
            }
        }

        /// <summary>
        /// Called when a violation is encountered during an analysis.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected void OnViolationEncountered(ViolationEventArgs e)
        {
            Param.RequireNotNull(e, "e");

            if (this.ViolationEncountered != null)
            {
                this.ViolationEncountered(this, e);
            }
        }

        #endregion Protected Methods

        #region Private Static Methods

        /// <summary>
        /// Creates a safe version of the element name that can be outputted to Xml.
        /// </summary>
        /// <param name="originalName">The original name.</param>
        /// <returns>Returns the safe name.</returns>
        private static string CreateSafeSectionName(string originalName)
        {
            Param.Ignore(originalName);

            if (originalName == null)
            {
                return null;
            }

            int index = originalName.IndexOf('<');
            if (index == -1)
            {
                return originalName;
            }

            StringBuilder builder = new StringBuilder(originalName.Length * 2);

            int startTagCount = 0;

            for (int i = 0; i < originalName.Length; ++i)
            {
                char character = originalName[i];

                if (character == '<')
                {
                    ++startTagCount;
                    builder.Append('`');
                }
                else if (startTagCount > 0)
                {
                    if (character == '>')
                    {
                        --startTagCount;
                    }
                    else if (character == ',')
                    {
                        builder.Append('`');
                    }
                    else if (!char.IsWhiteSpace(character))
                    {
                        builder.Append(character);
                    }
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        #endregion Private Static Methods

        #region Private Methods

        /// <summary>
        /// Called when output should be added to the Output pane.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CoreOutputGenerated(object sender, OutputEventArgs e)
        {
            Param.Ignore(sender, e);

            lock (this)
            {
                this.OnOutputGenerated(new OutputEventArgs(e.Output, e.Importance));
            }
        }

        /// <summary>
        /// Called when a violation is found.
        /// </summary> 
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CoreViolationEncountered(object sender, ViolationEventArgs e)
        {
            Param.Ignore(sender, e);

            lock (this)
            {
                // Create the violation element.
                XmlElement violation = this.violations.CreateElement("Violation");
                XmlAttribute attrib = null;

                // Add the element section if it's not empty.
                if (e.Element != null)
                {
                    attrib = this.violations.CreateAttribute("Section");
                    attrib.Value = CreateSafeSectionName(e.Element.FullyQualifiedName);
                    violation.Attributes.Append(attrib);
                }

                // Add the line number.
                attrib = this.violations.CreateAttribute("LineNumber");
                attrib.Value = e.LineNumber.ToString(CultureInfo.InvariantCulture);
                violation.Attributes.Append(attrib);

                // Get the source code that this element is in.
                SourceCode sourceCode = e.SourceCode;
                if (sourceCode == null && e.Element != null && e.Element.Document != null)
                {
                    sourceCode = e.Element.Document.SourceCode;
                }

                // Add the source code path.
                if (sourceCode != null)
                {
                    attrib = this.violations.CreateAttribute("Source");
                    attrib.Value = sourceCode.Path;
                    violation.Attributes.Append(attrib);
                }

                // Add the rule namespace.
                attrib = this.violations.CreateAttribute("RuleNamespace");
                attrib.Value = e.Violation.Rule.Namespace;
                violation.Attributes.Append(attrib);

                // Add the rule name.
                attrib = this.violations.CreateAttribute("Rule");
                attrib.Value = e.Violation.Rule.Name;
                violation.Attributes.Append(attrib);

                // Add the rule ID.
                attrib = this.violations.CreateAttribute("RuleId");
                attrib.Value = e.Violation.Rule.CheckId;
                violation.Attributes.Append(attrib);

                violation.InnerText = e.Message;

                this.violations.DocumentElement.AppendChild(violation);
                this.violationCount++;
            }

            // Forward event
            this.OnViolationEncountered(new ViolationEventArgs(e.Violation));
        }

        #endregion Private Methods
    }
}
