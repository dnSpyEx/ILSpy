// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;

using dnSpy.Contracts.Decompiler;

using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	public class SelectStatement : Statement
	{
		public Expression Expression {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		public IList<ILSpan> HiddenEnd { get; set; }

		public AstNodeCollection<CaseStatement> Cases {
			get { return GetChildrenByRole(CaseStatement.CaseStatementRole); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitSelectStatement(this, data);
		}
	}

	public class CaseStatement : Statement
	{
		public static readonly CSharp.Syntax.Role<CaseStatement> CaseStatementRole = new CSharp.Syntax.Role<CaseStatement>("CaseStatement", null);

		public AstNodeCollection<CaseClause> Clauses {
			get { return GetChildrenByRole(CaseClause.CaseClauseRole); }
		}

		public BlockStatement Body {
			get { return GetChildByRole(Roles.Body); }
			set { SetChildByRole(Roles.Body, value); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitCaseStatement(this, data);
		}
	}

	public abstract class CaseClause : AstNode
	{
		#region Null
		public new static readonly CaseClause Null = new NullCaseClause();

		sealed class NullCaseClause : CaseClause
		{
			public override bool IsNull {
				get {
					return true;
				}
			}

			public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
			{
				return default (S);
			}

			protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
			{
				return other == null || other.IsNull;
			}
		}
		#endregion

		public static readonly CSharp.Syntax.Role<CaseClause> CaseClauseRole = new CSharp.Syntax.Role<CaseClause>("CaseClause", CaseClause.Null);

		public Expression Expression {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}
	}

	public class SimpleCaseClause : CaseClause
	{
		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitSimpleCaseClause(this, data);
		}
	}

	public class RangeCaseClause : CaseClause
	{
		public static readonly CSharp.Syntax.Role<Expression> ToExpressionRole = ForStatement.ToExpressionRole;

		public Expression ToExpression {
			get { return GetChildByRole(ToExpressionRole); }
			set { SetChildByRole(ToExpressionRole, value); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitRangeCaseClause(this, data);
		}
	}

	public class ComparisonCaseClause : CaseClause
	{
		public static readonly CSharp.Syntax.Role<VBTokenNode> OperatorRole = BinaryOperatorExpression.OperatorRole;

		public ComparisonOperator Operator { get; set; }

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitComparisonCaseClause(this, data);
		}
	}

	public enum ComparisonOperator
	{
		Equality = BinaryOperatorType.Equality,
		InEquality = BinaryOperatorType.InEquality,
		LessThan = BinaryOperatorType.LessThan,
		GreaterThan = BinaryOperatorType.GreaterThan,
		LessThanOrEqual = BinaryOperatorType.LessThanOrEqual,
		GreaterThanOrEqual = BinaryOperatorType.GreaterThanOrEqual
	}
}
