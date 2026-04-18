// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ICSharpCode.Decompiler.CSharp.OutputVisitor
{
	public abstract class TokenWriter
	{
		public abstract void StartNode(AstNode node);
		public abstract void EndNode(AstNode node);

		public virtual void WriteSpecialsUpToNode(AstNode node) { }

		/// <summary>
		/// Writes an identifier.
		/// </summary>
		public abstract void WriteIdentifier(Identifier identifier, object data);

		/// <summary>
		/// Writes a keyword to the output.
		/// </summary>
		public abstract void WriteKeyword(Role role, string keyword);

		/// <summary>
		/// Writes a token to the output.
		/// </summary>
		public abstract void WriteToken(Role role, string token, object data);

		/// <summary>
		/// Writes a primitive/literal value
		/// </summary>
		public abstract void WritePrimitiveValue(object value, object data = null, LiteralFormat format = LiteralFormat.None);

		public abstract void WritePrimitiveType(string type);

		/// <summary>
		/// Write a piece of text in an interpolated string literal.
		/// </summary>
		public abstract void WriteInterpolatedText(string text, object data = null);

		public abstract void Space();
		public abstract void Indent();
		public abstract void Unindent();
		public abstract void NewLine();

		public abstract void WriteComment(CommentType commentType, string content, CommentReference[] refs);
		public abstract void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument);

		public static TokenWriter Create(TextWriter writer, string indentation = "\t")
		{
			return new InsertSpecialsDecorator(new InsertRequiredSpacesDecorator(new TextWriterTokenWriter(writer) { IndentationString = indentation }));
		}

		public static TokenWriter CreateWriterThatSetsLocationsInAST(TextWriter writer, string indentation = "\t")
		{
			var target = new TextWriterTokenWriter(writer) { IndentationString = indentation };
			return new InsertSpecialsDecorator(new InsertRequiredSpacesDecorator(new InsertMissingTokensDecorator(target, target)));
		}

		public static TokenWriter InsertRequiredSpaces(TokenWriter writer)
		{
			return new InsertRequiredSpacesDecorator(writer);
		}

		public static TokenWriter WrapInWriterThatSetsLocationsInAST(TokenWriter writer)
		{
			if (!(writer is ILocatable))
				throw new InvalidOperationException("writer does not provide locations!");
			return new InsertSpecialsDecorator(new InsertRequiredSpacesDecorator(new InsertMissingTokensDecorator(writer, (ILocatable)writer)));
		}

		public virtual void DebugStart(AstNode node, int? start)
		{
		}

		public virtual void DebugHidden(AstNode hiddenNode)
		{
		}

		public virtual void DebugExpression(AstNode node)
		{
		}

		public virtual void DebugEnd(AstNode node, int? end)
		{
		}

		// Don't use Location prop since it's used by ILocatable. We want that whoever can provide
		// this value can do it so we can't check the writer to see if it implements ILocatable
		public virtual int? GetLocation()
		{
			return null;
		}

		public void WriteTokenOperator(Role tokenRole, string token)
		{
			WriteToken(tokenRole, token, BoxedTextColor.Operator);
		}

		public void WriteTokenPunctuation(Role tokenRole, string token)
		{
			WriteToken(tokenRole, token, BoxedTextColor.Punctuation);
		}

		public virtual void AddHighlightedKeywordReference(object reference, int start, int end)
		{
		}

		public virtual void AddBracePair(int leftStart, int leftEnd, int rightStart, int rightEnd, CodeBracesRangeFlags flags)
		{
		}

		public virtual void AddLineSeparator(int position)
		{
		}
	}

	public interface ILocatable
	{
		TextLocation Location { get; }
	}

	public abstract class DecoratingTokenWriter : TokenWriter
	{
		readonly TokenWriter decoratedWriter;

		protected DecoratingTokenWriter(TokenWriter decoratedWriter)
		{
			if (decoratedWriter == null)
				throw new ArgumentNullException(nameof(decoratedWriter));
			this.decoratedWriter = decoratedWriter;
		}

		public override void StartNode(AstNode node)
		{
			decoratedWriter.StartNode(node);
		}

		public override void EndNode(AstNode node)
		{
			decoratedWriter.EndNode(node);
		}

		public override void WriteSpecialsUpToNode(AstNode node)
		{
			decoratedWriter.WriteSpecialsUpToNode(node);
		}

		public override void WriteIdentifier(Identifier identifier, object data)
		{
			decoratedWriter.WriteIdentifier(identifier, data);
		}

		public override void WriteKeyword(Role role, string keyword)
		{
			decoratedWriter.WriteKeyword(role, keyword);
		}

		public override void WriteToken(Role role, string token, object data)
		{
			decoratedWriter.WriteToken(role, token, data);
		}

		public override void WritePrimitiveValue(object value, object data = null, LiteralFormat format = LiteralFormat.None)
		{
			decoratedWriter.WritePrimitiveValue(value, data, format);
		}

		public override void WritePrimitiveType(string type)
		{
			decoratedWriter.WritePrimitiveType(type);
		}

		public override void WriteInterpolatedText(string text, object data = null)
		{
			decoratedWriter.WriteInterpolatedText(text, data);
		}

		public override void Space()
		{
			decoratedWriter.Space();
		}

		public override void Indent()
		{
			decoratedWriter.Indent();
		}

		public override void Unindent()
		{
			decoratedWriter.Unindent();
		}

		public override void NewLine()
		{
			decoratedWriter.NewLine();
		}

		public override void WriteComment(CommentType commentType, string content, CommentReference[] refs)
		{
			decoratedWriter.WriteComment(commentType, content, refs);
		}

		public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument)
		{
			decoratedWriter.WritePreProcessorDirective(type, argument);
		}

		public override void DebugStart(AstNode node, int? start)
		{
			decoratedWriter.DebugStart(node, start);
		}

		public override void DebugHidden(AstNode hiddenNode)
		{
			decoratedWriter.DebugHidden(hiddenNode);
		}

		public override void DebugExpression(AstNode node)
		{
			decoratedWriter.DebugExpression(node);
		}

		public override void DebugEnd(AstNode node, int? end)
		{
			decoratedWriter.DebugEnd(node, end);
		}

		public override int? GetLocation()
		{
			return decoratedWriter.GetLocation();
		}

		public override void AddHighlightedKeywordReference(object reference, int start, int end)
		{
			decoratedWriter.AddHighlightedKeywordReference(reference, start, end);
		}

		public override void AddBracePair(int leftStart, int leftEnd, int rightStart, int rightEnd, CodeBracesRangeFlags flags)
		{
			decoratedWriter.AddBracePair(leftStart, leftEnd, rightStart, rightEnd, flags);
		}

		public override void AddLineSeparator(int position)
		{
			decoratedWriter.AddLineSeparator(position);
		}
	}
}
