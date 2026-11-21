// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System.Collections.Generic;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.General {
	public class CompilationUnit : AstNode
	{
		public static readonly CSharp.Syntax.Role<AstNode> MemberRole = new CSharp.Syntax.Role<AstNode>("Member", AstNode.Null);

		public CompilationUnit ()
		{
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			CompilationUnit o = other as CompilationUnit;
			return o != null && GetChildrenByRole(MemberRole).DoMatch(o.GetChildrenByRole(MemberRole), match);
		}

		public AstNode GetNodeAt (int line, int column)
		{
			return GetNodeAt (new CSharp.Syntax.TextLocation (line, column));
		}

		public AstNode GetNodeAt (CSharp.Syntax.TextLocation location)
		{
			AstNode node = this;
			while (node.FirstChild != null) {
				var child = node.FirstChild;
				while (child != null) {
					if (child.StartLocation <= location && location < child.EndLocation) {
						node = child;
						break;
					}
					child = child.NextSibling;
				}
				// found no better child node - therefore the parent is the right one.
				if (child == null)
					break;
			}
			return node;
		}

		public IEnumerable<AstNode> GetNodesBetween (int startLine, int startColumn, int endLine, int endColumn)
		{
			return GetNodesBetween (new CSharp.Syntax.TextLocation (startLine, startColumn), new CSharp.Syntax.TextLocation (endLine, endColumn));
		}

		public IEnumerable<AstNode> GetNodesBetween (CSharp.Syntax.TextLocation start, CSharp.Syntax.TextLocation end)
		{
			AstNode node = this;
			while (node != null) {
				AstNode next;
				if (start <= node.StartLocation && node.EndLocation <= end) {
					// Remember next before yielding node.
					// This allows iteration to continue when the caller removes/replaces the node.
					next = node.NextSibling;
					yield return node;
				} else {
					if (node.EndLocation < start) {
						next = node.NextSibling;
					} else {
						next = node.FirstChild;
					}
				}

				if (next != null && next.StartLocation > end)
					yield break;
				node = next;
			}
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitCompilationUnit (this, data);
		}
	}
}
