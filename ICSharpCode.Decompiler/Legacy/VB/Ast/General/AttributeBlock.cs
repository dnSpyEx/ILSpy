// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


namespace ICSharpCode.Decompiler.Legacy.VB.Ast.General {
	public class AttributeBlock : AstNode
	{
		public readonly static CSharp.Syntax.Role<AttributeBlock> AttributeBlockRole = new CSharp.Syntax.Role<AttributeBlock>("AttributeBlock", null);
		public readonly static CSharp.Syntax.Role<AttributeBlock> ReturnTypeAttributeBlockRole = new CSharp.Syntax.Role<AttributeBlock>("ReturnTypeAttributeBlock", null);

		public VBTokenNode LChevron {
			get { return GetChildByRole(Roles.LChevron); }
		}

		public AstNodeCollection<Attribute> Attributes {
			get { return GetChildrenByRole(Attribute.AttributeRole); }
		}

		public VBTokenNode RChevron {
			get { return GetChildByRole(Roles.RChevron); }
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			var node = other as AttributeBlock;
			return node != null && Attributes.DoMatch(node.Attributes, match);
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitAttributeBlock(this, data);
		}
	}
}
