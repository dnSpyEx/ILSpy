// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions {
	public class XmlLiteralString : AstNode
	{
		public static readonly new XmlLiteralString Null = new XmlLiteralString();

		class NullXmlLiteralString : XmlLiteralString
		{
			public override bool IsNull {
				get {
					return true;
				}
			}

			public override S AcceptVisitor<T, S> (IAstVisitor<T, S> visitor, T data)
			{
				return default(S);
			}

			protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
			{
				return other == null || other.IsNull;
			}
		}

		public string Value { get; set; }

		CSharp.Syntax.TextLocation startLocation;
		public override CSharp.Syntax.TextLocation StartLocation {
			get { return startLocation; }
		}

		CSharp.Syntax.TextLocation endLocation;
		public override CSharp.Syntax.TextLocation EndLocation {
			get { return endLocation; }
		}

		private XmlLiteralString()
		{
			this.Value = string.Empty;
		}

		public XmlLiteralString(string value, CSharp.Syntax.TextLocation startLocation, CSharp.Syntax.TextLocation endLocation)
		{
			this.Value = value;
			this.startLocation = startLocation;
			this.endLocation = endLocation;
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			var ident = other as XmlLiteralString;
			return ident != null
				&& MatchStringXml(Value, ident.Value)
				&& ident.startLocation == startLocation
				&& ident.endLocation == endLocation;
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitXmlLiteralString(this, data);
		}
	}
}
