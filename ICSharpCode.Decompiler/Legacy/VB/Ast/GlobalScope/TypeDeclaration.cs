// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.General;
using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.GlobalScope {
	public enum ClassType
	{
		Class,
		Struct,
		Interface,
		Module
	}

	public class TypeDeclaration : AttributedNode
	{
		public readonly static CSharp.Syntax.Role<AttributedNode> MemberRole = new CSharp.Syntax.Role<AttributedNode>("Member", null);
		public readonly static CSharp.Syntax.Role<AstType> InheritsTypeRole = new CSharp.Syntax.Role<AstType>("InheritsType", AstType.Null);
		public readonly static CSharp.Syntax.Role<AstType> ImplementsTypesRole = new CSharp.Syntax.Role<AstType>("ImplementsTypes", AstType.Null);

		public AstNodeCollection<AttributedNode> Members {
			get { return base.GetChildrenByRole(MemberRole); }
		}

		public ClassType ClassType { get; set; }

		public Identifier Name {
			get { return GetChildByRole(Roles.Identifier); }
			set { SetChildByRole(Roles.Identifier, value); }
		}

		public AstNodeCollection<TypeParameterDeclaration> TypeParameters {
			get { return GetChildrenByRole(Roles.TypeParameter); }
		}

		public AstType InheritsType {
			get { return GetChildByRole(InheritsTypeRole); }
			set { SetChildByRole(InheritsTypeRole, value); }
		}

		public AstNodeCollection<AstType> ImplementsTypes {
			get { return GetChildrenByRole(ImplementsTypesRole); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			TypeDeclaration t = other as TypeDeclaration;
			return t != null &&
				MatchAttributesAndModifiers(t, match) &&
				Members.DoMatch(t.Members, match) &&
				ClassType == t.ClassType &&
				Name.DoMatch(t.Name, match) &&
				TypeParameters.DoMatch(t.TypeParameters, match) &&
				InheritsType.DoMatch(t.InheritsType, match) &&
				ImplementsTypes.DoMatch(t.ImplementsTypes, match);
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitTypeDeclaration(this, data);
		}
	}
}
