﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="http://stylecop.codeplex.com">
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
//   Utilities for many of our QuickFixes.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
extern alias JB;

namespace StyleCop.ReSharper600.Core
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    using JetBrains.Application;
    using JetBrains.Application.Progress;
    using JetBrains.DocumentManagers;
    using JetBrains.DocumentModel;
    using JetBrains.ProjectModel;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.Caches;
    using JetBrains.ReSharper.Psi.CodeStyle;
    using JetBrains.ReSharper.Psi.CSharp;
    using JetBrains.ReSharper.Psi.CSharp.CodeStyle;
    using JetBrains.ReSharper.Psi.CSharp.Parsing;
    using JetBrains.ReSharper.Psi.CSharp.Tree;
    using JetBrains.ReSharper.Psi.ExtensionsAPI;
    using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
    using JetBrains.ReSharper.Psi.Impl.CodeStyle;
    using JetBrains.ReSharper.Psi.Impl.Types;
    using JetBrains.ReSharper.Psi.Tree;
    using JetBrains.ReSharper.Psi.Util;
    using JetBrains.TextControl;

    using StyleCop.Diagnostics;
    using StyleCop.ReSharper600.Options;

    #endregion

    /// <summary>
    /// Utilities for many of our QuickFixes.
    /// </summary>
    internal class Utils
    {
        #region Public Static Fields

        /// <summary>
        /// This is an array of characters including all whitespace characters plus forward slash.
        /// </summary>
        public static readonly char[] TrimChars = new[]
                                                      {
                                                          '/', '\t', '\n', '\v', '\f', '\r', ' ', '\x0085', '\x00a0', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', 
                                                          ' ', '​', '\u2028', '\u2029', '　', '﻿'
                                                      };

        #endregion

        #region Constants

        private const string HeaderSummaryForDestructorXml = "Finalizes an instance of the <see cref=\"{0}\" /> class.";

        private const string HeaderSummaryForInstanceConstructorXml = "Initializes a new instance of the <see cref=\"{0}\" /> {1}.";

        private const string HeaderSummaryForPrivateInstanceConstructorXml = "Prevents a default instance of the <see cref=\"{0}\" /> {1} from being created.";

        private const string HeaderSummaryForStaticConstructorXml = "Initializes static members of the <see cref=\"{0}\" /> {1}.";

        #endregion

        #region Static Fields

        private static readonly NodeTypeSet OurNewLineTokens;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes static members of the <see cref="Utils"/> class. 
        /// </summary>
        static Utils()
        {
            OurNewLineTokens = new NodeTypeSet(new NodeType[] { CSharpTokenType.NEW_LINE });
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Calculates the number of line feeds occurring between the 2 nodes provided.
        /// </summary>
        /// <param name="node1">
        /// The first node to use.
        /// </param>
        /// <param name="node2">
        /// The second node to use.
        /// </param>
        /// <returns>
        /// The number of line feeds.
        /// </returns>
        public static int CalcLineFeedsBetween(ITreeNode node1, ITreeNode node2)
        {
            int lineFeedsBetween = 0;
            for (ITreeNode node = node1.NextSibling; node != node2; node = node.NextSibling)
            {
                if (node.IsNewLine())
                {
                    lineFeedsBetween++;
                }
            }

            return lineFeedsBetween;
        }

        /// <summary>
        /// Separates the pascal text with space.
        /// </summary>
        /// <param name="textToParse">
        /// The text to parse.
        /// </param>
        /// <returns>
        /// Formatted string.
        /// </returns>
        public static string ConvertTextToSentence(string textToParse)
        {
            string result = Regex.Replace(textToParse, "([^A-Z])([A-Z])", "$1 $2").Trim();
            result = Regex.Replace(result, "([A-Z])([A-Z])([^A-Z])", "$1 $2$3");
            result = Regex.Replace(result, "([A-Za-z])([0-9])", "$1 $2").Trim();
            result = Regex.Replace(result, "([0-9])([A-Za-z])", "$1 $2").Trim();

            return result;
        }

        /// <summary>
        /// Creates an ICSharpArgument for the argument passed in.
        /// </summary>
        /// <param name="psiModule">
        /// The PsiModule to use.
        /// </param>
        /// <param name="argument">
        /// The argument to create.
        /// </param>
        /// <returns>
        /// An instance of ICSharpArgument.
        /// </returns>
        public static ICSharpArgument CreateArgumentValueExpression(IPsiModule psiModule, string argument)
        {
            CSharpElementFactory factory = CSharpElementFactory.GetInstance(psiModule);
            return factory.CreateArgument(ParameterKind.VALUE, factory.CreateExpression("$0", new object[] { argument }));
        }

        /// <summary>
        /// Creates an ICSharpArgument for them argument passed in.
        /// </summary>
        /// <param name="psiModule">
        /// The PsiModule to use.
        /// </param>
        /// <param name="argument">
        /// The argument to create.
        /// </param>
        /// <returns>
        /// An instance of ICSharpArgument.
        /// </returns>
        public static ICSharpArgument CreateConstructorArgumentValueExpression(IPsiModule psiModule, string argument)
        {
            CSharpElementFactory factory = CSharpElementFactory.GetInstance(psiModule);
            return factory.CreateArgument(ParameterKind.VALUE, factory.CreateExpression("$0", new object[] { "\"" + argument + "\"" }));
        }

        /// <summary>
        /// Returns the text that the constructor should have from the containing type declaration with either with 'less than' and 'greater than' signs escaped or not.
        /// </summary>
        /// <param name="constructorDeclaration">
        /// The constructor to use.
        /// </param>
        /// <param name="encodeHtmlTags">
        /// If True then <see cref="ITypeParameterOfTypeDeclaration"/> will have {} instead of &lt; and &gt;.
        /// </param>
        /// <returns>
        /// A string of the text.
        /// </returns>
        public static string CreateConstructorDescriptionText(IConstructorDeclaration constructorDeclaration, bool encodeHtmlTags)
        {
            ICSharpTypeDeclaration containingTypeDeclaration = constructorDeclaration.GetContainingTypeDeclaration();
            string newName = constructorDeclaration.DeclaredName;
            if (containingTypeDeclaration.TypeParameters.Count > 0)
            {
                newName += encodeHtmlTags ? "{" : "<";
                for (int i = 0; i < containingTypeDeclaration.TypeParameters.Count; i++)
                {
                    ITypeParameterOfTypeDeclaration parameterDeclaration = containingTypeDeclaration.TypeParameters[i];
                    newName += parameterDeclaration.DeclaredName;
                    if (i < containingTypeDeclaration.TypeParameters.Count - 1)
                    {
                        newName += ",";
                    }
                }

                newName += encodeHtmlTags ? "}" : ">";
            }

            return newName;
        }

        /// <summary>
        /// Returns the text that the destructor should have from the containing type declaration with either with 'less than' and 'greater than' signs escaped or not.
        /// </summary>
        /// <param name="destructorDeclaration">
        /// The destructor to use.
        /// </param>
        /// <param name="encodeHtmlTags">
        /// If True then <see cref="ITypeParameterOfTypeDeclaration"/> will have {} instead of &lt; and &gt;.
        /// </param>
        /// <returns>
        /// A string of the text.
        /// </returns>
        public static string CreateDestructorDescriptionText(IDestructorDeclaration destructorDeclaration, bool encodeHtmlTags)
        {
            ICSharpTypeDeclaration containingTypeDeclaration = destructorDeclaration.GetContainingTypeDeclaration();
            string newName = destructorDeclaration.DeclaredName.Substring(1);
            if (containingTypeDeclaration.TypeParameters.Count > 0)
            {
                newName += encodeHtmlTags ? "{" : "<";
                for (int i = 0; i < containingTypeDeclaration.TypeParameters.Count; i++)
                {
                    ITypeParameterOfTypeDeclaration parameterDeclaration = containingTypeDeclaration.TypeParameters[i];
                    newName += parameterDeclaration.DeclaredName;
                    if (i < containingTypeDeclaration.TypeParameters.Count - 1)
                    {
                        newName += ",";
                    }
                }

                newName += encodeHtmlTags ? "}" : ">";
            }

            return newName;
        }

        /// <summary>
        /// Creates a DocCommentBlockNode with the text provided.
        /// </summary>
        /// <param name="element">
        /// The element to create for.
        /// </param>
        /// <param name="text">
        /// The text to use to create the doc.
        /// </param>
        /// <returns>
        /// An IDocCommentBlockNode that has been created.
        /// </returns>
        public static IDocCommentBlockNode CreateDocCommentBlockNode(ITreeNode element, string text)
        {
            // Fix up the xml terminators to remove the extra space.
            text = text.Replace(" />", "/>");

            StringBuilder builder = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                string outputLine = line;
                if (line.StartsWith(" "))
                {
                    outputLine = line.Substring(1);
                }

                builder.Append("/// " + outputLine + Environment.NewLine);
            }

            builder.Append("void fec();");
            IDocCommentBlockOwnerNode declaration =
                (IDocCommentBlockOwnerNode)CSharpElementFactory.GetInstance(element.GetPsiModule()).CreateTypeMemberDeclaration(builder.ToString());

            return declaration.GetDocCommentBlockNode();
        }

        /// <summary>
        /// Creates new documentation for the ParameterDeclaration or TypeParameterDeclaration.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to create the docs for.
        /// </param>
        /// <returns>
        /// A string of the parameters docs.
        /// </returns>
        public static string CreateDocumentationForParameter(ICSharpDeclaration declaration)
        {
            if (declaration is IParameterDeclaration)
            {
                string result = "<param name=\"" + declaration.DeclaredName + "\">";

                string parameterDescription = string.Empty;

                if (StyleCopOptions.Instance.InsertTextIntoDocumentation)
                {
                    parameterDescription = string.Format("The {0}.", ConvertTextToSentence(declaration.DeclaredName).ToLowerInvariant());
                }

                return result + parameterDescription + "</param>";
            }

            if (declaration is ITypeParameterDeclaration)
            {
                return CreateDocumentationForTypeParameterDeclaration((ITypeParameterDeclaration)declaration);
            }

            return null;
        }

        /// <summary>
        /// Creates new documentation for the ITypeParameterDeclaration.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to create the docs for.
        /// </param>
        /// <returns>
        /// A string of the type parameters docs.
        /// </returns>
        public static string CreateDocumentationForTypeParameterDeclaration(ITypeParameterDeclaration declaration)
        {
            return "<typeparam name=\"" + declaration.DeclaredName + "\"></typeparam>";
        }

        /// <summary>
        /// Creates a summary for the property.
        /// </summary>
        /// <param name="propertyDeclaration">
        /// The property declaration.
        /// </param>
        /// <returns>
        /// A String summary of the property.
        /// </returns>
        public static string CreateSummaryDocumentationForProperty(IPropertyDeclaration propertyDeclaration)
        {
            if (!StyleCopOptions.Instance.InsertTextIntoDocumentation)
            {
                return string.Empty;
            }

            IAccessor getter = propertyDeclaration.Getter();
            IAccessor setter = propertyDeclaration.Setter();
            string summaryText = string.Empty;

            string midText = Utils.IsPropertyBoolean(propertyDeclaration) ? "a value indicating whether " : string.Empty;

            if (getter != null)
            {
                summaryText = "Gets {0}{1}.";
            }

            if (setter != null)
            {
                AccessRights setterAccessRight = setter.GetAccessRights();

                if ((setterAccessRight == AccessRights.PRIVATE && propertyDeclaration.GetAccessRights() == AccessRights.PRIVATE)
                    || setterAccessRight == AccessRights.PUBLIC || setterAccessRight == AccessRights.PROTECTED || setterAccessRight == AccessRights.PROTECTED_OR_INTERNAL
                    || setterAccessRight == AccessRights.INTERNAL)
                {
                    if (string.IsNullOrEmpty(summaryText))
                    {
                        summaryText = "Sets {0}{1}.";
                    }
                    else
                    {
                        summaryText = "Gets or sets {0}{1}.";
                    }
                }
            }

            return string.Format(summaryText, midText, propertyDeclaration.DeclaredName);
        }

        /// <summary>
        /// Creates a new summary string for this constructor.
        /// </summary>
        /// <param name="constructorDeclaration">
        /// The constructor to produce the summary for.
        /// </param>
        /// <returns>
        /// A string of the constructor summary text.
        /// </returns>
        public static string CreateSummaryForConstructorDeclaration(IConstructorDeclaration constructorDeclaration)
        {
            DeclarationHeader declarationHeader = new DeclarationHeader(constructorDeclaration);

            if (declarationHeader.IsInherited)
            {
                return declarationHeader.XmlNode.InnerXml;
            }

            if (!StyleCopOptions.Instance.InsertTextIntoDocumentation)
            {
                return string.Empty;
            }

            bool parentIsStruct = Utils.IsContainingTypeAStruct(constructorDeclaration);

            string structOrClass = parentIsStruct ? "struct" : "class";

            string xmlWeShouldInsert;

            if (constructorDeclaration.IsStatic)
            {
                xmlWeShouldInsert = string.Format(CultureInfo.InvariantCulture, HeaderSummaryForStaticConstructorXml, constructorDeclaration.DeclaredName, structOrClass);
            }
            else if (constructorDeclaration.GetAccessRights() == AccessRights.PRIVATE && constructorDeclaration.ParameterDeclarations.Count == 0)
            {
                xmlWeShouldInsert = string.Format(
                    CultureInfo.InvariantCulture, HeaderSummaryForPrivateInstanceConstructorXml, constructorDeclaration.DeclaredName, structOrClass);
            }
            else
            {
                string constructorDescriptionText = Utils.CreateConstructorDescriptionText(constructorDeclaration, true);

                xmlWeShouldInsert = string.Format(CultureInfo.InvariantCulture, HeaderSummaryForInstanceConstructorXml, constructorDescriptionText, structOrClass);
            }

            return xmlWeShouldInsert;
        }

        /// <summary>
        /// Gets a string of the summary for this destructor.
        /// </summary>
        /// <param name="destructorDeclaration">
        /// The destructor to produce the summary for.
        /// </param>
        /// <returns>
        /// A string of the destructor summary text.
        /// </returns>
        public static string CreateSummaryForDestructorDeclaration(IDestructorDeclaration destructorDeclaration)
        {
            string summaryText = string.Empty;

            DeclarationHeader declarationHeader = new DeclarationHeader(destructorDeclaration);

            if (declarationHeader.IsInherited)
            {
                return declarationHeader.XmlNode.InnerXml;
            }

            if (declarationHeader.HasSummary)
            {
                summaryText = declarationHeader.SummaryXmlNode.InnerXml;
            }

            if (!StyleCopOptions.Instance.InsertTextIntoDocumentation)
            {
                return summaryText;
            }

            string destructorDescriptionText = Utils.CreateDestructorDescriptionText(destructorDeclaration, true);

            string newXmlText = string.Format(CultureInfo.InvariantCulture, HeaderSummaryForDestructorXml, destructorDescriptionText);

            return newXmlText + " " + summaryText;
        }

        /// <summary>
        /// Creates a valid value element for a property declaration.
        /// </summary>
        /// <param name="propertyDeclaration">
        /// The property declaration.
        /// </param>
        /// <returns>
        /// A valid value string for the property passed in.
        /// </returns>
        public static string CreateValueDocumentationForProperty(IPropertyDeclaration propertyDeclaration)
        {
            if (!StyleCopOptions.Instance.InsertTextIntoDocumentation)
            {
                return string.Empty;
            }

            return string.Format("<value>The {0}.</value>", Utils.ConvertTextToSentence(propertyDeclaration.DeclaredName).ToLower());
        }

        /// <summary>
        /// Executes the standard C Sharp reformatter on the line that this control is on.
        /// </summary>
        /// <param name="solution">
        /// The solution.
        /// </param>
        /// <param name="textControl">
        /// The text control.
        /// </param>
        public static void FormatLineForTextControl(ISolution solution, ITextControl textControl)
        {
            ITokenNode[] tokens = Utils.GetTokensForLineFromTextControl(solution, textControl).ToArray();
            if (tokens.Length > 0)
            {
                CSharpFormatterHelper.FormatterInstance.Format(tokens[0], tokens[tokens.Length - 1]);
            }
        }

        /// <summary>
        /// Executes the standard C Sharp reformatter on the lines passed in.
        /// </summary>
        /// <param name="solution">
        /// The solution.
        /// </param>
        /// <param name="document">
        /// The document.
        /// </param>
        /// <param name="startLine">
        /// The line to start the format of. Zero based.
        /// </param>
        /// <param name="endLine">
        /// The line to end the formatting.
        /// </param>
        public static void FormatLines(
            ISolution solution, 
            IDocument document, 
            JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> startLine, 
            JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> endLine)
        {
            JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> lineCount = document.GetLineCount();

            if (startLine < (JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine>)0)
            {
                startLine = (JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine>)0;
            }

            if (endLine > lineCount.Minus1())
            {
                endLine = lineCount.Minus1();
            }

            int startOffset = document.GetLineStartOffset(startLine);
            int endOffset = document.GetLineEndOffsetNoLineBreak(endLine);

            CSharpFormatterHelper.FormatterInstance.Format(
                solution, 
                new DocumentRange(document, new JB::JetBrains.Util.TextRange(startOffset, endOffset)), 
                SolutionCodeStyleSettings.GetInstance(solution).CodeStyleSettings, 
                CodeFormatProfile.DEFAULT, 
                true, 
                true, 
                NullProgressIndicator.Instance);
        }

        /// <summary>
        /// Get the CSharpFile holding this control in this solution.
        /// </summary>
        /// <param name="solution">
        /// The ISolution object to use.
        /// </param>
        /// <param name="textControl">
        /// The ITextControl to use.
        /// </param>
        /// <returns>
        /// The ICSharpFile to use.
        /// </returns>
        public static ICSharpFile GetCSharpFile(ISolution solution, ITextControl textControl)
        {
            return GetCSharpFile(solution, textControl.Document);
        }

        /// <summary>
        /// Get the CSharpFile holding this control in this solution.
        /// </summary>
        /// <param name="solution">
        /// The ISolution object to use.
        /// </param>
        /// <param name="document">
        /// The IDocument to use.
        /// </param>
        /// <returns>
        /// The ICSharpFile to use.
        /// </returns>
        public static ICSharpFile GetCSharpFile(ISolution solution, IDocument document)
        {
            PsiManager psiManager = PsiManager.GetInstance(solution);

            if (psiManager == null)
            {
                return null;
            }

            return psiManager.GetPsiFile(document, CSharpLanguage.Instance) as ICSharpFile;
        }

        /// <summary>
        /// Gets an IDeclaration that was closest to the ITextControl specified. It gets all the tokens on this line.
        /// Then moves through them looking for the first one that implements IDeclaration or has an immediate Parent that implements IDeclaration.
        /// </summary>
        /// <param name="solution">
        /// The solution.
        /// </param>
        /// <param name="textControl">
        /// The ITextControl.
        /// </param>
        /// <returns>
        /// An IDeclaration closest to the textControl.
        /// </returns>
        public static IDeclaration GetDeclarationClosestToTextControl(ISolution solution, ITextControl textControl)
        {
            IList<ITokenNode> tokens = GetTokensForLineFromTextControl(solution, textControl);

            foreach (ITokenNode tokenNode in tokens)
            {
                if (tokenNode is IDeclaration)
                {
                    return tokenNode as IDeclaration;
                }

                if (tokenNode.Parent is IDeclaration)
                {
                    return tokenNode.Parent as IDeclaration;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the DocCommentBlockNode for the declaration provided.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to check for docs.
        /// </param>
        /// <returns>
        /// Null if the declaration has no docs.
        /// </returns>
        public static IDocCommentBlockNode GetDocCommentBlockNodeForDeclaration(IDeclaration declaration)
        {
            IDeclaration treeNode = declaration;
            return (treeNode is IMultipleDeclarationMember)
                       ? SharedImplUtil.GetDocCommentBlockNode(((IMultipleDeclarationMember)treeNode).MultipleDeclaration)
                       : SharedImplUtil.GetDocCommentBlockNode(treeNode);
        }

        /// <summary>
        /// Gets the DocCommentBlockOwnerNode for the declaration provided.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to check for docs.
        /// </param>
        /// <returns>
        /// Null if the declaration has no docs.
        /// </returns>
        public static IDocCommentBlockOwnerNode GetDocCommentBlockOwnerNodeForDeclaration(IDeclaration declaration)
        {
            ITreeNode treeNode = declaration as ITreeNode;
            return treeNode is IMultipleDeclarationMember ? (IDocCommentBlockOwnerNode)treeNode.Parent : declaration as IDocCommentBlockOwnerNode;
        }

        /// <summary>
        /// Gets the element as the caret position. If the element is the new line character it returns the element immediately before it.
        /// </summary>
        /// <returns>
        /// The element.
        /// </returns>
        /// <param name="solution">
        /// The ISolution object to use.
        /// </param>
        /// <param name="textControl">
        /// The ITextControl to use.
        /// </param>
        public static ITreeNode GetElementAtCaret(ISolution solution, ITextControl textControl)
        {
            ICSharpFile file = Utils.GetCSharpFile(solution, textControl);

            if (file == null)
            {
                return null;
            }

            ITreeNode element = file.FindTokenAt(new TreeOffset(textControl.Caret.Offset()));

            if (element.IsNewLine())
            {
                element = file.FindTokenAt(new TreeOffset(textControl.Caret.Offset() - 1));
            }

            return element;
        }

        /// <summary>
        /// Gets the file header of the provided file.
        /// </summary>
        /// <param name="file">
        /// The file to get the header for.
        /// </param>
        /// <returns>
        /// The string of the header or string.Empty if there is no header.
        /// </returns>
        public static string GetFileHeader(ICSharpFile file)
        {
            return Utils.GetFileHeaderTreeRange(file).GetDocumentRange().GetText();
        }

        /// <summary>
        /// Gets the file header of the provided file.
        /// </summary>
        /// <param name="file">
        /// The file to get the header for.
        /// </param>
        /// <returns>
        /// The ITreeRange of the header or TreeRange.Empty if there is no header.
        /// </returns>
        public static ITreeRange GetFileHeaderTreeRange(ITreeNode file)
        {
            ITreeNode node = file.FirstChild;
            while ((node is ITokenNode) && node.GetTokenType().IsWhitespace)
            {
                node = node.NextSibling;
            }

            if (node is ICommentNode)
            {
                ITreeNode start = node;
                ICommentNode lastComment = node as ICommentNode;
                while ((node = node.NextSibling) != null)
                {
                    if (!(node is ITokenNode) || !node.GetTokenType().IsWhitespace)
                    {
                        if (node is ICSharpCommentNode)
                        {
                            ICSharpCommentNode n = node as ICSharpCommentNode;
                            if (n.CommentType == CommentType.END_OF_LINE_COMMENT)
                            {
                                lastComment = (ICommentNode)node;
                            }
                        }
                        else
                        {
                            if (lastComment == null || !(start is ICommentNode))
                            {
                                break;
                            }

                            return new TreeRange(start, lastComment);
                        }
                    }
                }
            }

            return TreeRange.Empty;
        }

        /// <summary>
        /// Gets the first non-whitespace token that occurs before the token passed in.
        /// </summary>
        /// <param name="tokenNode">
        /// The TokenNode to start at.
        /// </param>
        /// <returns>
        /// The first non-whitespace token.
        /// </returns>
        public static ITokenNode GetFirstNewLineTokenToLeft(ITokenNode tokenNode)
        {
            ITokenNode currentToken = tokenNode.GetPrevToken();
            while (!currentToken.IsNewLine() && currentToken != null)
            {
                currentToken = currentToken.GetPrevToken();
            }

            return currentToken;
        }

        /// <summary>
        /// Gets the first non-whitespace token that occurs after the token passed in.
        /// </summary>
        /// <param name="tokenNode">
        /// The TokenNode to start at.
        /// </param>
        /// <returns>
        /// The first non-whitespace token.
        /// </returns>
        public static ITokenNode GetFirstNewLineTokenToRight(ITokenNode tokenNode)
        {
            ITokenNode currentToken = tokenNode.GetNextToken();
            while (!currentToken.IsNewLine() && currentToken != null)
            {
                currentToken = currentToken.GetNextToken();
            }

            return currentToken;
        }

        /// <summary>
        /// Gets the first non white space character position from text sent.
        /// </summary>
        /// <param name="text">
        /// A <see cref="string"/> of text to search.
        /// </param>
        /// <returns>
        /// An <see cref="int"/> specifying position of non whitespace. -1 is returned if not found.
        /// </returns>
        public static int GetFirstNonWhitespaceCharacterPosition(string text)
        {
            Param.RequireValidString(text, "text");

            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            int textIndex = 0;

            while (textIndex < text.Length)
            {
                char ch = text[textIndex];
                int whitespaceIndex = 0;

                while (whitespaceIndex < TrimChars.Length)
                {
                    if (TrimChars[whitespaceIndex] == ch)
                    {
                        break;
                    }

                    whitespaceIndex++;
                }

                if (whitespaceIndex == TrimChars.Length)
                {
                    break;
                }

                textIndex++;
            }

            return textIndex;
        }

        /// <summary>
        /// Gets the first non-whitespace token that occurs before the token passed in.
        /// </summary>
        /// <param name="tokenNode">
        /// The TokenNode to start at.
        /// </param>
        /// <returns>
        /// The first non-whitespace token.
        /// </returns>
        public static ITokenNode GetFirstNonWhitespaceTokenToLeft(ITokenNode tokenNode)
        {
            ITokenNode currentToken = tokenNode.GetPrevToken();
            while (currentToken != null && currentToken.IsWhitespace())
            {
                currentToken = currentToken.GetPrevToken();
            }

            return currentToken;
        }

        /// <summary>
        /// Gets the first non-whitespace token that occurs after the token passed in.
        /// </summary>
        /// <param name="tokenNode">
        /// The TokenNode to start at.
        /// </param>
        /// <returns>
        /// The first non-whitespace token.
        /// </returns>
        public static ITokenNode GetFirstNonWhitespaceTokenToRight(ITokenNode tokenNode)
        {
            ITokenNode currentToken = tokenNode.GetNextToken();
            while (currentToken != null && currentToken.IsWhitespace())
            {
                currentToken = currentToken.GetNextToken();
            }

            return currentToken;
        }

        /// <summary>
        /// Gets the first type defined in the file in the first namespace or if no namespace then the first type otherwise null.
        /// </summary>
        /// <param name="file">
        /// The file to check.
        /// </param>
        /// <returns>
        /// The first type or null if it cannot be found.
        /// </returns>
        public static ICSharpTypeDeclaration GetFirstType(ICSharpFile file)
        {
            if (file == null)
            {
                return null;
            }

            if (file.NamespaceDeclarations.Count == 0)
            {
                return file.TypeDeclarations.Count == 0 ? null : file.TypeDeclarations[0];
            }

            ICSharpNamespaceDeclaration firstNamespace = file.NamespaceDeclarations[0];

            if (firstNamespace.TypeDeclarations.Count == 0)
            {
                return file.TypeDeclarations.Count == 0 ? null : file.TypeDeclarations[0];
            }

            return firstNamespace.TypeDeclarations[0];
        }

        /// <summary>
        /// Gets the name of first type defined in the file in the first namespace or if no namespace then the first type.
        /// </summary>
        /// <param name="file">
        /// The file to check.
        /// </param>
        /// <returns>
        /// The name of the first type or string.Empty if it cannot be found.
        /// </returns>
        public static string GetFirstTypeName(ICSharpFile file)
        {
            string returnValue = string.Empty;

            ICSharpTypeDeclaration typeDeclaration = Utils.GetFirstType(file);

            return typeDeclaration == null ? returnValue : typeDeclaration.DeclaredName;
        }

        /// <summary>
        /// Gets the last non white space character position from text sent.
        /// </summary>
        /// <param name="text">
        /// A <see cref="string"/> of text to search.
        /// </param>
        /// <returns>
        /// An <see cref="int"/> specifying position of non whitespace. -1 is returned if not found.
        /// </returns>
        public static int GetLastNonWhitespaceCharacterPosition(string text)
        {
            Param.RequireValidString(text, "text");

            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            int textIndex = text.Length - 1;

            while (textIndex >= 0)
            {
                char ch = text[textIndex];
                int whitespaceIndex = 0;

                while (whitespaceIndex < TrimChars.Length)
                {
                    if (TrimChars[whitespaceIndex] == ch)
                    {
                        break;
                    }

                    whitespaceIndex++;
                }

                if (whitespaceIndex == TrimChars.Length)
                {
                    break;
                }

                textIndex--;
            }

            return textIndex;
        }

        /// <summary>
        /// Gets the count of lines for the IProjectFile provided.
        /// </summary>
        /// <param name="projectFile">
        /// The project file to use.
        /// </param>
        /// <returns>
        /// An integer of the line count for the file.
        /// </returns>
        public static JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> GetLineCount(IProjectFile projectFile)
        {
            ISolution solution = projectFile.GetSolution();
            IDocument document = DocumentManager.GetInstance(solution).GetOrCreateDocument(projectFile);
            JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> lineCount;

            using (ReadLockCookie.Create())
            {
                lineCount = document.GetLineCount();
            }

            return lineCount;
        }

        /// <summary>
        /// Get the line number that the provided element is on. 0 based.
        /// </summary>
        /// <param name="element">
        /// The element to use.
        /// </param>
        /// <returns>
        /// An <see cref="int"/> of the line number. 0 based. -1 if the element was invalid.
        /// </returns>
        public static JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> GetLineNumberForElement(ITreeNode element)
        {
            DocumentRange range = element.GetDocumentRange();

            if (range == DocumentRange.InvalidRange)
            {
                JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> line = (JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine>)0;

                return line.Minus1();
            }

            return range.Document.GetCoordsByOffset(range.TextRange.StartOffset).Line;
        }

        /// <summary>
        /// Gets the line number that this ITextControl is on. Zero based.
        /// </summary>
        /// <param name="textControl">
        /// The control to use.
        /// </param>
        /// <returns>
        /// 0 based line number.
        /// </returns>
        public static JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> GetLineNumberForTextControl(ITextControl textControl)
        {
            return textControl.Caret.Position.Value.ToDocLineColumn().Line;
        }

        /// <summary>
        /// Returns the count of whitespace at the front of the line provided.
        /// </summary>
        /// <param name="tokenNode">
        /// The tokenNode to start at.
        /// </param>
        /// <returns>
        /// The count of the leftmost whitespace.
        /// </returns>
        public static int GetOffsetToStartOfLine(ITokenNode tokenNode)
        {
            ITokenNode firstTokenOnLine = Utils.GetFirstNewLineTokenToLeft(tokenNode).GetNextToken();

            TreeOffset startPosition = firstTokenOnLine.GetTreeStartOffset();

            return tokenNode.GetTreeStartOffset() - startPosition;
        }

        /// <summary>
        /// Returns an array of <see cref="CodeProject"/> which are required to fulfill the
        /// StyleCopCore.Analyze(IList{CodeProject}) method contract to parse the
        /// current file.
        /// </summary>
        /// <param name="core">
        /// StyleCopCore which performs the Source Analysis.
        /// </param>
        /// <param name="projectFile">
        /// The project file we are checking.
        /// </param>
        /// <param name="document">
        /// The document.
        /// </param>
        /// <returns>
        /// Returns an array of <see cref="CodeProject"/>.
        /// </returns>
        public static CodeProject[] GetProjects(StyleCopCore core, IProjectFile projectFile, IDocument document)
        {
            StyleCopTrace.In(core);

            // TODO We should load the configuration values for the project not just DEBUG and TRACE
            Configuration configuration = new Configuration(new[] { "DEBUG", "TRACE" });

            IList<IProjectFile> projectFiles = GetAllFilesForFile(projectFile);

            CodeProject[] codeProjects = new CodeProject[projectFiles.Count];
            int i = 0;

            foreach (IProjectFile projectfile in projectFiles)
            {
                string path = projectfile.Location.FullPath;

                CodeProject codeProject = new CodeProject(projectfile.GetHashCode(), path, configuration);

                string documentTextToPass = i == 0 ? document.GetText() : null;
                core.Environment.AddSourceCode(codeProject, path, documentTextToPass);

                codeProjects[i++] = codeProject;
            }

            return StyleCopTrace.Out(codeProjects);
        }

        /// <summary>
        /// Gets the StyleCop settings for the current selected project. 
        /// </summary>
        /// <returns>
        /// The settings.
        /// </returns>
        public static Settings GetStyleCopSettings()
        {
            ISolution solution = Shell.Instance.GetComponent<ISolutionManager>().CurrentSolution;

            if ((solution == null) || !Shell.HasInstance)
            {
                return null;
            }

            ITextControl control = Shell.Instance.Components.TextControlManager().FocusedTextControl.Value;

            if (control == null)
            {
                return null;
            }

            IProjectFile projectFile = DocumentManager.GetInstance(solution).GetProjectFile(control.Document);
            Settings settings = new StyleCopSettings(StyleCopCoreFactory.Create()).GetSettings(projectFile);

            return settings;
        }

        /// <summary>
        /// Gets the current summary element for the declaration provided or null if missing.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to get the summary for.
        /// </param>
        /// <returns>
        /// A string of the summary or null.
        /// </returns>
        public static string GetSummaryForDeclaration(IDeclaration declaration)
        {
            DeclarationHeader declarationHeader = new DeclarationHeader(declaration);

            if (declarationHeader.IsMissing || declarationHeader.IsInherited || !declarationHeader.HasSummary)
            {
                return null;
            }

            return declarationHeader.SummaryXmlNode.InnerXml.Trim();
        }

        /// <summary>
        /// Returns appropriate text for the file header summary element. It either uses text that it found from the summary of the first type in the file or if 
        /// that is not there it will use the name of the first type in the file.
        /// </summary>
        /// <param name="file">
        /// The file to produce the summary for.
        /// </param>
        /// <returns>
        /// A string of summary text. Empty if we're not inserting text from our settings.
        /// </returns>
        public static string GetSummaryText(ICSharpFile file)
        {
            if (!StyleCopOptions.Instance.InsertTextIntoDocumentation)
            {
                return string.Empty;
            }

            string fileName = file.GetSourceFile().ToProjectFile().Location.Name;

            string firstTypeName = Utils.GetFirstTypeName(file);

            string firstTypeSummaryText = Utils.GetSummaryForDeclaration(Utils.GetFirstType(file));

            string summaryText;

            if (string.IsNullOrEmpty(firstTypeSummaryText))
            {
                summaryText = string.IsNullOrEmpty(firstTypeName) ? fileName : string.Format("Defines the {0} type.", firstTypeName);
            }
            else
            {
                summaryText = firstTypeSummaryText;
            }

            return summaryText;
        }

        /// <summary>
        /// Gets the string from the passed in Doc Comment Xml.
        /// </summary>
        /// <param name="declarationNode">
        /// The xml to extract the text from.
        /// </param>
        /// <returns>
        /// A string of the xml passed in.
        /// </returns>
        public static string GetTextFromDeclarationHeader(XmlNode declarationNode)
        {
            StringBuilder builder = new StringBuilder();
            foreach (XmlNode node in declarationNode.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Text)
                {
                    builder.Append(node.Value);
                }
                else if (node.Name == "see" || node.Name == "seealso")
                {
                    XmlAttribute attribute = node.Attributes["cref"];
                    if (attribute != null)
                    {
                        builder.Append(StripClassName(attribute.Value));
                    }
                }
                else if (node.Name == "paramref")
                {
                    XmlAttribute attribute2 = node.Attributes["name"];
                    if (attribute2 != null)
                    {
                        builder.Append(attribute2.Value);
                    }
                }

                if (node.HasChildNodes && ((node.ChildNodes.Count > 0) || node.ChildNodes[0].NodeType != XmlNodeType.Text))
                {
                    builder.Append(GetTextFromDeclarationHeader(node));
                }
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Gets a TextRange for the ReSharper line number provided.
        /// </summary>
        /// <param name="projectFile">
        /// The project file to use.
        /// </param>
        /// <param name="resharperLineNumber">
        /// These are zero based.
        /// </param>
        /// <returns>
        /// A TextRange covering the line number specified.
        /// </returns>
        public static JB::JetBrains.Util.TextRange GetTextRange(
            IProjectFile projectFile, JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> resharperLineNumber)
        {
            using (ReadLockCookie.Create())
            {
                ISolution solution = projectFile.GetSolution();
                if (solution == null)
                {
                    return new JB::JetBrains.Util.TextRange();
                }

                IDocument document = DocumentManager.GetInstance(solution).GetOrCreateDocument(projectFile);

                return GetTextRangeForLineNumber(document, resharperLineNumber);
            }
        }

        /// <summary>
        /// Gets a TextRange covering the entire line specified.
        /// </summary>
        /// <param name="document">
        /// The document the line is in.
        /// </param>
        /// <param name="lineNumber">
        /// The line number to use. 0 based.
        /// </param>
        /// <returns>
        /// A TextRange for the line.
        /// </returns>
        public static JB::JetBrains.Util.TextRange GetTextRangeForLineNumber(
            IDocument document, JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> lineNumber)
        {
            using (ReadLockCookie.Create())
            {
                // must call GetLineCount first - it forces the line index to be built
                document.GetLineCount();
                int start = document.GetLineStartOffset(lineNumber);
                int end = document.GetLineEndOffsetNoLineBreak(lineNumber);
                return new JB::JetBrains.Util.TextRange(start, end);
            }
        }

        /// <summary>
        /// Gets a HashSet of tokens for the entire line specified.
        /// </summary>
        /// <param name="solution">
        /// The solution.
        /// </param>
        /// <param name="lineNumber">
        /// The line number.
        /// </param>
        /// <param name="document">
        /// The document.
        /// </param>
        /// <returns>
        /// A HashSet of tokens for this line.
        /// </returns>
        public static IList<ITokenNode> GetTokensForLine(
            ISolution solution, JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> lineNumber, IDocument document)
        {
            List<ITokenNode> tokens = new List<ITokenNode>();

            ICSharpFile file = Utils.GetCSharpFile(solution, document);

            using (ReadLockCookie.Create())
            {
                // must call GetLineCount first - it forces the line index to be built
                document.GetLineCount();
                int start = document.GetLineStartOffset(lineNumber);
                int end = document.GetLineEndOffsetNoLineBreak(lineNumber);

                for (int i = start; i < end; i++)
                {
                    ITokenNode t = (ITokenNode)file.FindTokenAt(new TreeOffset(i));
                    tokens.Add(t);
                }
            }

            return tokens;
        }

        /// <summary>
        /// Gets a HashSet of tokens for the entire line that the TextControl is on.
        /// </summary>
        /// <param name="solution">
        /// The solution.
        /// </param>
        /// <param name="textControl">
        /// The text control to get the line tokens for.
        /// </param>
        /// <returns>
        /// A HashSet of tokens for this line.
        /// </returns>
        public static IList<ITokenNode> GetTokensForLineFromTextControl(ISolution solution, ITextControl textControl)
        {
            JB::JetBrains.Util.dataStructures.TypedIntrinsics.Int32<DocLine> lineNumber = Utils.GetLineNumberForTextControl(textControl);
            return Utils.GetTokensForLine(solution, lineNumber, textControl.Document);
        }

        /// <summary>
        /// Gets an ITypeElement from the name passed in.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to use.
        /// </param>
        /// <param name="typeName">
        /// The type to create.
        /// </param>
        /// <returns>
        /// The ITypeElement created.
        /// </returns>
        public static ITypeElement GetTypeElement(IDeclaration declaration, string typeName)
        {
            return CacheManager.GetInstance(declaration.GetSolution()).GetDeclarationsCache(DeclarationCacheLibraryScope.FULL, true).GetTypeElementByCLRName(typeName);
        }

        /// <summary>
        /// Indicates if there is a line break between the 2 nodes.
        /// </summary>
        /// <param name="node1">
        /// The first node to check.
        /// </param>
        /// <param name="node2">
        /// The second node to check.
        /// </param>
        /// <returns>
        /// True if a line break between them.
        /// </returns>
        public static bool HasLineBreakBetween(ITreeNode node1, ITreeNode node2)
        {
            return FormatterImplHelper.HasTokenBetween(node1, node2, OurNewLineTokens);
        }

        /// <summary>
        /// Inserts a newline after the Node provided.
        /// </summary>
        /// <param name="currentNode">
        /// The node to insert after.
        /// </param>
        /// <returns>
        /// An ITreeNode that has been inserted.
        /// </returns>
        public static ITreeNode InsertNewLineAfter2(ITreeNode currentNode)
        {
            string newText = Environment.NewLine;
            LeafElementBase leafElement = TreeElementFactory.CreateLeafElement(CSharpTokenType.NEW_LINE, new JB::JetBrains.Text.StringBuffer(newText), 0, newText.Length);
            LowLevelModificationUtil.AddChildAfter(currentNode, new[] { leafElement });
            return leafElement;
        }

        /// <summary>
        /// Inserts a newline in front of the Node provided.
        /// </summary>
        /// <param name="currentNode">
        /// The node to insert in front of.
        /// </param>
        /// <returns>
        /// The inserted ITreeNode.
        /// </returns>
        public static ITreeNode InsertNewLineBefore2(ITreeNode currentNode)
        {
            string newText = Environment.NewLine;
            LeafElementBase leafElement = TreeElementFactory.CreateLeafElement(CSharpTokenType.NEW_LINE, new JB::JetBrains.Text.StringBuffer(newText), 0, newText.Length);
            LowLevelModificationUtil.AddChildBefore(currentNode, new[] { leafElement });
            return leafElement;
        }

        /// <summary>
        /// Inserts the number of whitespaces after the node.
        /// </summary>
        /// <param name="currentNode">
        /// The node to insert after.
        /// </param>
        /// <param name="count">
        /// The number of spaces to insert.
        /// </param>
        /// <returns>
        /// An ITreeNode that was inserted.
        /// </returns>
        public static ITreeNode InsertWhitespaceAfter(ITreeNode currentNode, int count)
        {
            string newText = " ".PadLeft(count);
            LeafElementBase leafElement = TreeElementFactory.CreateLeafElement(
                CSharpTokenType.WHITE_SPACE, new JB::JetBrains.Text.StringBuffer(newText), 0, newText.Length);

            using (WriteLockCookie.Create(true))
            {
                LowLevelModificationUtil.AddChildAfter(currentNode, new[] { leafElement });
            }

            return leafElement;
        }

        /// <summary>
        /// Inserts the number of whitespaces before the node.
        /// </summary>
        /// <param name="currentNode">
        /// The node to insert in front of.
        /// </param>
        /// <param name="count">
        /// The number of spaces to insert.
        /// </param>
        /// <returns>
        /// An ITreeNode that was inserted.
        /// </returns>
        public static ITreeNode InsertWhitespaceBefore(ITreeNode currentNode, int count)
        {
            string newText = " ".PadLeft(count);
            LeafElementBase leafElement = TreeElementFactory.CreateLeafElement(
                CSharpTokenType.WHITE_SPACE, new JB::JetBrains.Text.StringBuffer(newText), 0, newText.Length);

            using (WriteLockCookie.Create(true))
            {
                LowLevelModificationUtil.AddChildBefore(currentNode, new[] { leafElement });
            }

            return leafElement;
        }

        /// <summary>
        /// Indicates whether the type of the constructor passed in is a struct.
        /// </summary>
        /// <param name="constructorDeclaration">
        /// The constructor of the type to check.
        /// </param>
        /// <returns>
        /// True if containing type is a struct.
        /// </returns>
        public static bool IsContainingTypeAStruct(IConstructorDeclaration constructorDeclaration)
        {
            ICSharpTypeDeclaration typeDeclaration = constructorDeclaration.GetContainingTypeDeclaration();

            return typeDeclaration is IStructDeclaration;
        }

        /// <summary>
        /// Indicates if the node is the first on its line.
        /// </summary>
        /// <param name="node">
        /// The node to check.
        /// </param>
        /// <returns>
        /// True if the node is the first on the line.
        /// </returns>
        public static bool IsFirstNodeOnLine(ITreeNode node)
        {
            ITreeNode leftNode = node.FindFormattingRangeToLeft();

            return leftNode == null || HasLineBreakBetween(leftNode, node);
        }

        /// <summary>
        /// Indicates whether the property is a Boolean type.
        /// </summary>
        /// <param name="propertyDeclaration">
        /// The property to check.
        /// </param>
        /// <returns>
        /// True if the property is a Boolean.
        /// </returns>
        public static bool IsPropertyBoolean(IPropertyDeclaration propertyDeclaration)
        {
            if (propertyDeclaration.DeclaredElement == null)
            {
                return false;
            }

            DeclaredTypeFromCLRName declaredType = propertyDeclaration.DeclaredElement.Type as DeclaredTypeFromCLRName;

            if (declaredType == null)
            {
                return false;
            }

            return declaredType.GetClrName().FullName == "System.Boolean";
        }

        /// <summary>
        /// If the declaration or its parent has the rule provided suppressed it returns true.
        /// </summary>
        /// <param name="declaration">
        /// The declaration to check.
        /// </param>
        /// <param name="ruleId">
        /// The ruleId to see if its suppressed.
        /// </param>
        /// <returns>
        /// True if suppressed.
        /// </returns>
        public static bool IsRuleSuppressed(IDeclaration declaration, string ruleId)
        {
            IAttributesOwnerDeclaration attributesOwnerDeclaration = declaration as IAttributesOwnerDeclaration;

            if (IsRuleSuppressedInternal(attributesOwnerDeclaration, ruleId))
            {
                return true;
            }

            attributesOwnerDeclaration = declaration.GetContainingNode<ICSharpTypeDeclaration>(false);

            return IsRuleSuppressedInternal(attributesOwnerDeclaration, ruleId);
        }

        /// <summary>
        /// Removes whitespace from multiline comments and pads the result correctly.
        /// </summary>
        /// <param name="comment">
        /// The comment to split and pad.
        /// </param>
        /// <param name="whitespacePadding">
        /// How much padding to use after forward slashes.
        /// </param>
        /// <param name="commentType">
        /// Either single single comment or a doc comment.
        /// </param>
        /// <returns>
        /// The trimmed and padded string.
        /// </returns>
        public static string RemoveBlankLinesFromMultiLineStringComment(string comment, int whitespacePadding, CommentType commentType)
        {
            string[] splitString = comment.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            IEnumerable<string> newComment = from s in splitString let trimmedString = s.Trim(Utils.TrimChars) where trimmedString != string.Empty select trimmedString;

            string commentStart = commentType == CommentType.END_OF_LINE_COMMENT ? "//" : string.Empty;
            return newComment.JoinWith(string.Format("{0}{1}{2}", Environment.NewLine, commentStart, new string(' ', whitespacePadding)));
        }

        /// <summary>
        /// Removes the first blank line occurring before the node passed in.
        /// </summary>
        /// <param name="node">
        /// The node to start at.
        /// </param>
        public static void RemoveNewLineBefore(ITreeNode node)
        {
            ITokenNode currentToken;

            ITokenNode token = GetNextTokenNode(node);

            for (currentToken = token; currentToken != null; currentToken = currentToken.GetPrevToken())
            {
                if (currentToken is IWhitespaceNode)
                {
                    if (currentToken.IsNewLine())
                    {
                        ITokenNode prevToken = currentToken.GetPrevToken();
                        if (prevToken is IWhitespaceNode)
                        {
                            if (prevToken.IsNewLine())
                            {
                                using (WriteLockCookie.Create(true))
                                {
                                    LowLevelModificationUtil.DeleteChild(prevToken);
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the last class name from the string passed in.
        /// </summary>
        /// <param name="fullClassName">
        /// THe full class name to strip the leaf from.
        /// </param>
        /// <returns>
        /// A String of the class name.
        /// </returns>
        public static string StripClassName(string fullClassName)
        {
            int length = fullClassName.LastIndexOf('.');
            if (length > 0)
            {
                fullClassName = fullClassName.Substring(length + 1, (fullClassName.Length - length) - 1);
            }

            return fullClassName;
        }

        /// <summary>
        /// Swap generic type to documentation.
        /// </summary>
        /// <param name="typeDeclaration">
        /// The type declaration.
        /// </param>
        /// <returns>
        /// The swap generic type to documentation.
        /// </returns>
        public static string SwapGenericTypeToDocumentation(IEnumerable<char> typeDeclaration)
        {
            // if typeDeclaration is ThisIsSomeText<int> then I need ThisIsSomeText{T}
            // if typeDeclaration is ThisIsSomeText      then I need ThisIsSomeText
            // if typeDeclaration is ThisIsSomeText<int, int> then I need ThisIsSomeText{T, T}
            StringBuilder returnValue = new StringBuilder();

            bool insideBrackets = false;

            foreach (char c in typeDeclaration)
            {
                if (c == '<')
                {
                    returnValue.Append("{");
                    insideBrackets = true;
                    continue;
                }

                if (c == ',')
                {
                    returnValue.Append("T, ");
                    continue;
                }

                if (c == '>' && insideBrackets)
                {
                    returnValue.Append("T}");
                    insideBrackets = false;
                    continue;
                }

                if (!insideBrackets)
                {
                    returnValue.Append(c);
                }
            }

            return returnValue.ToString();
        }

        /// <summary>
        /// True if the token is preceded on the same line by a non-whitespace token.
        /// </summary>
        /// <param name="tokenNode">
        /// THe token to start at.
        /// </param>
        /// <returns>
        /// True or false.
        /// </returns>
        public static bool TokenHasNonWhitespaceTokenToLeftOnSameLine(ITokenNode tokenNode)
        {
            ITokenNode currentToken = tokenNode.GetPrevToken();
            if (currentToken == null)
            {
                return false;
            }

            while (currentToken.IsWhitespace() && !currentToken.IsNewLine() && currentToken != null)
            {
                currentToken = currentToken.GetPrevToken();
            }

            return currentToken != null && !currentToken.IsNewLine();
        }

        /// <summary>
        /// True if the token is followed on the same line with a non-whitespace token.
        /// </summary>
        /// <param name="tokenNode">
        /// THe token to start at.
        /// </param>
        /// <returns>
        /// True or false.
        /// </returns>
        public static bool TokenHasNonWhitespaceTokenToRightOnSameLine(ITokenNode tokenNode)
        {
            ITokenNode currentToken = tokenNode.GetNextToken();
            if (currentToken == null)
            {
                return false;
            }

            while (currentToken.IsWhitespace() && !currentToken.IsNewLine() && currentToken != null)
            {
                currentToken = currentToken.GetNextToken();
            }

            return currentToken != null && !currentToken.IsNewLine();
        }

        /// <summary>
        /// Gets a trimmed DocumentRange for the DocumentRange provided.
        /// </summary>
        /// <param name="documentRange">
        /// The DocumentRange to use.
        /// </param>
        /// <returns>
        /// A DocumentRange with whitespace removed from the left and right.
        /// </returns>
        public static DocumentRange TrimWhitespaceFromDocumentRange(DocumentRange documentRange)
        {
            using (ReadLockCookie.Create())
            {
                JB::JetBrains.Util.TextRange textRange = documentRange.TextRange;
                string text = documentRange.GetText();
                int newLeft = CountOfWhitespaceAtLeft(text);
                int newRight = CountOfWhitespaceAtRight(text);
                JB::JetBrains.Util.TextRange a = textRange.TrimLeft(newLeft);

                return new DocumentRange(documentRange.Document, a.TrimRight(newRight));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Count the number of whitespace characters at the left of the string.
        /// </summary>
        /// <param name="s">
        /// The string to count the whitespace in.
        /// </param>
        /// <returns>
        /// An <see cref="int"/> of the number of whitespace characters.
        /// </returns>
        private static int CountOfWhitespaceAtLeft(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsWhiteSpace(c))
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Count the number of whitespace characters at the right of the string.
        /// </summary>
        /// <param name="s">
        /// The string to count the whitespace in.
        /// </param>
        /// <returns>
        /// An <see cref="int"/> of the number of whitespace characters.
        /// </returns>
        private static int CountOfWhitespaceAtRight(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            for (int i = s.Length - 1; i >= 0; i--)
            {
                char c = s[i];
                if (!char.IsWhiteSpace(c))
                {
                    return s.Length - i - 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// The passed in file is always index 0 in the returned IList.
        /// </summary>
        /// <param name="projectFile">
        /// The IProjectFile to get the files for.
        /// </param>
        /// <returns>
        /// A List of Project files.
        /// </returns>
        private static IList<IProjectFile> GetAllFilesForFile(IProjectFile projectFile)
        {
            IProjectFile rootDependsItem = projectFile.GetDependsUponItemForItem();

            if (rootDependsItem == null)
            {
                return new List<IProjectFile> { projectFile };
            }

            ICollection<IProjectFile> dependentFiles = rootDependsItem.GetDependentFiles();

            List<IProjectFile> list = new List<IProjectFile> { projectFile };

            list.AddRange(dependentFiles.Where(file => !file.Equals(projectFile)));

            return list;
        }

        /// <summary>
        /// Returns the next TreeNode that is of type ITokenNode or null if not found.
        /// </summary>
        /// <param name="node">
        /// The node to start at.
        /// </param>
        /// <returns>
        /// A TokenNode.
        /// </returns>
        private static ITokenNode GetNextTokenNode(ITreeNode node)
        {
            while (!(node is ITokenNode) && node != null)
            {
                node = node.NextSibling;
            }

            return (ITokenNode)node;
        }

        /// <summary>
        /// If the declaration or its parent has the rule provided suppressed it returns true.
        /// </summary>
        /// <param name="attributesOwnerDeclaration">
        /// The declaration to check.
        /// </param>
        /// <param name="ruleId">
        /// The ruleId to see if its suppressed.
        /// </param>
        /// <returns>
        /// True if suppressed.
        /// </returns>
        private static bool IsRuleSuppressedInternal(IAttributesOwnerDeclaration attributesOwnerDeclaration, string ruleId)
        {
            if (attributesOwnerDeclaration != null)
            {
                CSharpElementFactory factory = CSharpElementFactory.GetInstance(attributesOwnerDeclaration.GetPsiModule());

                ITypeElement typeElement = Utils.GetTypeElement(attributesOwnerDeclaration, "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute");

                IAttribute attribute = factory.CreateAttribute(typeElement);

                foreach (IAttribute s in attributesOwnerDeclaration.Attributes)
                {
                    if (s.Name.ShortName == attribute.Name.ShortName)
                    {
                        ICSharpLiteralExpression b = s.ConstructorArgumentExpressions[1] as ICSharpLiteralExpression;

                        if (b == null)
                        {
                            return false;
                        }

                        string d = b.GetText();

                        if (string.IsNullOrEmpty(d))
                        {
                            return false;
                        }

                        string e = d.Trim('\"').Substring(2); // removes the 'SA' bit

                        string f = ruleId.Substring(2); // removes the 'SA' bit

                        return e == f;
                    }
                }
            }

            return false;
        }

        #endregion
    }
}