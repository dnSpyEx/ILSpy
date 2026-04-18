#nullable enable
// Copyright (c) 2017 Siegfried Pammer
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

using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace ICSharpCode.Decompiler.IL
{
	/// <summary>
	/// IL using instruction.
	/// Equivalent to:
	/// <code>
	/// stloc v(resourceExpression)
	/// try {
	///    body
	/// } finally {
	///    v?.Dispose();
	/// }
	/// </code>
	/// </summary>
	/// <remarks>
	/// The value of v is undefined after the end of the body block.
	/// </remarks>
	partial class UsingInstruction
	{
		public bool IsAsync { get; set; }

		public bool IsRefStruct { get; set; }

		public override bool SafeToAddToEndILSpans {
			get { return false; }
		}

		public override void WriteTo(IDecompilerOutput output, ILAstWritingOptions options)
		{
			WriteILRange(output, options);
			output.Write("using", BoxedTextColor.Keyword);
			if (IsAsync)
			{
				output.Write(".async", BoxedTextColor.Keyword);
			}
			if (IsRefStruct)
			{
				output.Write(".ref", BoxedTextColor.Keyword);
			}
			output.Write(" ", BoxedTextColor.Text);
			var braceInfo = OpenBrace(output, "(");
			Variable.WriteTo(output);
			output.Write(" ", BoxedTextColor.Text);
			output.Write("=", BoxedTextColor.Operator);
			output.Write(" ", BoxedTextColor.Text);
			ResourceExpression.WriteTo(output, options);
			CloseBrace(output, braceInfo, ")", CodeBracesRangeFlags.Parentheses);
			output.Write(" ", BoxedTextColor.Text);
			braceInfo = OpenBrace(output, "{");
			output.WriteLine("{", BoxedTextColor.Punctuation);
			output.IncreaseIndent();
			Body.WriteTo(output, options);
			output.DecreaseIndent();
			output.WriteLine();
			CloseBrace(output, braceInfo, "}", CodeBracesRangeFlags.UsingBraces);
		}
	}
}
