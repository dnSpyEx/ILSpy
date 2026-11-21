// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.TypeName {
	public class PrimitiveType : AstType
	{
		public string Keyword { get; set; }
		public CSharp.Syntax.TextLocation Location { get; set; }

		public PrimitiveType()
		{
		}

		public PrimitiveType(string keyword)
		{
			this.Keyword = keyword;
		}

		public PrimitiveType(string keyword, CSharp.Syntax.TextLocation location)
		{
			this.Keyword = keyword;
			this.Location = location;
		}

		public override CSharp.Syntax.TextLocation StartLocation {
			get {
				return Location;
			}
		}
		public override CSharp.Syntax.TextLocation EndLocation {
			get {
				return new CSharp.Syntax.TextLocation (Location.Line, Location.Column + (Keyword != null ? Keyword.Length : 0));
			}
		}

		public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitPrimitiveType(this, data);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			PrimitiveType o = other as PrimitiveType;
			return o != null && MatchString(this.Keyword, o.Keyword);
		}

		public override string ToString()
		{
			return Keyword ?? base.ToString();
		}
	}
}
