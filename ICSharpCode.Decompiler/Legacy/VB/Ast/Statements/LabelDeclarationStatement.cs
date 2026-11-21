// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	/// <summary>
	/// Label:
	/// </summary>
	public class LabelDeclarationStatement : Statement
	{
		/// <remarks>either PrimitiveExpression or IdentifierExpression</remarks>
		public Expression Label {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		public VBTokenNode Colon {
			get { return GetChildByRole(Roles.Colon); }
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitLabelDeclarationStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			LabelDeclarationStatement o = other as LabelDeclarationStatement;
			return o != null && this.Label.DoMatch(o.Label, match);
		}
	}
}
