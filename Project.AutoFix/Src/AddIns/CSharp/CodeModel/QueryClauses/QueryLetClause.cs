//-----------------------------------------------------------------------
// <copyright file="QueryLetClause.cs">
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

    /// <summary>
    /// Describes a let clause in a query expression.
    /// </summary>
    public sealed class QueryLetClause : QueryClause
    {
        #region Private Fields

        /// <summary>
        /// The variable defined in the clause.
        /// </summary>
        private CodeUnitProperty<IVariable> rangeVariable;

        /// <summary>
        /// The variables within the clause.
        /// </summary>
        private CodeUnitProperty<VariableCollection> variables;

        /// <summary>
        /// The  expression.
        /// </summary>
        private CodeUnitProperty<Expression> expression;

        #endregion Private Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the QueryLetClause class.
        /// </summary>
        /// <param name="proxy">Proxy object for the clause.</param>
        /// <param name="expression">The range expression.</param>
        internal QueryLetClause(CodeUnitProxy proxy, Expression expression)
            : base(proxy, QueryClauseType.Let)
        {
            Param.AssertNotNull(proxy, "proxy");
            Param.AssertNotNull(expression, "expression");

            this.expression.Value = expression;
        }

        #endregion Internal Constructors

        #region Public Override Properties

        /// <summary>
        /// Gets the variables defined within this clause.
        /// </summary>
        /// <returns>Returns the collection of variables.</returns>
        public override VariableCollection Variables
        {
            get
            {
                this.ValidateEditVersion();

                if (!this.variables.Initialized)
                {
                    this.variables.Value = new VariableCollection();

                    IVariable rangeVariable = this.RangeVariable;
                    if (rangeVariable != null)
                    {
                        this.variables.Value.Add(rangeVariable);
                    }
                }

                return this.variables.Value;
            }
        }

        #endregion Public Override Properties

        #region Public Properties

        /// <summary>
        /// Gets the variable that ranges over the values in the query result.
        /// </summary>
        public IVariable RangeVariable
        {
            get
            {
                this.ValidateEditVersion();

                if (!this.rangeVariable.Initialized)
                {
                    this.Initialize();
                    CsLanguageService.Debug.Assert(this.rangeVariable.Value != null, "Failed to initialize");
                }

                return this.rangeVariable.Value;
            }
        }

        /// <summary>
        /// Gets the range expression.
        /// </summary>
        public Expression Expression
        {
            get
            {
                this.ValidateEditVersion();

                if (!this.expression.Initialized)
                {
                    this.Initialize();
                    CsLanguageService.Debug.Assert(this.expression.Value != null, "Failed to initialize");
                }

                return this.expression.Value;
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

            this.rangeVariable.Reset();
            this.variables.Reset();
            this.expression.Reset();
        }

        #endregion Protected Override Methods

        #region Private Methods

        /// <summary>
        /// Initializes the contents of the class.
        /// </summary>
        private void Initialize()
        {
            // Find the 'let' keyword.
            LetToken let = this.FindFirstChild<LetToken>();
            if (let == null)
            {
                throw new SyntaxException(this.Document, this.LineNumber);
            }

            Token type = let.FindNextSiblingToken();
            if (type == null)
            {
                throw new SyntaxException(this.Document, this.LineNumber);
            }

            this.rangeVariable.Value = ExtractQueryVariable(type, true, true);

            EqualsOperator equals = type.FindNextSibling<EqualsOperator>();
            if (equals == null)
            {
                throw new SyntaxException(this.Document, this.LineNumber);
            }

            this.expression.Value = equals.FindNextSiblingExpression();
            if (this.expression.Value == null)
            {
                throw new SyntaxException(this.Document, this.LineNumber);
            }
        }

        #endregion Private Methods
    }
}
