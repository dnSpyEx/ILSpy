// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;

using dnSpy.Contracts.Text;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast {
	/// <summary>
	/// Represents an identifier in VB.
	/// </summary>
	public class Identifier : AstNode
	{
		public static readonly new Identifier Null = new NullIdentifier ();
		class NullIdentifier : Identifier
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

			protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
			{
				return other == null || other.IsNull;
			}
		}

		string name;

		public string Name {
			get { return name; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				name = value;
			}
		}

		public TypeCode TypeCharacter { get; set; }

		CSharp.Syntax.TextLocation startLocation;
		public override CSharp.Syntax.TextLocation StartLocation {
			get {
				return startLocation;
			}
		}

		public override CSharp.Syntax.TextLocation EndLocation {
			get {
				return new CSharp.Syntax.TextLocation (StartLocation.Line, StartLocation.Column + Name.Length);
			}
		}

		Identifier()
		{
			this.name = string.Empty;
		}

		public Identifier (object annotation, string name, CSharp.Syntax.TextLocation location)
		{
			if (name == null)
				throw new ArgumentNullException("name");
			this.Name = name;
			if (annotation != null)
				this.AddAnnotation(annotation);
			this.startLocation = location;
		}

		public static Identifier Create(object annotation, string name)
		{
			return new Identifier(annotation, name, CSharp.Syntax.TextLocation.Empty);
		}

		public static Identifier Create(IEnumerable<object> annotations, string name)
		{
			return Create(annotations, name, CSharp.Syntax.TextLocation.Empty);
		}

		public static Identifier Create(IEnumerable<object> annotations, string name, CSharp.Syntax.TextLocation textLoc)
		{
			var id = new Identifier(null, name, textLoc);
			if (annotations != null) {
				foreach (var ann in annotations)
					id.AddAnnotation(ann);
			}
			return id;
		}

		public static Identifier CreateLiteralField(string name)
		{
			return new Identifier(BoxedTextColor.LiteralField, name, CSharp.Syntax.TextLocation.Empty);
		}

		protected internal override bool DoMatch(AstNode other, CSharp.Syntax.PatternMatching.Match match)
		{
			var node = other as Identifier;
			return node != null
				&& MatchString(node.name, name)
				&& node.TypeCharacter == TypeCharacter;
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitIdentifier(this, data);
		}

		public override string ToString()
		{
			return string.Format("{0}",
			                     name);
		}
	}
}
