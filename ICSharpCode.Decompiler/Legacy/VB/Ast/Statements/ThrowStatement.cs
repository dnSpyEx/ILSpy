// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	/// <summary>
	/// Throw Expression
	/// </summary>
	public class ThrowStatement : Statement
	{
		public VBTokenNode ThrowToken {
			get { return GetChildByRole (Roles.Keyword); }
		}

		public Expression Expression {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		public ThrowStatement()
		{
		}

		public ThrowStatement(Expression expression)
		{
			AddChild (expression, Roles.Expression);
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitThrowStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			ThrowStatement o = other as ThrowStatement;
			return o != null && this.Expression.DoMatch(o.Expression, match);
		}
	}
}
