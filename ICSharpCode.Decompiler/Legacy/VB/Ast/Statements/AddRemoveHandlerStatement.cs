// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;

using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements
{
	/// <summary>
	/// ( AddHandler | RemoveHandler ) Expression, Expression
	/// </summary>
	public class AddRemoveHandlerStatement : Statement
	{
		public static readonly CSharp.Syntax.Role<Expression> EventExpressionRole = new CSharp.Syntax.Role<Expression>("EventExpression", Expression.Null);
		public static readonly CSharp.Syntax.Role<Expression> DelegateExpressionRole = new CSharp.Syntax.Role<Expression>("DelegateExpression", Expression.Null);

		public bool IsAddHandler { get; set; }

		public Expression EventExpression {
			get { return GetChildByRole(EventExpressionRole); }
			set { SetChildByRole(EventExpressionRole, value); }
		}

		public Expression DelegateExpression {
			get { return GetChildByRole(DelegateExpressionRole); }
			set { SetChildByRole(DelegateExpressionRole, value); }
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitAddRemoveHandlerStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}
	}
}
