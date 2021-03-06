﻿//-----------------------------------------------------------------------
// <copyright file="Comment.cs">
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
    /// Describes a comment.
    /// </summary>
    /// <subcategory>lexicalelement</subcategory>
    public abstract class Comment : SimpleLexicalElement
    {
        #region Internal Static Fields

        /// <summary>
        /// An empty array of comments.
        /// </summary>
        internal static readonly Comment[] EmptyCommentArray = new Comment[] { };

        #endregion Internal Static Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the Comment class.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="text">The line text.</param>
        /// <param name="commentType">The type of the comment.</param>
        internal Comment(CsDocument document, string text, CommentType commentType)
            : base(document, (int)commentType)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(text, "text");
            Param.Ignore(commentType);

            this.Text = text;
            CsLanguageService.Debug.Assert(System.Enum.IsDefined(typeof(CommentType), this.CommentType), "The type is invalid.");
        }

        /// <summary>
        /// Initializes a new instance of the Comment class.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="text">The line text.</param>
        /// <param name="commentType">The type of the comment.</param>
        /// <param name="location">The location of the comment in the code.</param>
        /// <param name="generated">Indicates whether the comment lies within a block of generated code.</param>
        internal Comment(CsDocument document, string text, CommentType commentType, CodeLocation location, bool generated)
            : base(document, (int)commentType, location, generated)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(text, "text");
            Param.Ignore(commentType);
            Param.Ignore(location);
            Param.Ignore(generated);

            this.Text = text;
            CsLanguageService.Debug.Assert(System.Enum.IsDefined(typeof(CommentType), this.CommentType), "The type is invalid.");
        }

        #endregion Internal Constructors

        #region Public Properties

        /// <summary>
        /// Gets the type of the comment.
        /// </summary>
        public CommentType CommentType
        {
            get
            {
                return (CommentType)(this.FundamentalType & (int)FundamentalTypeMasks.Comment);
            }
        }

        #endregion Public Properties
    }
}
