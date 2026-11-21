// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)


namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions {
	/// <summary>
	/// Description of XmlIdentifier.
	/// </summary>
	public class XmlIdentifier : AstNode
	{
		public static readonly new XmlIdentifier Null = new NullXmlIdentifier();

		class NullXmlIdentifier : XmlIdentifier
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

		public string Name { get; set; }

		CSharp.Syntax.TextLocation startLocation;
		public override CSharp.Syntax.TextLocation StartLocation {
			get { return startLocation; }
		}

		CSharp.Syntax.TextLocation endLocation;
		public override CSharp.Syntax.TextLocation EndLocation {
			get { return endLocation; }
		}

		private XmlIdentifier()
		{
			this.Name = string.Empty;
		}

		public XmlIdentifier(string name, CSharp.Syntax.TextLocation startLocation, CSharp.Syntax.TextLocation endLocation)
		{
			this.Name = name;
			this.startLocation = startLocation;
			this.endLocation = endLocation;
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			var ident = other as XmlIdentifier;
			return ident != null
				&& MatchStringXml(Name, ident.Name)
				&& ident.startLocation == startLocation
				&& ident.endLocation == endLocation;
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitXmlIdentifier(this, data);
		}
	}
}
