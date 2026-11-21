// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;
using ICSharpCode.Decompiler.Legacy.VB.Ast.General;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.GlobalScope {
	public class EnumMemberDeclaration : AstNode
	{
		public AstNodeCollection<AttributeBlock> Attributes {
			get { return GetChildrenByRole(AttributeBlock.AttributeBlockRole); }
		}

		public Identifier Name {
			get { return GetChildByRole(Roles.Identifier); }
			set { SetChildByRole(Roles.Identifier, value); }
		}

		public Expression Value {
			get { return GetChildByRole(Roles.Expression); }
			set { SetChildByRole(Roles.Expression, value); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			var member = other as EnumMemberDeclaration;
			return Attributes.DoMatch(member.Attributes, match) &&
				Name.DoMatch(member.Name, match) &&
				Value.DoMatch(member.Value, match);
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitEnumMemberDeclaration(this, data);
		}
	}
}
