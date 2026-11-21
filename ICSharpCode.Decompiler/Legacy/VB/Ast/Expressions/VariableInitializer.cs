// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeMembers;
using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions {
	/// <summary>
	/// Identifier As Type = Expression
	/// </summary>
	public class VariableInitializer : AstNode
	{
		public static readonly CSharp.Syntax.Role<VariableInitializer> VariableInitializerRole = new CSharp.Syntax.Role<VariableInitializer>("VariableInitializer", null);

		public VariableIdentifier Identifier {
			get { return GetChildByRole(VariableIdentifier.VariableIdentifierRole); }
			set { SetChildByRole(VariableIdentifier.VariableIdentifierRole, value); }
		}

		public AstType Type {
			get { return GetChildByRole(Roles.Type); }
			set { SetChildByRole(Roles.Type, value); }
		}

		public VBTokenNode AssignToken {
			get { return GetChildByRole (Roles.Assign); }
		}

		public Expression Expression {
			get { return GetChildByRole (Roles.Expression); }
			set { SetChildByRole (Roles.Expression, value); }
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitVariableInitializer(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			VariableInitializer o = other as VariableInitializer;
			return o != null && this.Identifier.DoMatch(o.Identifier, match) && this.Type.DoMatch(o.Type, match) && this.Expression.DoMatch(o.Expression, match);
		}
	}
}
