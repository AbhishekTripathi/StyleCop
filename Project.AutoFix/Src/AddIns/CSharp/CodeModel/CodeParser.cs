//-----------------------------------------------------------------------
// <copyright file="CodeParser.cs">
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
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    /// <summary>
    /// Parses a C# code file.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Design", 
        "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "The class does not create anything that it should dispose.")]
    [SuppressMessage(
        "Microsoft.Maintainability", 
        "CA1506:AvoidExcessiveClassCoupling", 
        Justification = "Class is split across multiple files for added maintainability.")]
    internal partial class CodeParser
    {
        #region Private Fields

        /// <summary>
        /// The code lexer.
        /// </summary>
        private CodeLexer lexer;

        /// <summary>
        /// The document being parsed.
        /// </summary>
        private CsDocument document;

        /// <summary>
        /// The C# language service.
        /// </summary>
        private CsLanguageService languageService;

        /// <summary>
        /// The collection of symbols in the document.
        /// </summary>
        private SymbolManager symbols;

        /// <summary>
        /// The optional preprocessor flags.
        /// </summary>
        private IDictionary<string, object> preprocessorDefinitions;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the CodeParser class.
        /// </summary>
        /// <param name="languageService">The C# language service.</param>
        /// <param name="lexer">The lexer to use for parsing the code.</param>
        /// <param name="preprocessorDefinitions">The optional preprocessor flags.</param>
        public CodeParser(CsLanguageService languageService, CodeLexer lexer, Dictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(languageService, "languageService");
            Param.AssertNotNull(lexer, "lexer");
            Param.Ignore(preprocessorDefinitions);

            this.languageService = languageService;
            this.lexer = lexer;
            this.preprocessorDefinitions = preprocessorDefinitions;
        }

        /// <summary>
        /// Initializes a new instance of the CodeParser class.
        /// </summary>
        /// <param name="languageService">The C# language service.</param>
        /// <param name="document">The document being parsed.</param>
        /// <param name="symbols">The symbols in the document to parse.</param>
        /// <param name="preprocessorDefinitions">The optional preprocessor flags.</param>
        public CodeParser(CsLanguageService languageService, CsDocument document, SymbolManager symbols, IDictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(languageService, "languageService");
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(symbols, "symbols");
            Param.Ignore(preprocessorDefinitions);

            this.languageService = languageService;
            this.document = document;
            this.symbols = symbols;
            this.preprocessorDefinitions = preprocessorDefinitions;
        }

        #endregion Public Constructors

        #region Private Enums

        /// <summary>
        /// The types of symbols to skip while advancing past symbols.
        /// </summary>
        [Flags]
        private enum SkipSymbols
        {
            /// <summary>
            /// Don't skip any symbols.
            /// </summary>
            None = 0x00,

            /// <summary>
            /// Skip past all whitespace symbols.
            /// </summary>
            WhiteSpace = 0x01,

            /// <summary>
            /// Skip past all end-of-line characters.
            /// </summary>
            EndOfLine = 0x02,

            /// <summary>
            /// Skip past all single-line comments.
            /// </summary>
            SingleLineComment = 0x04,

            /// <summary>
            /// Skip past all multi-line comments.
            /// </summary>
            MultiLineComment = 0x08,

            /// <summary>
            /// Skip past all Xml header lines.
            /// </summary>
            XmlHeader = 0x10,

            /// <summary>
            /// Skip past all preprocessor directives.
            /// </summary>
            Preprocessor = 0x20,

            /// <summary>
            /// Skip past all of these types of symbols.
            /// </summary>
            All = 0xFF
        }

        #endregion Private Enums

        #region Public Properties

        /// <summary>
        /// Gets the parsed document.
        /// </summary>
        public CsDocument Document
        {
            get
            {
                return this.document;
            }
        }

        #endregion Public Properties

        #region Internal Static Methods

        /// <summary>
        /// Finds the end of a name, moving past member access operators.
        /// </summary>
        /// <param name="document">The document containing the tokens.</param>
        /// <param name="codeUnit">The code unit.</param>
        /// <param name="startToken">The first token of the name.</param>
        /// <param name="endToken">Returns the last token of the name.</param>
        /// <returns>Returns the full name.</returns>
        internal static string GetFullName(CsDocument document, CodeUnit codeUnit, Token startToken, out Token endToken)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(codeUnit, "codeUnit");
            Param.AssertNotNull(startToken, "start");

            endToken = CodeParser.FindEndOfName(document, codeUnit, startToken);
            CsLanguageService.Debug.Assert(endToken != null, "Did not find the end of the name");

            // Create the text string.
            var text = new StringBuilder();

            Token token = startToken;
            while (token != null)
            {
                text.Append(token.Text);

                if (token == endToken)
                {
                    break;
                }

                token = token.FindNextSiblingToken();
            }

            return text.ToString();
        }

        /// <summary>
        /// Finds the end of the given name, moving past member access operators.
        /// </summary>
        /// <param name="document">The document containing the name.</param>
        /// <param name="codeUnit">The code unit.</param>
        /// <param name="startToken">The first token of the name.</param>
        /// <returns>Returns the last token of the name within the token list.</returns>
        internal static Token FindEndOfName(CsDocument document, CodeUnit codeUnit, Token startToken)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(codeUnit, "codeUnit");
            Param.AssertNotNull(startToken, "startToken");

            Token endToken = startToken;

            bool accessSymbol = false;

            for (Token temp = startToken; temp != null; temp = temp.FindNextSiblingToken())
            {
                if (accessSymbol)
                {
                    if (temp.TokenType != TokenType.Literal)
                    {
                        throw new SyntaxException(document, temp.LineNumber);
                    }

                    endToken = temp;
                    accessSymbol = false;
                }
                else
                {
                    if (temp.Text == "." || temp.Text == "::")
                    {
                        accessSymbol = true;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return endToken;
        }

        /// <summary>
        /// Adds parameters to the fully qualified name of the item.
        /// </summary>
        /// <param name="parameters">The list of parameters on the element.</param>
        /// <param name="fullyQualifiedName">The fully qualified name of the element.</param>
        /// <returns>Returns the new fully qualified name string.</returns>
        internal static string AddQualifications(IEnumerable<Parameter> parameters, string fullyQualifiedName)
        {
            Param.Ignore(parameters);
            Param.AssertNotNull(fullyQualifiedName, "fullyQualifiedName");

            if (parameters != null)
            {
                foreach (Parameter parameter in parameters)
                {
                    fullyQualifiedName += "%" + RemoveWhitespace(parameter.ParameterType);
                }
            }

            return fullyQualifiedName;
        }

        /// <summary>
        /// Removes whitespace from the given string.
        /// </summary>
        /// <param name="original">The original string.</param>
        /// <returns>Returns the modified string.</returns>
        internal static string RemoveWhitespace(string original)
        {
            Param.Ignore(original);

            if (original == null)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder(original.Length);
            for (int i = 0; i < original.Length; ++i)
            {
                char character = original[i];
                if (!char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Given a string containing a type, trims out all whitespace and comments from the type string. 
        /// </summary>
        /// <param name="type">The original type string.</param>
        /// <returns>Returns the trimmed string.</returns>
        internal static string TrimType(string type)
        {
            Param.Ignore(type);

            if (type == null)
            {
                return null;
            }

            int index = 0;
            char[] chars = new char[type.Length];

            bool multiLineComment = false;
            bool singleLineComment = false;

            for (int i = 0; i < type.Length; ++i)
            {
                char typeCharacter = type[i];

                if (singleLineComment)
                {
                    // When we're in a single-line comment, ignore everything up through the end of the line.
                    if (typeCharacter == '\r' || typeCharacter == '\n')
                    {
                        singleLineComment = false;
                    }
                }
                else if (multiLineComment)
                {
                    // When we're in a multi-line comment, ignore everythrough up through the end of the comment.
                    if (typeCharacter == '*' && i < type.Length - 1 && type[i + 1] == '/')
                    {
                        ++i;
                        multiLineComment = false;
                    }
                }
                else if (!char.IsWhiteSpace(typeCharacter))
                {
                    // Look for a comment.
                    if (typeCharacter == '/' && i < type.Length - 1)
                    {
                        if (type[i + 1] == '/')
                        {
                            singleLineComment = true;
                        }
                        else if (type[i + 1] == '*')
                        {
                            multiLineComment = true;
                        }
                    }

                    if (!singleLineComment && !multiLineComment)
                    {
                        chars[index++] = typeCharacter;
                    }
                }
            }

            // If nothing was trimmed, just return the original string.
            if (index == type.Length)
            {
                return type;
            }

            return new string(chars, 0, index);
        }

        /// <summary>
        /// Extracts a TypeToken from the literal expression, assuming that one exists.
        /// </summary>
        /// <param name="literal">The literal expression.</param>
        /// <returns>Returns the type token.</returns>
        internal static TypeToken ExtractTypeTokenFromLiteralExpression(LiteralExpression literal)
        {
            Param.AssertNotNull(literal, "literal");

            TypeToken type = literal.Token as TypeToken;
            if (type == null)
            {
                throw new SyntaxException(literal.Document, literal.LineNumber, Strings.LiteralExpressionDoesNotContainTypeToken);
            }

            return type;
        }

        #endregion Internal Static Methods

        #region Internal Methods

        /// <summary>
        /// Parses the contents of the document.
        /// </summary>
        internal void ParseCodeModel()
        {
            CsLanguageService.Debug.Assert(this.document == null, "This method is only designed to be called once.");

            // The parent reference to the document.
            var codeModelProxy = new CodeUnitProxy(null);

            // Create the document object.
            this.document = new CsDocument(codeModelProxy, this.lexer.SourceCode.Name, this.lexer.SourceCode.Path, this.languageService.PartialElementService);

            // Find the list of symbols in the document.
            List<Symbol> symbolList = this.lexer.GetSymbols(
                this.document, this.preprocessorDefinitions);

            // Create the symbol manager class.
            this.symbols = new SymbolManager(symbolList);

            // Get the file header if it exists.
            FileHeader fileHeader = this.GetFileHeader(codeModelProxy);

            // Let the symbol manager know if this document contains generated code.
            if (fileHeader.Generated)
            {
                this.symbols.IncrementGeneratedCodeBlocks();
            }

            // Parse the contents of the document.
            this.ParseElementContainerBody(codeModelProxy, this.document, false);

            // Perform a debug check to ensure that all code unit references have been set.
            ValidateCodeUnitReferences(this.document);
        }

        /// <summary>
        /// Parses an attribute.
        /// </summary>
        /// <param name="parentProxy">Proxy object for the parent item.</param>
        /// <param name="unsafeCode">Indicates whether the attribute lies within a block of unsafe code.</param>
        /// <returns>Returns the attribute.</returns>
        internal Attribute GetAttribute(CodeUnitProxy parentProxy, bool unsafeCode)
        {
            Param.AssertNotNull(parentProxy, "parentProxy");
            Param.Ignore(unsafeCode);

            this.AdvanceToNextCodeSymbol(parentProxy);

            // Get the first symbol and make sure it is the right type.
            Symbol firstSymbol = this.symbols.Peek(1);
            CsLanguageService.Debug.Assert(firstSymbol != null && firstSymbol.SymbolType == SymbolType.OpenSquareBracket, "Expected an opening square bracket");

            var attributeProxy = new CodeUnitProxy(this.document);

            // The list of attribute expressions in the attribute.
            var attributeExpressions = new List<AttributeExpression>();

            // Move past the opening square bracket.
            var openingBracket = new OpenAttributeBracketToken(this.document, firstSymbol.Text, firstSymbol.Location, this.symbols.Generated);

            attributeProxy.Children.Add(openingBracket);
            this.symbols.Advance();

            // Get each of the child attribute expressions within this attribute.
            while (true)
            {
                // Move to the next symbol.
                this.AdvanceToNextCodeSymbol(attributeProxy);
                Symbol symbol = this.symbols.Peek(1);
                if (symbol == null)
                {
                    throw new SyntaxException(this.document, firstSymbol.LineNumber);
                }

                // Check the type. If this is the closing bracket then we are done.
                if (symbol.SymbolType == SymbolType.CloseSquareBracket)
                {
                    var closingBracket = new CloseAttributeBracketToken(this.document, symbol.Text, symbol.Location, this.symbols.Generated);
                    attributeProxy.Children.Add(closingBracket);
                    this.symbols.Advance();

                    openingBracket.MatchingBracket = closingBracket;
                    closingBracket.MatchingBracket = openingBracket;

                    break;
                }

                // Check to see if there is a target specified.
                LiteralExpression target = null;

                this.AdvanceToNextCodeSymbol(attributeProxy);
                symbol = this.symbols.Peek(1);
                if (symbol == null)
                {
                    throw new SyntaxException(this.document, firstSymbol.LineNumber);
                }

                var attributeExpressionProxy = new CodeUnitProxy(this.document);

                if (symbol.SymbolType == SymbolType.Other || symbol.SymbolType == SymbolType.Return)
                {
                    // Peek ahead to the next symbol and check if it's a colon.
                    int index = this.GetNextCodeSymbolIndex(2);
                    if (index != -1)
                    {
                        Symbol colon = this.symbols.Peek(index);

                        if (colon.SymbolType == SymbolType.Colon)
                        {
                            // This is a target. Get the literal target expression and move past the colon.
                            // Change the type of the target symbol to OTHER so that it will be parsed
                            // correctly by the literal expression parser.
                            symbol.SymbolType = SymbolType.Other;

                            // Get the literal expression.
                            target = this.GetLiteralExpression(attributeExpressionProxy, unsafeCode);
                            CsLanguageService.Debug.Assert(target != null, "We should always succeed getting a Literal here since we have already seen a type of Other.");

                            // Add the colon.
                            this.AdvanceToNextCodeSymbol(attributeExpressionProxy);
                            attributeExpressionProxy.Children.Add(new AttributeColonToken(this.document, colon.Text, symbol.Location, this.symbols.Generated));
                            this.symbols.Advance();

                            this.AdvanceToNextCodeSymbol(attributeExpressionProxy);
                        }
                    }
                }

                // Now get the attribute call expression.
                Expression initialization = this.GetNextExpression(attributeExpressionProxy, ExpressionPrecedence.None, unsafeCode);
                if (initialization == null)
                {
                    throw new SyntaxException(this.document, firstSymbol.LineNumber);
                }

                // Create and add the attribute expression.
                var attributeExpression = new AttributeExpression(attributeExpressionProxy, target, initialization);
                attributeProxy.Children.Add(attributeExpression);
                attributeExpressions.Add(attributeExpression);

                // Get the next item, which must either be a comma or the closing attribute bracket.
                this.AdvanceToNextCodeSymbol(attributeProxy);
                symbol = this.symbols.Peek(1);
                if (symbol == null)
                {
                    throw new SyntaxException(this.document, firstSymbol.LineNumber);
                }

                if (symbol.SymbolType == SymbolType.Comma)
                {
                    // Add the comma and continue.
                    this.GetToken(attributeProxy, TokenType.Comma, SymbolType.Comma);
                }
                else if (symbol.SymbolType != SymbolType.CloseSquareBracket)
                {
                    // This type of symbol is unexpected.
                    throw new SyntaxException(this.document, firstSymbol.LineNumber);
                }
            }

            // Create and return the attribute.
            var attribute = new Attribute(attributeProxy, attributeExpressions.AsReadOnly());
            parentProxy.Children.Add(attribute);

            return attribute;
        }

        #endregion Internal Methods

        #region Private Static Methods

        /// <summary>
        /// Validates that all code unit references have been filled in.
        /// </summary>
        /// <param name="root">The root element.</param>
        [Conditional("DEBUG")]
        private static void ValidateCodeUnitReferences(CodeUnit root)
        {
            Param.AssertNotNull(root, "root");

            if (root != null)
            {
                root.ValidateCodeUnitReference(); 
                
                foreach (CodeUnit child in root.Children)
                {
                    ValidateCodeUnitReferences(child);
                }
            }
        }

        #endregion Private Static Methods

        #region Private Methods

        /// <summary>
        /// Gets and converts preprocessor directive.
        /// </summary>
        /// <param name="parentProxy">Represents the parent of the token.</param>
        /// <param name="preprocessorSymbol">The preprocessor symbol.</param>
        /// <param name="generated">Indicates whether the preprocessor directive lies within a block of generated code.</param>
        /// <returns>Returns the preprocessor directive.</returns>
        private PreprocessorDirective GetPreprocessorDirective(CodeUnitProxy parentProxy, Symbol preprocessorSymbol, bool generated)
        {
            Param.Ignore(parentProxy);
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.Ignore(generated);

            PreprocessorDirective preprocessor = this.PeekPreprocessorDirective(preprocessorSymbol, generated);

            if (parentProxy != null)
            {
                parentProxy.Children.Add(preprocessor);
            }

            return preprocessor;
        }

        /// <summary>
        /// Gets and converts preprocessor directive.
        /// </summary>
        /// <param name="preprocessorSymbol">The preprocessor symbol.</param>
        /// <param name="generated">Indicates whether the preprocessor directive lies within a block of generated code.</param>
        /// <returns>Returns the preprocessor directive.</returns>
        private PreprocessorDirective PeekPreprocessorDirective(Symbol preprocessorSymbol, bool generated)
        {
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.Ignore(generated);

            CsLanguageService.Debug.Assert(preprocessorSymbol != null && preprocessorSymbol.SymbolType == SymbolType.PreprocessorDirective, "Expected a preprocessor directive");

            // Get the type of the preprocessor directive.
            int bodyIndex;
            string type = CsLanguageService.GetPreprocessorDirectiveType(preprocessorSymbol, out bodyIndex);
            if (type == null)
            {
                throw new SyntaxException(this.document, preprocessorSymbol.LineNumber);
            }

            // Create the correct preprocessor object type.
            PreprocessorDirective preprocessor = null;
            if (type == "region")
            {
                var region = new RegionDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
                this.symbols.PushRegion(region);
                preprocessor = region;
            }
            else if (type == "endregion")
            {
                var endregion = new EndRegionDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
                RegionDirective startregion = this.symbols.PopRegion();

                if (startregion == null)
                {
                    throw new SyntaxException(this.document, preprocessorSymbol.LineNumber);
                }

                startregion.Partner = endregion;
                endregion.Partner = startregion;

                preprocessor = endregion;
            }
            else if (type == "if")
            {
                preprocessor = this.GetConditionalCompilationDirective(preprocessorSymbol, PreprocessorType.If, bodyIndex);
            }
            else if (type == "elif")
            {
                preprocessor = this.GetConditionalCompilationDirective(preprocessorSymbol, PreprocessorType.Elif, bodyIndex);
            }
            else if (type == "else")
            {
                preprocessor = this.GetConditionalCompilationDirective(preprocessorSymbol, PreprocessorType.Else, bodyIndex);
            }
            else if (type == "endif")
            {
                preprocessor = this.GetConditionalCompilationDirective(preprocessorSymbol, PreprocessorType.Endif, bodyIndex);
            }
            else if (type == "pragma")
            {
                preprocessor = new PragmaDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
            }
            else if (type == "define")
            {
                preprocessor = new DefineDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
            }
            else if (type == "undef")
            {
                preprocessor = new UndefDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
            }
            else if (type == "line")
            {
                preprocessor = new LineDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
            }
            else if (type == "error")
            {
                preprocessor = new ErrorDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
            }
            else if (type == "warning")
            {
                preprocessor = new WarningDirective(this.document, preprocessorSymbol.Text, preprocessorSymbol.Location, generated);
            }
            else
            {
                throw this.CreateSyntaxException();                
            }

            return preprocessor;
        }

        /// <summary>
        /// Gets the next conditional compilation directive from the code.
        /// </summary>
        /// <param name="preprocessorSymbol">The symbol representing the directive.</param>
        /// <param name="type">The type of the conditional compilation directive.</param>
        /// <param name="startIndex">The start index of the body of the directive.</param>
        /// <returns>Returns the directive.</returns>
        private ConditionalCompilationDirective GetConditionalCompilationDirective(Symbol preprocessorSymbol, PreprocessorType type, int startIndex)
        {
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.Ignore(type);
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            Expression body = null;

            var directiveProxy = new CodeUnitProxy(this.document);

            // Extract the body of the directive if necessary.
            if (type != PreprocessorType.Endif && startIndex < preprocessorSymbol.Text.Length)
            {
                body = CodeParser.GetConditionalPreprocessorBodyExpression(
                    this.document, this.lexer.SourceCode, directiveProxy, this.languageService, this.preprocessorDefinitions, preprocessorSymbol, startIndex);
            }

            // Create and return the directive.
            switch (type)
            {
                case PreprocessorType.If:
                    return new IfDirective(preprocessorSymbol.Text, directiveProxy, body);
                case PreprocessorType.Elif:
                    return new ElifDirective(preprocessorSymbol.Text, directiveProxy, body);
                case PreprocessorType.Else:
                    return new ElseDirective(preprocessorSymbol.Text, directiveProxy, body);
                case PreprocessorType.Endif:
                    return new EndifDirective(preprocessorSymbol.Text, directiveProxy, body);
                default:
                    CsLanguageService.Debug.Fail("Not a conditional preprocessor type.");
                    return null;
            }
        }

        /// <summary>
        /// Gets the file header from a piece of code. The file header must start on the first
        /// line of code, and it must follow the format shown below. This method will strip off
        /// the first and last lines, as well as the leading slashes on all lines, and return only
        /// the header text.
        /// </summary>
        /// <param name="parentProxy">Proxy object for the parent item.</param>
        /// <returns>Returns the file header.</returns>
        private FileHeader GetFileHeader(CodeUnitProxy parentProxy)
        {
            Param.AssertNotNull(parentProxy, "parentProxy");

            // Make sure that we are starting at the beginning of the file.
            CsLanguageService.Debug.Assert(this.symbols.CurrentIndex == -1, "Expected to be at the  beginning of the file");

            var fileHeaderProxy = new CodeUnitProxy(this.document);

            // Advance past whitespace and pragmas.
            this.AdvanceToNextCodeSymbol(parentProxy, SkipSymbols.WhiteSpace | SkipSymbols.EndOfLine | SkipSymbols.Preprocessor);

            // Loop until the entire header has been read.
            bool finished = false;
            int newLineCount = 0;
            while (!finished)
            {
                // Grab the next symbol but do not advance the index.
                Symbol symbol = this.symbols.Peek(1);
                if (symbol == null)
                {
                    break;
                }
                else if (symbol.SymbolType == SymbolType.SingleLineComment)
                {
                    newLineCount = 0;

                    // Ignore lines that start with //- since these are borders. We only want the body.
                    // Advance the symbol manager.
                    fileHeaderProxy.Children.Add(new SingleLineComment(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                }
                else if (symbol.SymbolType == SymbolType.EndOfLine)
                {
                    // If we've seen more than one newline in a row, we are past the end of the file header.
                    if (++newLineCount > 1 || !this.IsNextSymbolPartOfHeader())
                    {
                        break;
                    }

                    fileHeaderProxy.Children.Add(new EndOfLine(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                }
                else if (symbol.SymbolType == SymbolType.WhiteSpace)
                {
                    if (this.IsNextSymbolPartOfHeader())
                    {
                        fileHeaderProxy.Children.Add(new Whitespace(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                        this.symbols.Advance();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            var header = new FileHeader(fileHeaderProxy);
            parentProxy.Children.Add(header);

            return header;
        }

        /// <summary>
        /// Determines whether the next non-whitespace symbol looks like it is part of the file header.
        /// </summary>
        /// <returns>Returns true if the next symbol is part of the header; false otherwise.</returns>
        private bool IsNextSymbolPartOfHeader()
        {
            Symbol symbol = this.PeekNextSymbol(SkipSymbols.EndOfLine | SkipSymbols.WhiteSpace, true);
            if (symbol != null && symbol.SymbolType == SymbolType.SingleLineComment)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the symbol manager is currently sitting on the start of a generic token. 
        /// If so, reads the generic and returns it as a token.
        /// </summary>
        /// <param name="parentProxy">Represents the parent of the token.</param>
        /// <param name="unsafeCode">Indicates whether the code is marked as unsafe.</param>
        /// <returns>Returns the generic token, or null if the symbol manager is not sitting on a generic.</returns>
        private GenericTypeToken GetGenericToken(CodeUnitProxy parentProxy, bool unsafeCode)
        {
            Param.AssertNotNull(parentProxy, "parentProxy");
            Param.Ignore(unsafeCode);

            int lastSymbolIndex = -1;

            GenericTypeToken generic = this.GetGenericTokenAux(unsafeCode, 1, out lastSymbolIndex);
            if (generic != null)
            {
                this.symbols.CurrentIndex += lastSymbolIndex;
                parentProxy.Children.Add(generic);
            }

            return generic;
        }

        /// <summary>
        /// Reads a generic token from the document.
        /// </summary>
        /// <param name="unsafeCode">Indicates whether the code is marked as unsafe.</param>
        /// <param name="startIndex">The first index of the generic.</param>
        /// <param name="lastIndex">Returns the last index of the generic.</param>
        /// <returns>Returns the generic token, or null if the symbol manager is not sitting on a generic.</returns>
        /// <remarks>This should only be called by GetGenericToken.</remarks>
        private GenericTypeToken GetGenericTokenAux(bool unsafeCode, int startIndex, out int lastIndex)
        {
            Param.Ignore(unsafeCode);
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            lastIndex = -1;

            // The reference to the generic token.
            var genericTokenProxy = new CodeUnitProxy(this.document);

            // Get the first symbol. This should be an unknown word type.
            Symbol firstSymbol = this.symbols.Peek(startIndex);
            CsLanguageService.Debug.Assert(firstSymbol != null && firstSymbol.SymbolType == SymbolType.Other, "Expected a text symbol");

            // This will hold the generic type if we create one.
            GenericTypeToken generic = null;

            // Create a token for the name.
            var name = new LiteralToken(this.document, firstSymbol.Text, firstSymbol.Location, this.symbols.Generated);

            // Get the argument list. This will return null if this is not a generic.
            if (this.GetGenericArgumentList(genericTokenProxy, unsafeCode, name, startIndex + 1, out lastIndex))
            {
                generic = new GenericTypeToken(genericTokenProxy);
            }

            return generic;
        }

        /// <summary>
        /// Gets the argument list from a generic type.
        /// </summary>
        /// <param name="genericTokenProxy">Proxy object for the generic token being created.</param>
        /// <param name="unsafeCode">Indicates whether the code is marked as unsafe.</param>
        /// <param name="name">Optional name of the generic type.</param>
        /// <param name="startIndex">The first index of the generic.</param>
        /// <param name="endIndex">Returns the index of the last token in the generic argument list.</param>
        /// <returns>Returns a list of tokens containing the arguments.</returns>
        private bool GetGenericArgumentList(
            CodeUnitProxy genericTokenProxy, bool unsafeCode, Token name, int startIndex, out int endIndex)
        {
            Param.AssertNotNull(genericTokenProxy, "genericTokenProxy");
            Param.Ignore(unsafeCode);
            Param.Ignore(name);
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            endIndex = -1;
            List<LexicalElement> genericArgumentListItems = new List<LexicalElement>();

            // Move past whitespace and comments.
            int index = this.GetGenericArgumentListLeadingWhitespace(startIndex, genericArgumentListItems);

            BracketToken openingGenericBracket = null;

            // The next symbol should be an opening bracket, if this is a generic.
            Symbol symbol = this.symbols.Peek(index);
            if (symbol != null && symbol.SymbolType == SymbolType.LessThan)
            {
                if (!this.GetGenericArgumentListSymbols(unsafeCode, genericArgumentListItems, index, out openingGenericBracket, out endIndex))
                {
                    return false;
                }
            }

            // If any tokens were gathered, then this is valid generic argument list so add the tokens
            // to the generic token proxy and return success.
            if (genericArgumentListItems != null && genericArgumentListItems.Count > 0)
            {
                if (name != null)
                {
                    genericTokenProxy.Children.Add(name);
                }

                for (index = 0; index < genericArgumentListItems.Count; ++index)
                {
                    if (genericArgumentListItems[index].Is(TokenType.OpenGenericBracket))
                    {
                        break;
                    }
                
                    genericTokenProxy.Children.Add(genericArgumentListItems[index]);
                }

                // First, find the opening bracket.
                if (!genericArgumentListItems[index].Is(TokenType.OpenGenericBracket))
                {
                    throw new SyntaxException(this.document, this.GetBestLineNumber());
                }

                genericTokenProxy.Children.Add(genericArgumentListItems[index]);

                // Create a GenericTypeParameterList and add it as a child of the generic token.
                var genericTypeParameterListProxy = new CodeUnitProxy(this.document);
                genericTokenProxy.Children.Add(new GenericTypeParameterList(genericTypeParameterListProxy));

                this.GetGenericArgumentListTokens(genericTokenProxy, genericTypeParameterListProxy, genericArgumentListItems, index + 1);

                return true;
            }

            // This is not a generic argument list.
            return false;
        }

        /// <summary>
        /// Creates tokens for each of the symbols in the argument list.
        /// </summary>
        /// <param name="genericTokenProxy">The proxy for the generic type token.</param>
        /// <param name="genericTypeParameterListProxy">The proxy for the generic type argument list.</param>
        /// <param name="genericArgumentListItems">The collection of symbols to convert.</param>
        /// <param name="startIndex">The start index within the symbol list.</param>
        private void GetGenericArgumentListTokens(CodeUnitProxy genericTokenProxy, CodeUnitProxy genericTypeParameterListProxy, List<LexicalElement> genericArgumentListItems, int startIndex)
        {
            Param.AssertNotNull(genericTokenProxy, "genericTokenProxy");
            Param.AssertNotNull(genericTypeParameterListProxy, "genericTypeParameterListProxy");
            Param.AssertNotNull(genericArgumentListItems, "genericArgumentListItems");
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            // Find each of the generic arguments and add them to the list.
            int tokenIndex = this.NextCodeToken(genericTypeParameterListProxy, startIndex, genericArgumentListItems);
            while (tokenIndex < genericArgumentListItems.Count && tokenIndex != -1)
            {
                if (genericArgumentListItems[tokenIndex].Is(TokenType.CloseGenericBracket))
                {
                    genericTokenProxy.Children.Add(genericArgumentListItems[tokenIndex]);
                    break;
                }
                else if (genericArgumentListItems[tokenIndex].Is(TokenType.Comma))
                {
                    genericTypeParameterListProxy.Children.Add(genericArgumentListItems[tokenIndex]);
                    tokenIndex = this.NextCodeToken(genericTypeParameterListProxy, tokenIndex + 1, genericArgumentListItems);
                    if (tokenIndex == -1)
                    {
                        throw new SyntaxException(this.document, this.GetBestLineNumber());
                    }
                }
                else
                {
                    var genericTypeParameterProxy = new CodeUnitProxy(this.document);

                    // Check for out or in.
                    if (genericArgumentListItems[tokenIndex].Is(TokenType.In) || genericArgumentListItems[tokenIndex].Is(TokenType.Out))
                    {
                        genericTypeParameterProxy.Children.Add(genericArgumentListItems[tokenIndex]);
                        tokenIndex = this.NextCodeToken(genericTypeParameterProxy, tokenIndex + 1, genericArgumentListItems);
                        if (tokenIndex == -1)
                        {
                            throw new SyntaxException(this.document, this.GetBestLineNumber());
                        }
                    }

                    // Check for out or in.
                    if (genericArgumentListItems[tokenIndex].Is(TokenType.In) || genericArgumentListItems[tokenIndex].Is(TokenType.Out))
                    {
                        genericTypeParameterProxy.Children.Add(genericArgumentListItems[tokenIndex]);
                        tokenIndex = this.NextCodeToken(genericTypeParameterProxy, tokenIndex + 1, genericArgumentListItems);
                        if (tokenIndex == -1)
                        {
                            throw new SyntaxException(this.document, this.GetBestLineNumber());
                        }
                    }

                    // Get the type token.
                    if (!genericArgumentListItems[tokenIndex].Is(TokenType.Type))
                    {
                        throw new SyntaxException(this.document, this.GetBestLineNumber());
                    }

                    genericTypeParameterProxy.Children.Add(genericArgumentListItems[tokenIndex]);
                    genericTypeParameterListProxy.Children.Add(new GenericTypeParameter(genericTypeParameterProxy));

                    tokenIndex = this.NextCodeToken(genericTypeParameterListProxy, tokenIndex + 1, genericArgumentListItems);
                    if (tokenIndex == -1)
                    {
                        throw new SyntaxException(this.document, this.GetBestLineNumber());
                    }
                }
            }
        }

        /// <summary>
        /// Gets the symbols that form a generic argument list.
        /// </summary>
        /// <param name="unsafeCode">Indicates whether the code is unsafe.</param>
        /// <param name="genericArgumentListItems">The collection to add the symbols into.</param>
        /// <param name="startIndex">The index at which to start looking for symbols.</param>
        /// <param name="openingGenericBracket">Returns the opening generic bracket.</param>
        /// <param name="endIndex">Returns the index of the end of the argument list.</param>
        /// <returns>Returns false if the symbols are not part of a generic argument list.</returns>
        private bool GetGenericArgumentListSymbols(
            bool unsafeCode, 
            List<LexicalElement> genericArgumentListItems, 
            int startIndex, 
            out BracketToken openingGenericBracket,            
            out int endIndex)
        {
            Param.Ignore(unsafeCode);
            Param.AssertNotNull(genericArgumentListItems, "genericArgumentListItems");
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            openingGenericBracket = null;
            endIndex = -1;

            // Loop through the rest of the symbols.
            while (true)
            {
                Symbol symbol = this.symbols.Peek(startIndex);

                if (symbol == null)
                {
                    // The code ran out before we found the end of the generic.
                    throw new SyntaxException(this.document, this.GetBestLineNumber());
                }
                else if (symbol.SymbolType == SymbolType.LessThan)
                {
                    if (openingGenericBracket != null)
                    {
                        // This is not a generic statement.
                        return false;
                    }

                    openingGenericBracket = new OpenGenericBracketToken(this.document, symbol.Text, symbol.Location, this.symbols.Generated);
                    genericArgumentListItems.Add(openingGenericBracket);
                }
                else if (symbol.SymbolType == SymbolType.GreaterThan)
                {
                    if (openingGenericBracket == null)
                    {
                        // This is not a generic statement.
                        return false;
                    }

                    // This is the end of the generic statement. Add the closing bracket to the token list.
                    var closingGenericBracket = new CloseGenericBracketToken(this.document, symbol.Text, symbol.Location, this.symbols.Generated);
                    genericArgumentListItems.Add(closingGenericBracket);

                    openingGenericBracket.MatchingBracket = closingGenericBracket;
                    closingGenericBracket.MatchingBracket = openingGenericBracket;

                    endIndex = startIndex;
                    break;
                }
                else if (symbol.SymbolType == SymbolType.Other)
                {
                    int lastIndex = 0;
                    Token word = this.GetTypeTokenAux(unsafeCode, true, false, startIndex, out lastIndex);
                    if (word == null)
                    {
                        throw new SyntaxException(this.document, symbol.LineNumber);
                    }

                    // Advance the index to the end of the token.
                    startIndex = lastIndex;

                    // Add the token.
                    genericArgumentListItems.Add(word);
                }
                else if (symbol.SymbolType == SymbolType.Out)
                {
                    genericArgumentListItems.Add(this.ConvertTokenSymbol(symbol, TokenType.Out));
                }
                else if (symbol.SymbolType == SymbolType.In)
                {
                    genericArgumentListItems.Add(this.ConvertTokenSymbol(symbol, TokenType.In));
                }
                else if (symbol.SymbolType == SymbolType.WhiteSpace ||
                    symbol.SymbolType == SymbolType.EndOfLine ||
                    symbol.SymbolType == SymbolType.SingleLineComment ||
                    symbol.SymbolType == SymbolType.MultiLineComment ||
                    symbol.SymbolType == SymbolType.PreprocessorDirective ||
                    symbol.SymbolType == SymbolType.SkippedSection)
                {
                    // Add these to the token list.
                    genericArgumentListItems.Add(this.PeekNonTokenSymbol(symbol));
                }
                else if (symbol.SymbolType == SymbolType.Comma)
                {
                    genericArgumentListItems.Add(this.ConvertTokenSymbol(symbol, TokenType.Comma));
                }
                else
                {
                    // Any other symbol signifies that this is not a generic statement.
                    genericArgumentListItems = null;
                    break;
                }

                ++startIndex;
            }

            return true;
        }

        /// <summary>
        /// Gets the leading whitespace before a generic argument list.
        /// </summary>
        /// <param name="startIndex">The first index to start looking.</param>
        /// <param name="genericArgumentListItems">Add the whitespace items to this list.</param>
        /// <returns>Returns the index past the whitespace.</returns>
        private int GetGenericArgumentListLeadingWhitespace(int startIndex, List<LexicalElement> genericArgumentListItems)
        {
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");
            Param.AssertNotNull(genericArgumentListItems, "genericArgumentListItems");

            int index = startIndex;
            while (true)
            {
                Symbol lex = this.symbols.Peek(index);
                if (lex == null)
                {
                    break;
                }

                LexicalElement item = this.PeekNonTokenSymbol(lex);
                if (item == null)
                {
                    break;
                }

                genericArgumentListItems.Add(item);
                ++index;
            }

            return index;
        }

        /// <summary>
        /// Gets the next code token in the list.
        /// </summary>
        /// <param name="parent">The parent proxy.</param>
        /// <param name="startIndex">The index at which to begin searching.</param>
        /// <param name="items">The list of items.</param>
        /// <returns>Returns the next code token or -1 if none was found.</returns>
        private int NextCodeToken(CodeUnitProxy parent, int startIndex, IList<LexicalElement> items)
        {
            Param.AssertNotNull(parent, "parent");
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");
            Param.AssertNotNull(items, "items");

            for (int i = startIndex; i < items.Count; ++i)
            {
                if (!items[i].Is(CodeUnitType.LexicalElement) || items[i].Is(LexicalElementType.Token))
                {
                    return i;
                }

                parent.Children.Add(items[i]);
            }

            return -1;
        }

        /// <summary>
        /// Find the end of a name.
        /// </summary>
        /// <param name="startIndex">The first index of the name.</param>
        /// <returns>Returns the last index of the name.</returns>
        private int AdvanceToEndOfName(int startIndex)
        {
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            bool generic;
            return this.AdvanceToEndOfName(startIndex, out generic);
        }

        /// <summary>
        /// Find the end of a name.
        /// </summary>
        /// <param name="startIndex">The first index of the name.</param>
        /// <param name="generic">Returns true if the first part of the name is a generic.</param>
        /// <returns>Returns the last index of the name.</returns>
        private int AdvanceToEndOfName(int startIndex, out bool generic)
        {
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            generic = false;

            Symbol symbol = this.symbols.Peek(startIndex);
            CsLanguageService.Debug.Assert(symbol != null && symbol.SymbolType == SymbolType.Other, "Expected a name symbol");

            int index = startIndex;
            int endIndex = startIndex;

            int leadingGenericIndex = this.GetNextCodeSymbolIndex(index + 1);
            if (leadingGenericIndex != -1)
            {
                symbol = this.symbols.Peek(leadingGenericIndex);
                if (symbol.SymbolType == SymbolType.LessThan)
                {
                    // Move up to the closing bracket and check that this is actually a generic type.
                    leadingGenericIndex = this.AdvanceToClosingGenericSymbol(leadingGenericIndex + 1);
                    if (leadingGenericIndex != -1)
                    {
                        index = leadingGenericIndex;
                        endIndex = index;
                        generic = true;
                    }
                }
            }

            bool dot = false;
            while (true)
            {
                index = this.GetNextCodeSymbolIndex(index + 1);
                if (index == -1)
                {
                    break;
                }

                symbol = this.symbols.Peek(index);
                if (symbol.SymbolType == SymbolType.Other)
                {
                    if (dot)
                    {
                        // Peek ahead and see if this looks like a generic.
                        int temp = this.GetNextCodeSymbolIndex(index + 1);
                        if (temp != -1 && this.symbols.Peek(temp).SymbolType == SymbolType.LessThan)
                        {
                            temp = this.AdvanceToClosingGenericSymbol(temp + 1);
                            if (temp == -1)
                            {
                                break;
                            }
                            else
                            {
                                index = temp;
                            }
                        }

                        endIndex = index;
                        dot = false;
                    }
                    else
                    {
                        break;
                    }
                }
                else if (symbol.SymbolType == SymbolType.Dot || symbol.SymbolType == SymbolType.QualifiedAlias)
                {
                    if (dot)
                    {
                        break;
                    }
                    else
                    {
                        dot = true;
                    }
                }
                else
                {
                    break;
                }
            }

            return endIndex;
        }

        /// <summary>
        /// Advances past any whitespace and comments in the code.
        /// </summary>
        /// <param name="parentProxy">Represents the parent item.</param>
        private void AdvanceToNextCodeSymbol(CodeUnitProxy parentProxy)
        {
            Param.Ignore(parentProxy);
            this.AdvanceToNextCodeSymbol(parentProxy, SkipSymbols.All);
        }

        /// <summary>
        /// Advances past any whitespace and comments in the code.
        /// </summary>
        /// <param name="parentProxy">Represents the parent item.</param>
        /// <param name="skip">Indicates the types of CodeUnits to advance past.</param>
        private void AdvanceToNextCodeSymbol(CodeUnitProxy parentProxy, SkipSymbols skip)
        {
            Param.AssertNotNull(parentProxy, "parentProxy");
            Param.Ignore(skip);

            while (this.GetNextNonCodeSymbol(parentProxy, skip))
            {
            }
        }

        /// <summary>
        /// Gets the next whitespace or comment symbol if it appears in the skip list.
        /// </summary>
        /// <param name="parentProxy">The proxy to add the new CodeUnits to.</param>
        /// <param name="types">Indicates the types of CodeUnits to advance past.</param>
        /// <returns>Returns true if a symbol was advanced past; false otherwise.</returns>
        private bool GetNextNonCodeSymbol(CodeUnitProxy parentProxy, SkipSymbols types)
        {
            Param.AssertNotNull(parentProxy, "parentProxy");
            Param.Ignore(types);

            Symbol symbol = this.symbols.Peek(1);
            if (symbol == null)
            {
                return false;
            }

            // NOTE: If the list of case-statements checked here is changed, you must also make the same corresponding change in the PeekToNextCodeSymbol method.
            switch (symbol.SymbolType)
            {
                case SymbolType.WhiteSpace:
                    if ((types & SkipSymbols.WhiteSpace) == 0)
                    {
                        return false;
                    }

                    parentProxy.Children.Add(new Whitespace(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                    return true;

                case SymbolType.EndOfLine:
                    if ((types & SkipSymbols.EndOfLine) == 0)
                    {
                        return false;
                    }

                    parentProxy.Children.Add(new EndOfLine(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                    return true;

                case SymbolType.SingleLineComment:
                    if ((types & SkipSymbols.SingleLineComment) == 0)
                    {
                        return false;
                    }

                    parentProxy.Children.Add(new SingleLineComment(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                    return true;

                case SymbolType.MultiLineComment:
                    if ((types & SkipSymbols.MultiLineComment) == 0)
                    {
                        return false;
                    }

                    parentProxy.Children.Add(new MultilineComment(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                    return true;

                case SymbolType.PreprocessorDirective:
                    if ((types & SkipSymbols.Preprocessor) == 0)
                    {
                        return false;
                    }

                    this.GetPreprocessorDirective(parentProxy, symbol, this.symbols.Generated);
                    this.symbols.Advance();
                    return true;

                case SymbolType.XmlHeaderLine:
                    if ((types & SkipSymbols.XmlHeader) == 0)
                    {
                        return false;
                    }

                    parentProxy.Children.Add(new ElementHeaderLine(this.document, symbol.Text, symbol.Location, this.symbols.Generated));
                    this.symbols.Advance();
                    return true;

                case SymbolType.SkippedSection:
                    parentProxy.Children.Add(new SkippedSection(this.document, symbol.Location, this.symbols.Generated, symbol.Text));
                    this.symbols.Advance();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Advances past any whitespace and comments in the code.
        /// </summary>
        /// <returns>Returns the next symbol.</returns>
        private Symbol PeekToNextCodeSymbol()
        {
            return this.PeekToNextCodeSymbol(SkipSymbols.All);
        }

        /// <summary>
        /// Advances past any whitespace and comments in the code.
        /// </summary>
        /// <param name="skip">Indicates the types of tokens to advance past.</param>
        /// <returns>Returns the next symbol.</returns>
        private Symbol PeekToNextCodeSymbol(SkipSymbols skip)
        {
            Param.Ignore(skip);

            int index = 1;
            Symbol symbol = this.symbols.Peek(index);
            
            // NOTE: If the list of symbols checked here is changed, you must also make the same corresponding change in the AdvanceToNextCodeSymbol method.
            while ((symbol != null) &&
                (((symbol.SymbolType == SymbolType.WhiteSpace) && (skip & SkipSymbols.WhiteSpace) != 0) ||
                 ((symbol.SymbolType == SymbolType.EndOfLine) && (skip & SkipSymbols.EndOfLine) != 0) ||
                 ((symbol.SymbolType == SymbolType.SingleLineComment) && (skip & SkipSymbols.SingleLineComment) != 0) ||
                 ((symbol.SymbolType == SymbolType.MultiLineComment) && (skip & SkipSymbols.MultiLineComment) != 0) ||
                 ((symbol.SymbolType == SymbolType.PreprocessorDirective) && (skip & SkipSymbols.Preprocessor) != 0) ||
                 ((symbol.SymbolType == SymbolType.XmlHeaderLine) && (skip & SkipSymbols.XmlHeader) != 0) ||
                 (symbol.SymbolType == SymbolType.SkippedSection)))
            {
                symbol = this.symbols.Peek(++index);
            }

            return symbol;
        }

        /// <summary>
        /// Returns the next code symbol without allocating any tokens or changing the current symbol index.
        /// </summary>
        /// <returns>Returns the next code symbol.</returns>
        /// <exception cref="SyntaxException">Will be thrown if there are no more symbols in the document.</exception>
        private Symbol PeekNextSymbol()
        {
            return this.PeekNextSymbol(SkipSymbols.All);
        }

        /// <summary>
        /// Returns the next code symbol without allocating any tokens or changing the current symbol index.
        /// </summary>
        /// <returns>Returns the next code symbol.</returns>
        /// <param name="allowNull">If true, indicates that this method is allowed to return a null symbol, if there are no
        /// more symbols in the document. If false, the method will throw an exception if it is unable to get another symbol.</param>
        /// <exception cref="SyntaxException">Will be thrown if there are no more symbols in the document.</exception>
        private Symbol PeekNextSymbol(bool allowNull)
        {
            Param.Ignore(allowNull);
            return this.PeekNextSymbol(SkipSymbols.All, allowNull);
        }

        /// <summary>
        /// Returns the next code symbol without allocating any tokens or changing the current symbol index.
        /// </summary>
        /// <param name="skip">Indicates the types of symbols to skip past.</param>
        /// <returns>Returns the next code symbol.</returns>
        /// <exception cref="SyntaxException">Will be thrown if there are no more symbols in the document.</exception>
        private Symbol PeekNextSymbol(SkipSymbols skip)
        {
            Param.Ignore(skip);

            return this.PeekNextSymbol(skip, false);
        }

        /// <summary>
        /// Returns the next code symbol without allocating any tokens or changing the current symbol index.
        /// </summary>
        /// <param name="skip">Indicates the types of symbols to skip past.</param>
        /// <param name="allowNull">If true, indicates that this method is allowed to return a null symbol, if there are no
        /// more symbols in the document. If false, the method will throw an exception if it is unable to get another symbol.</param>
        /// <returns>Returns the next code symbol.</returns>
        /// <exception cref="SyntaxException">Will be thrown if there are no more symbols in the document.</exception>
        private Symbol PeekNextSymbol(SkipSymbols skip, bool allowNull)
        {
            Param.Ignore(skip);
            Param.Ignore(allowNull);

            Symbol symbol = this.PeekToNextCodeSymbol(skip);
            if (symbol == null && !allowNull)
            {
                throw this.CreateSyntaxException();
            }

            return symbol;
        }

        /// <summary>
        /// Advances past any whitespace and comments in the code.
        /// </summary>
        /// <param name="startIndex">The first index to peek.</param>
        /// <returns>Returns the peek index of the next code symbol or -1 if there 
        /// are no more code symbols.</returns>
        private int GetNextCodeSymbolIndex(int startIndex)
        {
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");

            int index = -1;

            while (true)
            {
                Symbol symbol = this.symbols.Peek(startIndex);
                if (symbol == null)
                {
                    break;
                }
                else if (symbol.SymbolType != SymbolType.WhiteSpace &&
                    symbol.SymbolType != SymbolType.EndOfLine &&
                    symbol.SymbolType != SymbolType.SingleLineComment &&
                    symbol.SymbolType != SymbolType.MultiLineComment &&
                    symbol.SymbolType != SymbolType.PreprocessorDirective &&
                    symbol.SymbolType != SymbolType.SkippedSection)
                {
                    index = startIndex;
                    break;
                }

                ++startIndex;
            }

            return index;
        }

        /// <summary>
        /// Gets the closest line number to the current part of the file being parsed.
        /// </summary>
        /// <returns>Returns the best line number.</returns>
        private int GetBestLineNumber()
        {
            int lineNumber = 1;
            if (this.symbols.Current != null)
            {
                lineNumber = this.symbols.Current.LineNumber;
            }
            else if (this.document != null && this.document.Children.Count > 0)
            {
                // Take the line number of the last code unit that has been added to the document up to this point.
                lineNumber = this.document.FindLastDescendent().LineNumber;
            }

            if (lineNumber <= 0)
            {
                lineNumber = 1;
            }

            return lineNumber;
        }

        /// <summary>
        /// Creates a new syntax exception which attempts to find the best line number 
        /// given the current set up symbols and tokens.
        /// </summary>
        /// <returns>Returns the exception.</returns>
        private SyntaxException CreateSyntaxException()
        {
            return new SyntaxException(this.document, this.GetBestLineNumber());
        }

        #endregion Private Methods
    }
}
