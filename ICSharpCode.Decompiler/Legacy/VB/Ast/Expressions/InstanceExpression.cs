// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;

using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ICSharpCode.Decompiler.Legacy.VB.Ast.Expressions
{
	/// <summary>
	/// Description of InstanceExpression.
	/// </summary>
	public class InstanceExpression : Expression
	{
		TextLocation location;

		public InstanceExpression(InstanceExpressionType type, TextLocation location)
		{
			this.Type = type;
			this.location = location;
		}

		public override TextLocation StartLocation {
			get { return location; }
		}

		public override TextLocation EndLocation {
			get {
				switch (Type) {
					case InstanceExpressionType.Me:
						return new TextLocation(location.Line, location.Column + "Me".Length);
					case InstanceExpressionType.MyBase:
						return new TextLocation(location.Line, location.Column + "MyBase".Length);
					case InstanceExpressionType.MyClass:
						return new TextLocation(location.Line, location.Column + "MyClass".Length);
					default:
						#pragma warning disable CA1065
						throw new Exception("Invalid value for InstanceExpressionType");
					#pragma warning restore CA1065
				}
			}
		}

		public InstanceExpressionType Type { get; set; }

		protected internal override bool DoMatch(AstNode other, ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching.Match match)
		{
			var expr = other as InstanceExpression;
			return expr != null &&
				Type == expr.Type;
		}

		public override S AcceptVisitor<T, S>(IAstVisitor<T, S> visitor, T data)
		{
			return visitor.VisitInstanceExpression(this, data);
		}
	}

	public enum InstanceExpressionType
	{
		Me,
		MyBase,
		MyClass
	}
}
