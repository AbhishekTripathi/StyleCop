//-----------------------------------------------------------------------
// <copyright file="IVariable.cs">
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
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Describes a field, variable or constant.
    /// </summary>
    /// <subcategory>other</subcategory>
    public interface IVariable
    {
        /// <summary>
        /// Gets the variable name.
        /// </summary>
        string VariableName
        {
            get;
        }

        /// <summary>
        /// Gets the variable type.
        /// </summary>
        TypeToken VariableType
        {
            get;
        }

        /// <summary>
        /// Gets the modifiers applied to this variable.
        /// </summary>
        VariableModifiers VariableModifiers
        {
            get;
        }

        /// <summary>
        /// Gets the location of the variable.
        /// </summary>
        CodeLocation Location
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the variable is located within a block of generated code.
        /// </summary>
        bool Generated
        {
            get;
        }
    }
}
