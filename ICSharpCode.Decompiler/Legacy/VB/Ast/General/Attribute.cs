// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions;
using ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.General {
	public class Attribute : AstNode
	{
		#region PatternPlaceholder
		public static implicit operator Attribute(CSharp.Syntax.PatternMatching.Pattern pattern)
		{
			return pattern != null ? new PatternPlaceholder(pattern) : null;
		}

		sealed class PatternPlaceholder : Attribute, CSharp.Syntax.PatternMatching.INode
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

		public static readonly CSharp.Syntax.Role<Attribute> AttributeRole = new CSharp.Syntax.Role<Attribute>("Attribute", null);
		public static readonly CSharp.Syntax.Role<VBTokenNode> TargetRole = new CSharp.Syntax.Role<VBTokenNode>("Target", VBTokenNode.Null);

		public AttributeTarget Target { get; set; }

		public VBTokenNode TargetKeyword {
			get { return GetChildByRole(TargetRole); }
			set { SetChildByRole(TargetRole, value); }
		}

		public VBTokenNode ColonToken {
			get { return GetChildByRole(Roles.StatementTerminator); }
		}

		public AstType Type {
			get { return GetChildByRole(Roles.Type); }
			set { SetChildByRole(Roles.Type, value); }
		}

		public VBTokenNode LParToken {
			get { return GetChildByRole(Roles.LPar); }
		}

		public AstNodeCollection<Expression> Arguments {
			get { return GetChildrenByRole(Roles.Argument); }
		}

		public VBTokenNode RParToken {
			get { return GetChildByRole(Roles.RPar); }
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitAttribute(this, data);
		}

//		public override string ToString()
//		{
//			return string.Format("[Attribute Type={0} Arguments={1}]", Type, GetCollectionString(Arguments));
//		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			var node = other as Attribute;
			return node != null && node.Target == Target && node.TargetKeyword.DoMatch(this.TargetKeyword, match) && node.Type.DoMatch(this.Type, match) && node.Arguments.DoMatch(this.Arguments, match);
		}
	}

	public enum AttributeTarget
	{
		None,
		Assembly,
		Module
	}
}
