// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


using ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching;
using ICSharpCode.Decompiler.Legacy.ILAst;
using ICSharpCode.Decompiler.Legacy.VB.Ast.General;
using ICSharpCode.Decompiler.Legacy.VB.Ast.Statements;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.TypeMembers {
	/// <summary>
	/// Get/Set/AddHandler/RemoveHandler/RaiseEvent
	/// </summary>
	public class Accessor : AttributedNode
	{
		public static readonly new Accessor Null = new NullAccessor ();
		sealed class NullAccessor : Accessor
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
		}

		public BlockStatement Body {
			get { return GetChildByRole(Roles.Body); }
			set { SetChildByRole(Roles.Body, value); }
		}

		public AstNodeCollection<ParameterDeclaration> Parameters {
			get { return GetChildrenByRole(Roles.Parameter); }
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitAccessor(this, data);
		}

		protected internal override bool DoMatch(AstNode other, Match match)
		{
			Accessor o = other as Accessor;
			return o != null && !o.IsNull && this.MatchAttributesAndModifiers(o, match) &&
				this.Body.DoMatch(o.Body, match) && Parameters.DoMatch(o.Parameters, match);
		}
	}
}
