// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions {
	public class ConditionalExpression : Expression
	{
		public readonly static CSharp.Syntax.Role<Expression> ConditionExpressionRole = new CSharp.Syntax.Role<Expression>("ConditionExpressionRole", Expression.Null);
		public readonly static CSharp.Syntax.Role<Expression> TrueExpressionRole = new CSharp.Syntax.Role<Expression>("TrueExpressionRole", Expression.Null);
		public readonly static CSharp.Syntax.Role<Expression> FalseExpressionRole = new CSharp.Syntax.Role<Expression>("FalseExpressionRole", Expression.Null);

		public VBTokenNode IfToken {
			get { return GetChildByRole (Roles.Keyword); }
		}

		public Expression ConditionExpression {
			get { return GetChildByRole (ConditionExpressionRole); }
			set { SetChildByRole (ConditionExpressionRole, value); }
		}

		public Expression TrueExpression {
			get { return GetChildByRole (TrueExpressionRole); }
			set { SetChildByRole (TrueExpressionRole, value); }
		}

		public Expression FalseExpression {
			get { return GetChildByRole (FalseExpressionRole); }
			set { SetChildByRole (FalseExpressionRole, value); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitConditionalExpression(this, data);
		}
	}
}
