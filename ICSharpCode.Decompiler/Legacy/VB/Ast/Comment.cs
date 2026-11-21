// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


namespace ICSharpCode.Decompiler.Legacy.VB.Ast {
	public class Comment : AstNode
	{
		public bool IsDocumentationComment { get; set; }

		public bool StartsLine {
			get;
			set;
		}

		public string Content {
			get;
			set;
		}

		CSharp.Syntax.TextLocation startLocation;
		public override CSharp.Syntax.TextLocation StartLocation {
			get {
				return startLocation;
			}
		}

		CSharp.Syntax.TextLocation endLocation;
		public override CSharp.Syntax.TextLocation EndLocation {
			get {
				return endLocation;
			}
		}

		public CSharp.CommentReference[] References { get; set; }

		public Comment (string content, bool isDocumentation = false)
		{
			this.IsDocumentationComment = isDocumentation;
			this.Content = content;
		}

		public Comment (bool isDocumentation, CSharp.Syntax.TextLocation startLocation, CSharp.Syntax.TextLocation endLocation)
		{
			this.IsDocumentationComment = isDocumentation;
			this.startLocation = startLocation;
			this.endLocation = endLocation;
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitComment(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			Comment o = other as Comment;
			return o != null && this.IsDocumentationComment == o.IsDocumentationComment && MatchString(this.Content, o.Content);
		}
	}
}
