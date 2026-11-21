// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.GlobalScope;
using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.General {
	/// <summary>
	/// [In|Out] Name [As Contraints]
	///
	/// Represents a type parameter.
	/// </summary>
	public class TypeParameterDeclaration : AstNode
	{
		public static readonly CSharp.Syntax.Role<AstType> TypeConstraintRole = TypeDeclaration.InheritsTypeRole;
		public static readonly CSharp.Syntax.Role<VBTokenNode> VarianceRole = new CSharp.Syntax.Role<VBTokenNode>("Variance", null);

		public TypeSystem.VarianceModifier Variance { get; set; }

		public string Name {
			get { return GetChildByRole (Roles.Identifier).Name; }
//			set { SetChildByRole(Roles.Identifier, new Identifier (TextToken.Default, value, TextLocation.Empty)); }
		}

		public Identifier NameToken {
			get { return GetChildByRole(Roles.Identifier); }
			set { SetChildByRole(Roles.Identifier, value); }
		}

		public AstNodeCollection<AstType> Constraints {
			get { return GetChildrenByRole(TypeConstraintRole); }
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitTypeParameterDeclaration(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			TypeParameterDeclaration o = other as TypeParameterDeclaration;
			return o != null && this.Variance == o.Variance
				&& MatchString(this.Name, o.Name)
				&& this.Constraints.DoMatch(o.Constraints, match);
		}
	}
}
