//-----------------------------------------------------------------------
// <copyright file="Rules.cs">
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

    /// <summary>
    /// The list of rules that can be triggered by this analyzer module.
    /// </summary>
    internal enum Rules
    {
        /// <summary>
        /// An exception occurred while parsing the file.
        /// </summary>
        ExceptionOccurred,

        /// <summary>
        /// A {0} exception occurred while saving the file {1}: {2}.
        /// </summary>
        SaveExceptionOccurred,

        /// <summary>
        /// An unknown error occurred while saving the file {0}.
        /// </summary>
        UnknownSaveExceptionOccurred
    }
}
