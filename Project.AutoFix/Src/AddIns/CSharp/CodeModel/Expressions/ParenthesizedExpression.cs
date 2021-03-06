//-----------------------------------------------------------------------
// <copyright file="ParenthesizedExpression.cs">
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
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// An expression wrapped within parenthesis.
    /// </summary>
    /// <subcategory>expression</subcategory>
    public sealed class ParenthesizedExpression : Expression
    {
        #region Private Fields

        /// <summary>
        /// The expression within the parenthesis.
        /// </summary>
        private CodeUnitProperty<Expression> innerExpression;

        #endregion Private Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the ParenthesizedExpression class.
        /// </summary>
        /// <param name="proxy">Proxy object for the expression.</param>
        /// <param name="innerExpression">The expression within the parenthesis.</param>
        internal ParenthesizedExpression(CodeUnitProxy proxy, Expression innerExpression)
            : base(proxy, ExpressionType.Parenthesized)
        {
            Param.AssertNotNull(proxy, "proxy");
            Param.AssertNotNull(innerExpression, "innerExpression");

            this.innerExpression.Value = innerExpression;
        }

        #endregion Internal Constructors

        #region Public Properties

        /// <summary>
        /// Gets the expression within the parenthesis.
        /// </summary>
        public Expression InnerExpression
        {
            get
            {
                this.ValidateEditVersion();

                if (!this.innerExpression.Initialized)
                {
                    this.innerExpression.Value = this.FindFirstChildExpression();

                    if (this.innerExpression.Value == null)
                    {
                        throw new SyntaxException(this.Document, this.LineNumber);
                    }
                }

                return this.innerExpression.Value;
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

            this.innerExpression.Reset();
        }

        #endregion Protected Override Methods
    }
}
