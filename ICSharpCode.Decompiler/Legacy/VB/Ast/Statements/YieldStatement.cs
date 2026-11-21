// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	/// <summary>
	/// Yield Expression
	/// </summary>
	/// <remarks>VB 11</remarks>
	public class YieldStatement : Statement
	{
		public VBTokenNode YieldToken {
			get { return GetChildByRole (Roles.Keyword); }
		}

		public Expression Expression {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		public YieldStatement()
		{
		}

		public YieldStatement(Expression expression)
		{
			AddChild (expression, Roles.Expression);
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitYieldStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			YieldStatement o = other as YieldStatement;
			return o != null && this.Expression.DoMatch(o.Expression, match);
		}
	}
}
