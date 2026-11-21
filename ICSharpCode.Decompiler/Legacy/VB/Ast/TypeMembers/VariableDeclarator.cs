// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;

using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;
using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.TypeMembers
{
	/// <remarks>
	/// VariableIdentifiers As ObjectCreationExpression <br />
	/// VariableIdentifiers ( As TypeName )? ( Equals Expression )?
	/// </remarks>
	public abstract class VariableDeclarator : AstNode
	{
		public static readonly CSharp.Syntax.Role<VariableDeclarator> VariableDeclaratorRole = new CSharp.Syntax.Role<VariableDeclarator>("VariableDeclarator", null);

		public AstNodeCollection<VariableIdentifier> Identifiers {
			get { return GetChildrenByRole(VariableIdentifier.VariableIdentifierRole); }
		}
	}

	public class VariableDeclaratorWithTypeAndInitializer : VariableDeclarator
	{
		public AstType Type {
			get { return GetChildByRole(Roles.Type); }
			set { SetChildByRole(Roles.Type, value); }
		}

		public Expression Initializer {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitVariableDeclaratorWithTypeAndInitializer(this, data);
		}
	}

	public class VariableDeclaratorWithObjectCreation : VariableDeclarator
	{
		public static readonly CSharp.Syntax.Role<ObjectCreationExpression> InitializerRole = new CSharp.Syntax.Role<ObjectCreationExpression>("InitializerRole", null);

		public ObjectCreationExpression Initializer {
			get { return GetChildByRole(InitializerRole); }
			set { SetChildByRole(InitializerRole, value); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			throw new NotImplementedException();
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitVariableDeclaratorWithObjectCreation(this, data);
		}
	}
}
