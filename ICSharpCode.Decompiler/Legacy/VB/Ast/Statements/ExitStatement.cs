// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Statements {
	/// <summary>
	/// Exit ( Do | For | While | Select | Sub | Function | Property | Try )
	/// </summary>
	public class ExitStatement : Statement
	{
		public static readonly CSharp.Syntax.Role<VBTokenNode> ExitKindTokenRole = new CSharp.Syntax.Role<VBTokenNode>("ExitKindToken", null);

		public ExitKind ExitKind { get; set; }

		public VBTokenNode ExitToken {
			get { return GetChildByRole (Roles.Keyword); }
		}

		public VBTokenNode ExitKindToken {
			get { return GetChildByRole (ExitKindTokenRole); }
		}

		public ExitStatement(ExitKind kind)
		{
			this.ExitKind = kind;
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitExitStatement(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			ExitStatement o = other as ExitStatement;
			return o != null && this.ExitKind == o.ExitKind;
		}
	}

	public enum ExitKind
	{
		None,
		Sub,
		Function,
		Property,
		Do,
		For,
		While,
		Select,
		Try
	}
}
