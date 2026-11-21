// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;

using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;
using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	public class TryStatement : Statement
	{
		public static readonly CSharp.Syntax.Role<BlockStatement> FinallyBlockRole = new CSharp.Syntax.Role<BlockStatement>("FinallyBlock", BlockStatement.Null);

		public BlockStatement Body {
			get { return GetChildByRole(Roles.Body); }
			set { SetChildByRole(Roles.Body, value); }
		}

		public AstNodeCollection<CatchBlock> CatchBlocks {
			get { return GetChildrenByRole(CatchBlock.CatchBlockRole); }
		}

		public BlockStatement FinallyBlock {
			get { return GetChildByRole(FinallyBlockRole); }
			set { SetChildByRole(FinallyBlockRole, value); }
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitTryStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}
	}

	public class CatchBlock : BlockStatement
	{
		public static readonly CSharp.Syntax.Role<CatchBlock> CatchBlockRole = new CSharp.Syntax.Role<CatchBlock>("CatchBlockRole", null);

		public Identifier ExceptionVariable {
			get { return GetChildByRole(Roles.Identifier); }
			set { SetChildByRole(Roles.Identifier, value); }
		}

		public AstType ExceptionType {
			get { return GetChildByRole(Roles.Type); }
			set { SetChildByRole(Roles.Type, value); }
		}

		public Expression WhenExpression {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitCatchBlock(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}
	}
}
