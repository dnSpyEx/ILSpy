// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;

using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	public class IfElseStatement : Statement
	{
		public static readonly CSharp.Syntax.Role<Statement> FalseStatementRole = new CSharp.Syntax.Role<Statement>("False", Statement.Null);
		public static readonly CSharp.Syntax.Role<Statement> TrueStatementRole = new CSharp.Syntax.Role<Statement>("True", Statement.Null);

		public Expression Condition {
			get { return GetChildByRole(Roles.Condition); }
			set { SetChildByRole(Roles.Condition, value); }
		}

		public Statement Body {
			get { return GetChildByRole(TrueStatementRole); }
			set { SetChildByRole(TrueStatementRole, value); }
		}

		public Statement ElseBlock {
			get { return GetChildByRole(FalseStatementRole); }
			set { SetChildByRole(FalseStatementRole, value); }
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitIfElseStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}
	}
}
