// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System.Collections.Generic;

using dnSpy.Contracts.Decompiler;

using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	/// <summary>
	/// { Statements }
	/// </summary>
	public class BlockStatement : Statement, IEnumerable<Statement>
	{
		public static readonly CSharp.Syntax.Role<Statement> StatementRole = new CSharp.Syntax.Role<Statement>("Statement", Statement.Null);

		#region Null
		public static readonly new BlockStatement Null = new NullBlockStatement();
		sealed class NullBlockStatement : BlockStatement
		{
			public override bool IsNull {
				get {
					return true;
				}
			}

			public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
			{
				return default(S);
			}

			protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
			{
				return other == null || other.IsNull;
			}
		}
		#endregion

		#region PatternPlaceholder
		public static implicit operator BlockStatement(CSharp.Syntax.PatternMatching.Pattern pattern)
		{
			return pattern != null ? new PatternPlaceholder(pattern) : null;
		}

		sealed class PatternPlaceholder : BlockStatement, CSharp.Syntax.PatternMatching.INode
		{
			readonly CSharp.Syntax.PatternMatching.Pattern child;

			public PatternPlaceholder(CSharp.Syntax.PatternMatching.Pattern child)
			{
				this.child = child;
			}

			public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
			{
				return visitor.VisitPatternPlaceholder(this, child, data);
			}

			protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
			{
				return child.DoMatch(other, match);
			}

			bool CSharp.Syntax.PatternMatching.INode.DoMatchCollection(CSharp.Syntax.Role role, CSharp.Syntax.PatternMatching.INode pos, CSharp.Syntax.PatternMatching.Match match, CSharp.Syntax.PatternMatching.BacktrackingInfo backtrackingInfo)
			{
				return child.DoMatchCollection(role, pos, match, backtrackingInfo);
			}
		}
		#endregion

		public AstNodeCollection<Statement> Statements {
			get { return GetChildrenByRole (StatementRole); }
		}

		public IList<ILSpan> HiddenStart { get; set; }
		public IList<ILSpan> HiddenEnd { get; set; }

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitBlockStatement (this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			BlockStatement o = other as BlockStatement;
			return o != null && !(o is CatchBlock) && !o.IsNull && this.Statements.DoMatch(o.Statements, match);
		}

		#region Builder methods
		public void Add(Statement statement)
		{
			AddChild(statement, StatementRole);
		}

		public void Add(Expression expression)
		{
			AddChild(new ExpressionStatement { Expression = expression }, StatementRole);
		}

		public void AddRange(IEnumerable<Statement> statements)
		{
			foreach (Statement st in statements)
				AddChild(st, StatementRole);
		}

		public void AddAssignment(Expression left, Expression right)
		{
			Add(new AssignmentExpression(left, AssignmentOperatorType.Assign, right));
		}

		public void AddReturnStatement(Expression expression)
		{
			Add(new ReturnStatement { Expression = expression });
		}
		#endregion

		IEnumerator<Statement> IEnumerable<Statement>.GetEnumerator()
		{
			return this.Statements.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.Statements.GetEnumerator();
		}
	}
}
