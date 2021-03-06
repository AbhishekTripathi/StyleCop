//-----------------------------------------------------------------------
// <copyright file="GotoStatement.cs">
//     MS-PL
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
namespace StyleCop.CSharp.CodeModel
{
    using System;

    /// <summary>
    /// A goto-statement.
    /// </summary>
    /// <subcategory>statement</subcategory>
    public sealed class GotoStatement : Statement
    {
        #region Private Fields

        /// <summary>
        /// The identifier of the label to jump to.
        /// </summary>
        private CodeUnitProperty<Expression> identifier;

        #endregion Private Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the GotoStatement class.
        /// </summary>
        /// <param name="proxy">Proxy object for the statement.</param>
        /// <param name="identifier">The identifier of the label to jump to.</param>
        internal GotoStatement(CodeUnitProxy proxy, Expression identifier)
            : base(proxy, StatementType.Goto)
        {
            Param.AssertNotNull(proxy, "proxy");
            Param.AssertNotNull(identifier, "identifier");

            this.identifier.Value = identifier;
        }

        #endregion Internal Constructors

        #region Public Properties

        /// <summary>
        /// Gets the identifier of the label to jump to.
        /// </summary>
        public Expression Identifier
        {
            get
            {
                this.ValidateEditVersion();

                if (!this.identifier.Initialized)
                {
                    Token token = this.FindFirstChild<GotoToken>();
                    if (token == null)
                    {
                        throw new SyntaxException(this.Document, this.LineNumber);
                    }

                    this.identifier.Value = token.FindNextSiblingExpression();
                    if (this.identifier.Value == null)
                    {
                        throw new SyntaxException(this.Document, this.LineNumber);
                    }
                }

                return this.identifier.Value;
            }
        }

        #endregion Public Properties

        #region Protected Override Methods

        /// <summary>
        /// Resets the contents of the class.
        /// </summary>
        protected override void Reset()
        {
            base.Reset();

            this.identifier.Reset();
        }

        #endregion Protected Override Methods
    }
}
