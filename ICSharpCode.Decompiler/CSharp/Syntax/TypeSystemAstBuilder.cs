﻿// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.CSharp.TypeSystem;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.CSharp.Syntax
{
	/// <summary>
	/// Converts from type system to the C# AST.
	/// </summary>
	public class TypeSystemAstBuilder
	{
		readonly CSharpResolver resolver;

		#region Constructor
		/// <summary>
		/// Creates a new TypeSystemAstBuilder.
		/// </summary>
		/// <param name="resolver">
		/// A resolver initialized for the position where the type will be inserted.
		/// </param>
		public TypeSystemAstBuilder(CSharpResolver resolver)
		{
			if (resolver == null)
				throw new ArgumentNullException("resolver");
			this.resolver = resolver;
			InitProperties();
		}

		/// <summary>
		/// Creates a new TypeSystemAstBuilder.
		/// </summary>
		public TypeSystemAstBuilder()
		{
			InitProperties();
		}
		#endregion

		#region Properties
		void InitProperties()
		{
			this.AlwaysUseBuiltinTypeNames = true;
			this.ShowAccessibility = true;
			this.ShowModifiers = true;
			this.ShowBaseTypes = true;
			this.ShowTypeParameters = true;
			this.ShowTypeParameterConstraints = true;
			this.ShowParameterNames = true;
			this.ShowConstantValues = true;
			this.UseAliases = true;
			this.UseSpecialConstants = true;
		}

		/// <summary>
		/// Specifies whether the ast builder should add annotations to type references.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool AddTypeReferenceAnnotations { get; set; }

		/// <summary>
		/// Specifies whether the ast builder should add ResolveResult annotations to AST nodes.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool AddResolveResultAnnotations { get; set; }

		/// <summary>
		/// Controls the accessibility modifiers are shown.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowAccessibility { get; set; }

		/// <summary>
		/// Controls the non-accessibility modifiers are shown.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowModifiers { get; set; }

		/// <summary>
		/// Controls whether base type references are shown.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowBaseTypes { get; set; }

		/// <summary>
		/// Controls whether type parameter declarations are shown.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowTypeParameters { get; set; }

		/// <summary>
		/// Controls whether constraints on type parameter declarations are shown.
		/// Has no effect if ShowTypeParameters is false.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowTypeParameterConstraints { get; set; }

		/// <summary>
		/// Controls whether the names of parameters are shown.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowParameterNames { get; set; }

		/// <summary>
		/// Controls whether to show default values of optional parameters, and the values of constant fields.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool ShowConstantValues { get; set; }

		/// <summary>
		/// Controls whether to show attributes.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool ShowAttributes { get; set; }

		/// <summary>
		/// Controls whether to use fully-qualified type names or short type names.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool AlwaysUseShortTypeNames { get; set; }

		/// <summary>
		/// Controls whether to use fully-qualified type names or short type names.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool AlwaysUseBuiltinTypeNames { get; set; }

		/// <summary>
		/// Determines the name lookup mode for converting a type name.
		///
		/// The default value is <c>NameLookupMode.Expression</c>, which means the name is disambiguated
		/// for use in expression context.
		/// </summary>
		public NameLookupMode NameLookupMode { get; set; }

		/// <summary>
		/// Controls whether to generate a body that throws a <c>System.NotImplementedException</c>.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool GenerateBody { get; set; }

		/// <summary>
		/// Controls whether to generate custom events.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool UseCustomEvents { get; set; }

		/// <summary>
		/// Controls if unbound type argument names are inserted in the ast or not.
		/// The default value is <c>false</c>.
		/// </summary>
		public bool ConvertUnboundTypeArguments { get; set;}

		/// <summary>
		/// Controls if aliases should be used inside the type name or not.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool UseAliases { get; set; }

		/// <summary>
		/// Controls if constants like <c>int.MaxValue</c> are converted to a <see cref="MemberReferenceExpression"/> or <see cref="PrimitiveExpression" />.
		/// The default value is <c>true</c>.
		/// </summary>
		public bool UseSpecialConstants { get; set; }
		#endregion

		#region Convert Type
		public AstType ConvertType(IType type)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			AstType astType = ConvertTypeHelper(type);
			if (AddTypeReferenceAnnotations)
				astType.AddAnnotation(type);
			if (AddResolveResultAnnotations)
				astType.AddAnnotation(new TypeResolveResult(type));
			return astType;
		}

		public AstType ConvertType(FullTypeName fullTypeName)
		{
			if (resolver != null) {
				foreach (var asm in resolver.Compilation.Modules) {
					var def = asm.GetTypeDefinition(fullTypeName);
					if (def != null) {
						return ConvertType(def);
					}
				}
			}
			TopLevelTypeName top = fullTypeName.TopLevelTypeName;
			AstType type;
			if (string.IsNullOrEmpty(top.Namespace)) {
				type = new SimpleType(top.Name);
			} else {
				type = new SimpleType(top.Namespace).MemberType(top.Name);
			}
			for (int i = 0; i < fullTypeName.NestingLevel; i++) {
				type = type.MemberType(fullTypeName.GetNestedTypeName(i));
			}
			return type;
		}

		AstType ConvertTypeHelper(IType type)
		{
			TypeWithElementType typeWithElementType = type as TypeWithElementType;
			if (typeWithElementType != null) {
				if (typeWithElementType is PointerType) {
					return ConvertType(typeWithElementType.ElementType).MakePointerType();
				} else if (typeWithElementType is ArrayType) {
					return ConvertType(typeWithElementType.ElementType).MakeArrayType(((ArrayType)type).Dimensions);
				} else if (typeWithElementType is ByReferenceType) {
					return ConvertType(typeWithElementType.ElementType).MakeRefType();
				} else {
					// not supported as type in C#
					return ConvertType(typeWithElementType.ElementType);
				}
			}
			if (type is ParameterizedType pt) {
				if (AlwaysUseBuiltinTypeNames && pt.IsKnownType(KnownTypeCode.NullableOfT)) {
					return ConvertType(pt.TypeArguments[0]).MakeNullableType();
				}
				return ConvertTypeHelper(pt.GenericType, pt.TypeArguments);
			}
			if (type is TupleType tuple) {
				var astType = new TupleAstType();
				foreach (var (etype, ename) in tuple.ElementTypes.Zip(tuple.ElementNames)) {
					astType.Elements.Add(new TupleTypeElement {
						Type = ConvertType(etype),
						Name = ename
					});
				}
				return astType;
			}
			if (type is ITypeDefinition typeDef) {
				if (typeDef.TypeParameterCount > 0) {
					// Unbound type
					IType[] typeArguments = new IType[typeDef.TypeParameterCount];
					for (int i = 0; i < typeArguments.Length; i++) {
						typeArguments[i] = SpecialType.UnboundTypeArgument;
					}
					return ConvertTypeHelper(typeDef, typeArguments);
				} else {
					return ConvertTypeHelper(typeDef, EmptyList<IType>.Instance);
				}
			}
			return new SimpleType(type.Name);
		}

		AstType ConvertTypeHelper(IType genericType, IReadOnlyList<IType> typeArguments)
		{
			Debug.Assert(typeArguments.Count >= genericType.TypeParameterCount);
			Debug.Assert(genericType is ITypeDefinition || genericType.Kind == TypeKind.Unknown);

			ITypeDefinition typeDef = genericType as ITypeDefinition;
			if (AlwaysUseBuiltinTypeNames && typeDef != null) {
				string keyword = KnownTypeReference.GetCSharpNameByTypeCode(typeDef.KnownTypeCode);
				if (keyword != null)
					return new PrimitiveType(keyword);
			}

			// The number of type parameters belonging to outer classes
			int outerTypeParameterCount = genericType.DeclaringType?.TypeParameterCount ?? 0;

			if (resolver != null && typeDef != null) {
				// Look if there's an alias to the target type
				if (UseAliases) {
					for (ResolvedUsingScope usingScope = resolver.CurrentUsingScope; usingScope != null; usingScope = usingScope.Parent) {
						foreach (var pair in usingScope.UsingAliases) {
							if (pair.Value is TypeResolveResult) {
								if (TypeMatches(pair.Value.Type, typeDef, typeArguments))
									return new SimpleType(pair.Key);
							}
						}
					}
				}

				IType[] localTypeArguments;
				if (typeDef.TypeParameterCount > outerTypeParameterCount) {
					localTypeArguments = new IType[typeDef.TypeParameterCount - outerTypeParameterCount];
					for (int i = 0; i < localTypeArguments.Length; i++) {
						localTypeArguments[i] = typeArguments[outerTypeParameterCount + i];
					}
				} else {
					localTypeArguments = Empty<IType>.Array;
				}
				ResolveResult rr = resolver.LookupSimpleNameOrTypeName(typeDef.Name, localTypeArguments, NameLookupMode);
				TypeResolveResult trr = rr as TypeResolveResult;
				if (trr != null || (localTypeArguments.Length == 0 && resolver.IsVariableReferenceWithSameType(rr, typeDef.Name, out trr))) {
					if (!trr.IsError && TypeMatches(trr.Type, typeDef, typeArguments)) {
						// We can use the short type name
						SimpleType shortResult = new SimpleType(typeDef.Name);
						AddTypeArguments(shortResult, typeDef.TypeParameters, typeArguments, outerTypeParameterCount, typeDef.TypeParameterCount);
						return shortResult;
					}
				}
			}

			if (AlwaysUseShortTypeNames || (typeDef == null && genericType.DeclaringType == null)) {
				var shortResult = new SimpleType(genericType.Name);
				AddTypeArguments(shortResult, genericType.TypeParameters, typeArguments, outerTypeParameterCount, genericType.TypeParameterCount);
				return shortResult;
			}
			MemberType result = new MemberType();
			if (genericType.DeclaringType != null) {
				// Handle nested types
				result.Target = ConvertTypeHelper(genericType.DeclaringType, typeArguments);
			} else {
				// Handle top-level types
				if (string.IsNullOrEmpty(genericType.Namespace)) {
					result.Target = new SimpleType("global");
					if (AddResolveResultAnnotations && resolver != null)
						result.Target.AddAnnotation(new NamespaceResolveResult(resolver.Compilation.RootNamespace));
					result.IsDoubleColon = true;
				} else {
					result.Target = ConvertNamespace(genericType.Namespace, out _);
				}
			}
			result.MemberName = genericType.Name;
			AddTypeArguments(result, genericType.TypeParameters, typeArguments, outerTypeParameterCount, genericType.TypeParameterCount);
			return result;
		}

		/// <summary>
		/// Gets whether 'type' is the same as 'typeDef' parameterized with the given type arguments.
		/// </summary>
		bool TypeMatches(IType type, ITypeDefinition typeDef, IReadOnlyList<IType> typeArguments)
		{
			if (typeDef.TypeParameterCount == 0) {
				return typeDef.Equals(type);
			} else {
				if (!typeDef.Equals(type.GetDefinition()))
					return false;
				ParameterizedType pt = type as ParameterizedType;
				if (pt == null) {
					return typeArguments.All(t => t.Kind == TypeKind.UnboundTypeArgument);
				}
				var ta = pt.TypeArguments;
				for (int i = 0; i < ta.Count; i++) {
					if (!ta[i].Equals(typeArguments[i]))
						return false;
				}
				return true;
			}
		}

		/// <summary>
		/// Adds type arguments to the result type.
		/// </summary>
		/// <param name="result">The result AST node (a SimpleType or MemberType)</param>
		/// <param name="typeParameters">The type parameters</param>
		/// <param name="typeArguments">The list of type arguments</param>
		/// <param name="startIndex">Index of first type argument to add</param>
		/// <param name="endIndex">Index after last type argument to add</param>
		void AddTypeArguments(AstType result, IReadOnlyList<ITypeParameter> typeParameters, IReadOnlyList<IType> typeArguments, int startIndex, int endIndex)
		{
			Debug.Assert(endIndex <= typeParameters.Count);
			for (int i = startIndex; i < endIndex; i++) {
				if (ConvertUnboundTypeArguments && typeArguments[i].Kind == TypeKind.UnboundTypeArgument) {
					result.AddChild(new SimpleType(typeParameters[i].Name), Roles.TypeArgument);
				} else {
					result.AddChild(ConvertType(typeArguments[i]), Roles.TypeArgument);
				}
			}
		}

		public AstType ConvertNamespace(string namespaceName, out NamespaceResolveResult nrr)
		{
			if (resolver != null) {
				// Look if there's an alias to the target namespace
				if (UseAliases) {
					for (ResolvedUsingScope usingScope = resolver.CurrentUsingScope; usingScope != null; usingScope = usingScope.Parent) {
						foreach (var pair in usingScope.UsingAliases) {
							nrr = pair.Value as NamespaceResolveResult;
							if (nrr != null && nrr.NamespaceName == namespaceName) {
								var ns = new SimpleType(pair.Key);
								if (AddResolveResultAnnotations)
									ns.AddAnnotation(nrr);
								return ns;
							}
						}
					}
				}
			}

			int pos = namespaceName.LastIndexOf('.');
			if (pos < 0) {
				if (IsValidNamespace(namespaceName, out nrr)) {
					var ns = new SimpleType(namespaceName);
					if (AddResolveResultAnnotations && nrr != null)
						ns.AddAnnotation(nrr);
					return ns;
				} else {
					var target = new SimpleType("global");
					if (AddResolveResultAnnotations)
						target.AddAnnotation(new NamespaceResolveResult(resolver.Compilation.RootNamespace));
					var ns = new MemberType {
						Target = target,
						IsDoubleColon = true,
						MemberName = namespaceName
					};
					if (AddResolveResultAnnotations) {
						var @namespace = resolver.Compilation.RootNamespace.GetChildNamespace(namespaceName);
						if (@namespace != null)
							ns.AddAnnotation(nrr = new NamespaceResolveResult(@namespace));
					}
					return ns;
				}
			} else {
				string parentNamespace = namespaceName.Substring(0, pos);
				string localNamespace = namespaceName.Substring(pos + 1);
				var parentNS = ConvertNamespace(parentNamespace, out var parentNRR);
				var ns = new MemberType {
					Target = parentNS,
					MemberName = localNamespace
				};
				nrr = null;
				if (AddResolveResultAnnotations && parentNRR != null) {
					var newNamespace = parentNRR.Namespace.GetChildNamespace(localNamespace);
					if (newNamespace != null) {
						ns.AddAnnotation(nrr = new NamespaceResolveResult(newNamespace));
					}
				}
				return ns;
			}
		}

		bool IsValidNamespace(string firstNamespacePart, out NamespaceResolveResult nrr)
		{
			nrr = null;
			if (resolver == null)
				return true; // just assume namespaces are valid if we don't have a resolver
			nrr = resolver.ResolveSimpleName(firstNamespacePart, EmptyList<IType>.Instance) as NamespaceResolveResult;
			return nrr != null && !nrr.IsError && nrr.NamespaceName == firstNamespacePart;
		}
		#endregion

		#region Convert Attribute
		public Attribute ConvertAttribute(IAttribute attribute)
		{
			Attribute attr = new Attribute();
			attr.Type = ConvertAttributeType(attribute.AttributeType);
			SimpleType st = attr.Type as SimpleType;
			MemberType mt = attr.Type as MemberType;
			if (st != null && st.Identifier.EndsWith("Attribute", StringComparison.Ordinal)) {
				st.Identifier = st.Identifier.Substring(0, st.Identifier.Length - 9);
			} else if (mt != null && mt.MemberName.EndsWith("Attribute", StringComparison.Ordinal)) {
				mt.MemberName = mt.MemberName.Substring(0, mt.MemberName.Length - 9);
			}
			var parameters = attribute.Constructor?.Parameters ?? EmptyList<IParameter>.Instance;
			for (int i = 0; i < attribute.FixedArguments.Length; i++) {
				var arg = attribute.FixedArguments[i];
				var p = (i < parameters.Count) ? parameters[i] : null;
				attr.Arguments.Add(ConvertConstantValue(p?.Type ?? arg.Type, arg.Type, arg.Value));
			}
			if (attribute.NamedArguments.Length > 0) {
				InitializedObjectResolveResult targetResult = new InitializedObjectResolveResult(attribute.AttributeType);
				foreach (var namedArg in attribute.NamedArguments) {
					NamedExpression namedArgument = new NamedExpression(namedArg.Name, ConvertConstantValue(namedArg.Type, namedArg.Value));
					if (AddResolveResultAnnotations) {
						IMember member = CustomAttribute.MemberForNamedArgument(attribute.AttributeType, namedArg);
						if (member != null) {
							namedArgument.AddAnnotation(new MemberResolveResult(targetResult, member));
						}
					}
					attr.Arguments.Add(namedArgument);
				}
			}
			if (attribute.HasDecodeErrors) {
				attr.HasArgumentList = true;
				attr.AddChild(new Comment("Could not decode attribute arguments.", CommentType.MultiLine), Roles.Comment);
				// insert explicit rpar token to make the comment appear within the parentheses
				attr.AddChild(new CSharpTokenNode(TextLocation.Empty, Roles.RPar), Roles.RPar);
			}
			return attr;
		}

		private IEnumerable<AttributeSection> ConvertAttributes(IEnumerable<IAttribute> attibutes)
		{
			return attibutes.Select(a => new AttributeSection(ConvertAttribute(a)));
		}

		private IEnumerable<AttributeSection> ConvertAttributes(IEnumerable<IAttribute> attibutes, string target)
		{
			return attibutes.Select(a => new AttributeSection(ConvertAttribute(a)) {
				AttributeTarget = target
			});
		}
		#endregion

		#region Convert Attribute Type
		public AstType ConvertAttributeType(IType type)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			AstType astType = ConvertTypeHelper(type);

			string shortName = null;
			if (type.Name.Length > 9 && type.Name.EndsWith("Attribute", StringComparison.Ordinal)) {
				shortName = type.Name.Remove(type.Name.Length - 9);
			}
			if (AlwaysUseShortTypeNames) {
				switch (astType) {
					case SimpleType st:
						st.Identifier = shortName;
						break;
					case MemberType mt:
						mt.MemberName = shortName;
						break;
				}
			} else if (resolver != null) {
				ApplyShortAttributeNameIfPossible(type, astType, shortName);
			}

			if (AddTypeReferenceAnnotations)
				astType.AddAnnotation(type);
			if (AddResolveResultAnnotations)
				astType.AddAnnotation(new TypeResolveResult(type));
			return astType;
		}

		private void ApplyShortAttributeNameIfPossible(IType type, AstType astType, string shortName)
		{
			switch (astType) {
				case SimpleType st:
					ResolveResult shortRR = null;
					ResolveResult withExtraAttrSuffix = resolver.LookupSimpleNameOrTypeName(type.Name + "Attribute", EmptyList<IType>.Instance, NameLookupMode.Type);
					if (shortName != null) {
						shortRR = resolver.LookupSimpleNameOrTypeName(shortName, EmptyList<IType>.Instance, NameLookupMode.Type);
					}
					// short type is either unknown or not an attribute type -> we can use the short name.
					if (shortRR != null && (shortRR is UnknownIdentifierResolveResult || !IsAttributeType(shortRR))) {
						st.Identifier = shortName;
					} else if (IsAttributeType(withExtraAttrSuffix)) {
						// typeName + "Attribute" is an attribute type -> we cannot use long type name, add '@' to disable implicit "Attribute" suffix.
						st.Identifier = '@' + st.Identifier;
					}
					break;
				case MemberType mt:
					if (type.DeclaringType != null) {
						var declaringTypeDef = type.DeclaringType.GetDefinition();
						if (declaringTypeDef != null) {
							if (shortName != null && !declaringTypeDef.GetNestedTypes(t => t.TypeParameterCount == 0 && t.Name == shortName).Any(IsAttributeType)) {
								mt.MemberName = shortName;
							} else if (declaringTypeDef.GetNestedTypes(t => t.TypeParameterCount == 0 && t.Name == type.Name + "Attribute").Any(IsAttributeType)) {
								mt.MemberName = '@' + mt.MemberName;
							}
						}
					} else if (mt.Target.GetResolveResult() is NamespaceResolveResult nrr) {
						if (shortName != null && !IsAttributeType(nrr.Namespace.GetTypeDefinition(shortName, 0))) {
							mt.MemberName = shortName;
						} else if (IsAttributeType(nrr.Namespace.GetTypeDefinition(type.Name + "Attribute", 0))) {
							mt.MemberName = '@' + mt.MemberName;
						}
					}
					break;
			}
		}

		private bool IsAttributeType(IType type)
		{
			return type != null && type.GetNonInterfaceBaseTypes().Any(t => t.IsKnownType(KnownTypeCode.Attribute));
		}

		private bool IsAttributeType(ResolveResult rr)
		{
			return rr is TypeResolveResult trr && IsAttributeType(trr.Type);
		}
		#endregion

		#region Convert Constant Value
		/// <summary>
		/// Creates an Expression for the given constant value.
		///
		/// Note: the returned expression is not necessarily of the desired type.
		/// However, the returned expression will always be implicitly convertible to <c>rr.Type</c>,
		/// and will have the correct value when being converted in this way.
		/// </summary>
		public Expression ConvertConstantValue(ResolveResult rr)
		{
			if (rr == null)
				throw new ArgumentNullException("rr");
			bool isBoxing = false;
			if (rr is ConversionResolveResult crr) {
				// unpack ConversionResolveResult if necessary
				// (e.g. a boxing conversion or string->object reference conversion)
				rr = crr.Input;
				isBoxing = crr.Conversion.IsBoxingConversion;
			}

			if (rr is TypeOfResolveResult) {
				var expr = new TypeOfExpression(ConvertType(((TypeOfResolveResult)rr).ReferencedType));
				if (AddResolveResultAnnotations)
					expr.AddAnnotation(rr);
				return expr;
			} else if (rr is ArrayCreateResolveResult acrr) {
				ArrayCreateExpression ace = new ArrayCreateExpression();
				ace.Type = ConvertType(acrr.Type);
				if (ace.Type is ComposedType composedType) {
					composedType.ArraySpecifiers.MoveTo(ace.AdditionalArraySpecifiers);
					if (!composedType.HasNullableSpecifier && composedType.PointerRank == 0)
						ace.Type = composedType.BaseType;
				}

				if (acrr.SizeArguments != null && acrr.InitializerElements == null) {
					ace.AdditionalArraySpecifiers.FirstOrNullObject().Remove();
					ace.Arguments.AddRange(acrr.SizeArguments.Select(ConvertConstantValue));
				}
				if (acrr.InitializerElements != null) {
					ArrayInitializerExpression initializer = new ArrayInitializerExpression();
					initializer.Elements.AddRange(acrr.InitializerElements.Select(ConvertConstantValue));
					ace.Initializer = initializer;
				}
				if (AddResolveResultAnnotations)
					ace.AddAnnotation(rr);
				return ace;
			} else if (rr.IsCompileTimeConstant) {
				var expr = ConvertConstantValue(rr.Type, rr.ConstantValue);
				if (isBoxing && rr.Type.IsCSharpSmallIntegerType()) {
					// C# does not have small integer literal types.
					// We need to add a cast so that the integer literal gets boxed as the correct type.
					expr = new CastExpression(ConvertType(rr.Type), expr);
					if (AddResolveResultAnnotations)
						expr.AddAnnotation(rr);
				}
				return expr;
			} else {
				return new ErrorExpression();
			}
		}

		/// <summary>
		/// Creates an Expression for the given constant value.
		///
		/// Note: the returned expression is not necessarily of the specified <paramref name="type"/>:
		/// For example, <c>ConvertConstantValue(typeof(string), null)</c> results in a <c>null</c> literal,
		/// without a cast to <c>string</c>.
		/// Similarly, <c>ConvertConstantValue(typeof(short), 1)</c> results in the literal <c>1</c>,
		/// which is of type <c>int</c>.
		/// However, the returned expression will always be implicitly convertible to <paramref name="type"/>.
		/// </summary>
		public Expression ConvertConstantValue(IType type, object constantValue)
		{
			return ConvertConstantValue(type, type, constantValue);
		}

		/// <summary>
		/// Creates an Expression for the given constant value.
		/// </summary>
		public Expression ConvertConstantValue(IType expectedType, IType type, object constantValue)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			if (constantValue == null) {
				if (type.IsReferenceType == true) {
					var expr = new NullReferenceExpression();
					if (AddResolveResultAnnotations)
						expr.AddAnnotation(new ConstantResolveResult(SpecialType.NullType, null));
					return expr;
				} else {
					var expr = new DefaultValueExpression(ConvertType(type));
					if (AddResolveResultAnnotations)
						expr.AddAnnotation(new ConstantResolveResult(type, null));
					return expr;
				}
			} else if (constantValue is IType typeofType) {
				var expr = new TypeOfExpression(ConvertType(typeofType));
				if (AddResolveResultAnnotations)
					expr.AddAnnotation(new TypeOfResolveResult(type, typeofType));
				return expr;
			} else if (constantValue is ImmutableArray<CustomAttributeTypedArgument<IType>> arr) {
				var elementType = (type as ArrayType)?.ElementType ?? SpecialType.UnknownType;
				var expr = new ArrayCreateExpression();
				expr.Type = ConvertType(type);
				if (expr.Type is ComposedType composedType) {
					composedType.ArraySpecifiers.MoveTo(expr.AdditionalArraySpecifiers);
					if (!composedType.HasNullableSpecifier && composedType.PointerRank == 0)
						expr.Type = composedType.BaseType;
				}
				expr.Initializer = new ArrayInitializerExpression(arr.Select(e => ConvertConstantValue(elementType, e.Type, e.Value)));
				return expr;
			} else if (type.Kind == TypeKind.Enum) {
				return ConvertEnumValue(type, (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, constantValue, false));
			} else {
				if (IsSpecialConstant(type, constantValue, out var expr))
					return expr;
				IType literalType = type;
				bool smallInteger = type.IsCSharpSmallIntegerType();
				if (smallInteger) {
					// C# does not have integer literals of small integer types,
					// use `int` literal instead.
					constantValue = CSharpPrimitiveCast.Cast(TypeCode.Int32, constantValue, false);
					literalType = type.GetDefinition().Compilation.FindType(KnownTypeCode.Int32);
				}
				expr = new PrimitiveExpression(constantValue);
				if (AddResolveResultAnnotations)
					expr.AddAnnotation(new ConstantResolveResult(literalType, constantValue));
				if (smallInteger && !type.Equals(expectedType)) {
					expr = new CastExpression(ConvertType(type), expr);
				}
				return expr;
			}
		}

		bool IsSpecialConstant(IType type, object constant, out Expression expression)
		{
			expression = null;
			if (!specialConstants.TryGetValue(constant, out var info))
				return false;
			if (!UseSpecialConstants) {
				// +Infty, -Infty and NaN, cannot be represented in their encoded form.
				// Use an equivalent arithmetic expression instead.
				if (info.Type == KnownTypeCode.Double) {
					switch ((double)constant) {
						case double.NegativeInfinity: // (-1.0 / 0.0)
							var left = new PrimitiveExpression(-1.0).WithoutILInstruction().WithRR(new ConstantResolveResult(type, -1.0));
							var right = new PrimitiveExpression(0.0).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0));
							expression = new BinaryOperatorExpression(left, BinaryOperatorType.Divide, right).WithoutILInstruction()
								.WithRR(new ConstantResolveResult(type, double.NegativeInfinity));
							return true;
						case double.PositiveInfinity: // (1.0 / 0.0)
							left = new PrimitiveExpression(1.0).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 1.0));
							right = new PrimitiveExpression(0.0).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0));
							expression = new BinaryOperatorExpression(left, BinaryOperatorType.Divide, right).WithoutILInstruction()
								.WithRR(new ConstantResolveResult(type, double.PositiveInfinity));
							return true;
						case double.NaN: // (0.0 / 0.0)
							left = new PrimitiveExpression(0.0).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0));
							right = new PrimitiveExpression(0.0).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0));
							expression = new BinaryOperatorExpression(left, BinaryOperatorType.Divide, right).WithoutILInstruction()
								.WithRR(new ConstantResolveResult(type, double.NaN));
							return true;
					}
				}
				if (info.Type == KnownTypeCode.Single) {
					switch ((float)constant) {
						case float.NegativeInfinity: // (-1.0f / 0.0f)
							var left = new PrimitiveExpression(-1.0f).WithoutILInstruction().WithRR(new ConstantResolveResult(type, -1.0f));
							var right = new PrimitiveExpression(0.0f).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0f));
							expression = new BinaryOperatorExpression(left, BinaryOperatorType.Divide, right).WithoutILInstruction()
								.WithRR(new ConstantResolveResult(type, float.NegativeInfinity));
							return true;
						case float.PositiveInfinity: // (1.0f / 0.0f)
							left = new PrimitiveExpression(1.0f).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 1.0f));
							right = new PrimitiveExpression(0.0f).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0f));
							expression = new BinaryOperatorExpression(left, BinaryOperatorType.Divide, right).WithoutILInstruction()
								.WithRR(new ConstantResolveResult(type, float.PositiveInfinity));
							return true;
						case float.NaN: // (0.0f / 0.0f)
							left = new PrimitiveExpression(0.0f).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0f));
							right = new PrimitiveExpression(0.0f).WithoutILInstruction().WithRR(new ConstantResolveResult(type, 0.0f));
							expression = new BinaryOperatorExpression(left, BinaryOperatorType.Divide, right).WithoutILInstruction()
								.WithRR(new ConstantResolveResult(type, float.NaN));
							return true;
					}
				}
				return false;
			}

			expression = new TypeReferenceExpression(ConvertType(type));

			if (AddResolveResultAnnotations)
				expression.AddAnnotation(new TypeResolveResult(type));

			expression = new MemberReferenceExpression(expression, info.Member);

			if (AddResolveResultAnnotations)
				expression.AddAnnotation(new MemberResolveResult(new TypeResolveResult(type), type.GetFields(p => p.Name == info.Member).Single()));

			return true;
		}

		Dictionary<object, (KnownTypeCode Type, string Member)> specialConstants = new Dictionary<object, (KnownTypeCode Type, string Member)>() {
			// byte:
			{ byte.MaxValue, (KnownTypeCode.Byte, "MaxValue") },
			// sbyte:
			{ sbyte.MinValue, (KnownTypeCode.SByte, "MinValue") },
			{ sbyte.MaxValue, (KnownTypeCode.SByte, "MaxValue") },
			// short:
			{ short.MinValue, (KnownTypeCode.Int16, "MinValue") },
			{ short.MaxValue, (KnownTypeCode.Int16, "MaxValue") },
			// ushort:
			{ ushort.MaxValue, (KnownTypeCode.UInt16, "MaxValue") },
			// int:
			{ int.MinValue, (KnownTypeCode.Int32, "MinValue") },
			{ int.MaxValue, (KnownTypeCode.Int32, "MaxValue") },
			// uint:
			{ uint.MaxValue, (KnownTypeCode.UInt32, "MaxValue") },
			// long:
			{ long.MinValue, (KnownTypeCode.Int64, "MinValue") },
			{ long.MaxValue, (KnownTypeCode.Int64, "MaxValue") },
			// ulong:
			{ ulong.MaxValue, (KnownTypeCode.UInt64, "MaxValue") },
			// float:
			{ float.NaN, (KnownTypeCode.Single, "NaN") },
			{ float.NegativeInfinity, (KnownTypeCode.Single, "NegativeInfinity") },
			{ float.PositiveInfinity, (KnownTypeCode.Single, "PositiveInfinity") },
			{ float.MinValue, (KnownTypeCode.Single, "MinValue") },
			{ float.MaxValue, (KnownTypeCode.Single, "MaxValue") },
			{ float.Epsilon, (KnownTypeCode.Single, "Epsilon") },
			// double:
			{ double.NaN, (KnownTypeCode.Double, "NaN") },
			{ double.NegativeInfinity, (KnownTypeCode.Double, "NegativeInfinity") },
			{ double.PositiveInfinity, (KnownTypeCode.Double, "PositiveInfinity") },
			{ double.MinValue, (KnownTypeCode.Double, "MinValue") },
			{ double.MaxValue, (KnownTypeCode.Double, "MaxValue") },
			{ double.Epsilon, (KnownTypeCode.Double, "Epsilon") },
			// decimal:
			{ decimal.MinValue, (KnownTypeCode.Decimal, "MinValue") },
			{ decimal.MaxValue, (KnownTypeCode.Decimal, "MaxValue") },
		};

		bool IsFlagsEnum(ITypeDefinition type)
		{
			return type.HasAttribute(KnownAttribute.Flags, inherit: false);
		}

		Expression ConvertEnumValue(IType type, long val)
		{
			ITypeDefinition enumDefinition = type.GetDefinition();
			TypeCode enumBaseTypeCode = ReflectionHelper.GetTypeCode(enumDefinition.EnumUnderlyingType);
			foreach (IField field in enumDefinition.Fields) {
				if (field.IsConst && object.Equals(CSharpPrimitiveCast.Cast(TypeCode.Int64, field.ConstantValue, false), val)) {
					MemberReferenceExpression mre = new MemberReferenceExpression(new TypeReferenceExpression(ConvertType(type)), field.Name);
					if (AddResolveResultAnnotations)
						mre.AddAnnotation(new MemberResolveResult(mre.Target.GetResolveResult(), field));
					return mre;
				}
			}
			if (IsFlagsEnum(enumDefinition)) {
				long enumValue = val;
				Expression expr = null;
				long negatedEnumValue = ~val;
				// limit negatedEnumValue to the appropriate range
				switch (enumBaseTypeCode) {
					case TypeCode.Byte:
					case TypeCode.SByte:
						negatedEnumValue &= byte.MaxValue;
						break;
					case TypeCode.Int16:
					case TypeCode.UInt16:
						negatedEnumValue &= ushort.MaxValue;
						break;
					case TypeCode.Int32:
					case TypeCode.UInt32:
						negatedEnumValue &= uint.MaxValue;
						break;
				}
				Expression negatedExpr = null;
				foreach (IField field in enumDefinition.Fields.Where(fld => fld.IsConst)) {
					long fieldValue = (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, field.ConstantValue, false);
					if (fieldValue == 0)
						continue;	// skip None enum value

					if ((fieldValue & enumValue) == fieldValue) {
						var fieldExpression = new MemberReferenceExpression(new TypeReferenceExpression(ConvertType(type)), field.Name);
						if (expr == null)
							expr = fieldExpression;
						else
							expr = new BinaryOperatorExpression(expr, BinaryOperatorType.BitwiseOr, fieldExpression);

						enumValue &= ~fieldValue;
					}
					if ((fieldValue & negatedEnumValue) == fieldValue) {
						var fieldExpression = new MemberReferenceExpression(new TypeReferenceExpression(ConvertType(type)), field.Name);
						if (negatedExpr == null)
							negatedExpr = fieldExpression;
						else
							negatedExpr = new BinaryOperatorExpression(negatedExpr, BinaryOperatorType.BitwiseOr, fieldExpression);

						negatedEnumValue &= ~fieldValue;
					}
				}
				if (enumValue == 0 && expr != null) {
					if (!(negatedEnumValue == 0 && negatedExpr != null && negatedExpr.Descendants.Count() < expr.Descendants.Count())) {
						return expr;
					}
				}
				if (negatedEnumValue == 0 && negatedExpr != null) {
					return new UnaryOperatorExpression(UnaryOperatorType.BitNot, negatedExpr);
				}
			}
			return new CastExpression(ConvertType(type), new PrimitiveExpression(CSharpPrimitiveCast.Cast(enumBaseTypeCode, val, false)));
		}

		#endregion

		#region Convert Parameter
		public ParameterDeclaration ConvertParameter(IParameter parameter)
		{
			if (parameter == null)
				throw new ArgumentNullException("parameter");
			ParameterDeclaration decl = new ParameterDeclaration();
			if (parameter.IsRef) {
				decl.ParameterModifier = ParameterModifier.Ref;
			} else if (parameter.IsOut) {
				decl.ParameterModifier = ParameterModifier.Out;
			} else if (parameter.IsIn) {
				decl.ParameterModifier = ParameterModifier.In;
			} else if (parameter.IsParams) {
				decl.ParameterModifier = ParameterModifier.Params;
			}
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(parameter.GetAttributes()));
			}
			if (parameter.Type.Kind == TypeKind.ByReference) {
				// avoid 'out ref'
				decl.Type = ConvertType(((ByReferenceType)parameter.Type).ElementType);
			} else {
				decl.Type = ConvertType(parameter.Type);
			}
			if (this.ShowParameterNames) {
				decl.Name = parameter.Name;
			}
			if (parameter.IsOptional && parameter.HasConstantValueInSignature && this.ShowConstantValues) {
				decl.DefaultExpression = ConvertConstantValue(parameter.Type, parameter.ConstantValue);
			}
			return decl;
		}
		#endregion

		#region Convert Entity
		public AstNode ConvertSymbol(ISymbol symbol)
		{
			if (symbol == null)
				throw new ArgumentNullException("symbol");
			switch (symbol.SymbolKind) {
				case SymbolKind.Namespace:
					return ConvertNamespaceDeclaration((INamespace)symbol);
				case SymbolKind.Variable:
					return ConvertVariable((IVariable)symbol);
				case SymbolKind.Parameter:
					return ConvertParameter((IParameter)symbol);
				case SymbolKind.TypeParameter:
					return ConvertTypeParameter((ITypeParameter)symbol);
				default:
					IEntity entity = symbol as IEntity;
					if (entity != null)
						return ConvertEntity(entity);
					throw new ArgumentException("Invalid value for SymbolKind: " + symbol.SymbolKind);
			}
		}

		public EntityDeclaration ConvertEntity(IEntity entity)
		{
			if (entity == null)
				throw new ArgumentNullException("entity");
			switch (entity.SymbolKind) {
				case SymbolKind.TypeDefinition:
					return ConvertTypeDefinition((ITypeDefinition)entity);
				case SymbolKind.Field:
					return ConvertField((IField)entity);
				case SymbolKind.Property:
					return ConvertProperty((IProperty)entity);
				case SymbolKind.Indexer:
					return ConvertIndexer((IProperty)entity);
				case SymbolKind.Event:
					return ConvertEvent((IEvent)entity);
				case SymbolKind.Method:
					return ConvertMethod((IMethod)entity);
				case SymbolKind.Operator:
					return ConvertOperator((IMethod)entity);
				case SymbolKind.Constructor:
					return ConvertConstructor((IMethod)entity);
				case SymbolKind.Destructor:
					return ConvertDestructor((IMethod)entity);
				case SymbolKind.Accessor:
					IMethod accessor = (IMethod)entity;
					return ConvertAccessor(accessor, accessor.AccessorOwner != null ? accessor.AccessorOwner.Accessibility : Accessibility.None, false);
				default:
					throw new ArgumentException("Invalid value for SymbolKind: " + entity.SymbolKind);
			}
		}

		EntityDeclaration ConvertTypeDefinition(ITypeDefinition typeDefinition)
		{
			Modifiers modifiers = Modifiers.None;
			if (this.ShowAccessibility) {
				modifiers |= ModifierFromAccessibility(typeDefinition.Accessibility);
			}
			if (this.ShowModifiers) {
				if (typeDefinition.IsStatic) {
					modifiers |= Modifiers.Static;
				} else if (typeDefinition.IsAbstract) {
					modifiers |= Modifiers.Abstract;
				} else if (typeDefinition.IsSealed) {
					modifiers |= Modifiers.Sealed;
				}
			}

			ClassType classType;
			switch (typeDefinition.Kind) {
				case TypeKind.Struct:
					classType = ClassType.Struct;
					modifiers &= ~Modifiers.Sealed;
					if (ShowModifiers) {
						if (typeDefinition.IsReadOnly) {
							modifiers |= Modifiers.Readonly;
						}
						if (typeDefinition.IsByRefLike) {
							modifiers |= Modifiers.Ref;
						}
					}
					break;
				case TypeKind.Enum:
					classType = ClassType.Enum;
					modifiers &= ~Modifiers.Sealed;
					break;
				case TypeKind.Interface:
					classType = ClassType.Interface;
					modifiers &= ~Modifiers.Abstract;
					break;
				case TypeKind.Delegate:
					IMethod invoke = typeDefinition.GetDelegateInvokeMethod();
					if (invoke != null) {
						return ConvertDelegate(invoke, modifiers);
					} else {
						goto default;
					}
				default:
					classType = ClassType.Class;
					break;
			}

			var decl = new TypeDeclaration();
			decl.ClassType = classType;
			decl.Modifiers = modifiers;
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(typeDefinition.GetAttributes()));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new TypeResolveResult(typeDefinition));
			}
			decl.Name = typeDefinition.Name;

			int outerTypeParameterCount = (typeDefinition.DeclaringTypeDefinition == null) ? 0 : typeDefinition.DeclaringTypeDefinition.TypeParameterCount;

			if (this.ShowTypeParameters) {
				foreach (ITypeParameter tp in typeDefinition.TypeParameters.Skip(outerTypeParameterCount)) {
					decl.TypeParameters.Add(ConvertTypeParameter(tp));
				}
			}

			if (this.ShowBaseTypes) {
				foreach (IType baseType in typeDefinition.DirectBaseTypes) {
					if (baseType.IsKnownType (KnownTypeCode.Enum)) {
						if (!typeDefinition.EnumUnderlyingType.IsKnownType (KnownTypeCode.Int32)) {
							decl.BaseTypes.Add (ConvertType (typeDefinition.EnumUnderlyingType));
						}
					} else if (!baseType.IsKnownType (KnownTypeCode.Object) &&
						 !baseType.IsKnownType (KnownTypeCode.ValueType)) {
						decl.BaseTypes.Add (ConvertType (baseType));
					}
				}
			}

			if (this.ShowTypeParameters && this.ShowTypeParameterConstraints) {
				foreach (ITypeParameter tp in typeDefinition.TypeParameters.Skip(outerTypeParameterCount)) {
					var constraint = ConvertTypeParameterConstraint(tp);
					if (constraint != null)
						decl.Constraints.Add(constraint);
				}
			}
			return decl;
		}

		DelegateDeclaration ConvertDelegate(IMethod invokeMethod, Modifiers modifiers)
		{
			ITypeDefinition d = invokeMethod.DeclaringTypeDefinition;

			DelegateDeclaration decl = new DelegateDeclaration();
			decl.Modifiers = modifiers & ~Modifiers.Sealed;
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(d.GetAttributes()));
				decl.Attributes.AddRange(ConvertAttributes(invokeMethod.GetReturnTypeAttributes(), "return"));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new TypeResolveResult(d));
			}
			decl.ReturnType = ConvertType(invokeMethod.ReturnType);
			decl.Name = d.Name;

			int outerTypeParameterCount = (d.DeclaringTypeDefinition == null) ? 0 : d.DeclaringTypeDefinition.TypeParameterCount;

			if (this.ShowTypeParameters) {
				foreach (ITypeParameter tp in d.TypeParameters.Skip(outerTypeParameterCount)) {
					decl.TypeParameters.Add(ConvertTypeParameter(tp));
				}
			}

			foreach (IParameter p in invokeMethod.Parameters) {
				decl.Parameters.Add(ConvertParameter(p));
			}

			if (this.ShowTypeParameters && this.ShowTypeParameterConstraints) {
				foreach (ITypeParameter tp in d.TypeParameters.Skip(outerTypeParameterCount)) {
					var constraint = ConvertTypeParameterConstraint(tp);
					if (constraint != null)
						decl.Constraints.Add(constraint);
				}
			}
			return decl;
		}

		FieldDeclaration ConvertField(IField field)
		{
			FieldDeclaration decl = new FieldDeclaration();
			if (ShowModifiers) {
				Modifiers m = GetMemberModifiers(field);
				if (field.IsConst) {
					m &= ~Modifiers.Static;
					m |= Modifiers.Const;
				} else if (field.IsReadOnly) {
					m |= Modifiers.Readonly;
				} else if (field.IsVolatile) {
					m |= Modifiers.Volatile;
				}
				decl.Modifiers = m;
			}
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(field.GetAttributes()));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, field));
			}
			decl.ReturnType = ConvertType(field.ReturnType);
			Expression initializer = null;
			if (field.IsConst && this.ShowConstantValues)
				initializer = ConvertConstantValue(field.Type, field.ConstantValue);
			decl.Variables.Add(new VariableInitializer(field.Name, initializer));
			return decl;
		}

		BlockStatement GenerateBodyBlock()
		{
			if (GenerateBody) {
				return new BlockStatement {
					new ThrowStatement(new ObjectCreateExpression(ConvertType(new TopLevelTypeName("System", "NotImplementedException", 0))))
				};
			} else {
				return BlockStatement.Null;
			}
		}

		Accessor ConvertAccessor(IMethod accessor, Accessibility ownerAccessibility, bool addParameterAttribute)
		{
			if (accessor == null)
				return Accessor.Null;
			Accessor decl = new Accessor();
			if (this.ShowAccessibility && accessor.Accessibility != ownerAccessibility)
				decl.Modifiers = ModifierFromAccessibility(accessor.Accessibility);
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(accessor.GetAttributes()));
				decl.Attributes.AddRange(ConvertAttributes(accessor.GetReturnTypeAttributes(), "return"));
				if (addParameterAttribute && accessor.Parameters.Count > 0) {
					decl.Attributes.AddRange(ConvertAttributes(accessor.Parameters.Last().GetAttributes(), "param"));
				}
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, accessor));
			}
			decl.Body = GenerateBodyBlock();
			return decl;
		}

		PropertyDeclaration ConvertProperty(IProperty property)
		{
			PropertyDeclaration decl = new PropertyDeclaration();
			decl.Modifiers = GetMemberModifiers(property);
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(property.GetAttributes()));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, property));
			}
			decl.ReturnType = ConvertType(property.ReturnType);
			decl.Name = property.Name;
			decl.Getter = ConvertAccessor(property.Getter, property.Accessibility, false);
			decl.Setter = ConvertAccessor(property.Setter, property.Accessibility, true);
			decl.PrivateImplementationType = GetExplicitInterfaceType (property);
			return decl;
		}

		IndexerDeclaration ConvertIndexer(IProperty indexer)
		{
			IndexerDeclaration decl = new IndexerDeclaration();
			decl.Modifiers = GetMemberModifiers(indexer);
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(indexer.GetAttributes()));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, indexer));
			}
			decl.ReturnType = ConvertType(indexer.ReturnType);
			foreach (IParameter p in indexer.Parameters) {
				decl.Parameters.Add(ConvertParameter(p));
			}
			decl.Getter = ConvertAccessor(indexer.Getter, indexer.Accessibility, false);
			decl.Setter = ConvertAccessor(indexer.Setter, indexer.Accessibility, true);
			decl.PrivateImplementationType = GetExplicitInterfaceType (indexer);
			return decl;
		}

		EntityDeclaration ConvertEvent(IEvent ev)
		{
			if (this.UseCustomEvents) {
				CustomEventDeclaration decl = new CustomEventDeclaration();
				decl.Modifiers = GetMemberModifiers(ev);
				if (ShowAttributes) {
					decl.Attributes.AddRange(ConvertAttributes(ev.GetAttributes()));
				}
				if (AddResolveResultAnnotations) {
					decl.AddAnnotation(new MemberResolveResult(null, ev));
				}
				decl.ReturnType = ConvertType(ev.ReturnType);
				decl.Name = ev.Name;
				decl.AddAccessor    = ConvertAccessor(ev.AddAccessor, ev.Accessibility, true);
				decl.RemoveAccessor = ConvertAccessor(ev.RemoveAccessor, ev.Accessibility, true);
				decl.PrivateImplementationType = GetExplicitInterfaceType (ev);
				return decl;
			} else {
				EventDeclaration decl = new EventDeclaration();
				decl.Modifiers = GetMemberModifiers(ev);
				if (ShowAttributes) {
					decl.Attributes.AddRange(ConvertAttributes(ev.GetAttributes()));
				}
				if (AddResolveResultAnnotations) {
					decl.AddAnnotation(new MemberResolveResult(null, ev));
				}
				decl.ReturnType = ConvertType(ev.ReturnType);
				decl.Variables.Add(new VariableInitializer(ev.Name));
				return decl;
			}
		}

		MethodDeclaration ConvertMethod(IMethod method)
		{
			MethodDeclaration decl = new MethodDeclaration();
			decl.Modifiers = GetMemberModifiers(method);
			if (ShowAttributes) {
				decl.Attributes.AddRange(ConvertAttributes(method.GetAttributes()));
				decl.Attributes.AddRange(ConvertAttributes(method.GetReturnTypeAttributes(), "return"));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, method));
			}
			decl.ReturnType = ConvertType(method.ReturnType);
			decl.Name = method.Name;

			if (this.ShowTypeParameters) {
				foreach (ITypeParameter tp in method.TypeParameters) {
					decl.TypeParameters.Add(ConvertTypeParameter(tp));
				}
			}

			foreach (IParameter p in method.Parameters) {
				decl.Parameters.Add(ConvertParameter(p));
			}
			if (method.IsExtensionMethod && method.ReducedFrom == null && decl.Parameters.Any() && decl.Parameters.First().ParameterModifier == ParameterModifier.None)
				decl.Parameters.First().ParameterModifier = ParameterModifier.This;

			if (this.ShowTypeParameters && this.ShowTypeParameterConstraints && !method.IsOverride && !method.IsExplicitInterfaceImplementation) {
				foreach (ITypeParameter tp in method.TypeParameters) {
					var constraint = ConvertTypeParameterConstraint(tp);
					if (constraint != null)
						decl.Constraints.Add(constraint);
				}
			}
			decl.Body = GenerateBodyBlock();
			decl.PrivateImplementationType = GetExplicitInterfaceType (method);
			return decl;
		}

		EntityDeclaration ConvertOperator(IMethod op)
		{
			OperatorType? opType = OperatorDeclaration.GetOperatorType(op.Name);
			if (opType == null)
				return ConvertMethod(op);

			OperatorDeclaration decl = new OperatorDeclaration();
			decl.Modifiers = GetMemberModifiers(op);
			decl.OperatorType = opType.Value;
			decl.ReturnType = ConvertType(op.ReturnType);
			foreach (IParameter p in op.Parameters) {
				decl.Parameters.Add(ConvertParameter(p));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, op));
			}
			decl.Body = GenerateBodyBlock();
			return decl;
		}

		ConstructorDeclaration ConvertConstructor(IMethod ctor)
		{
			ConstructorDeclaration decl = new ConstructorDeclaration();
			decl.Modifiers = GetMemberModifiers(ctor);
			if (ShowAttributes)
				decl.Attributes.AddRange(ConvertAttributes(ctor.GetAttributes()));
			if (ctor.DeclaringTypeDefinition != null)
				decl.Name = ctor.DeclaringTypeDefinition.Name;
			foreach (IParameter p in ctor.Parameters) {
				decl.Parameters.Add(ConvertParameter(p));
			}
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, ctor));
			}
			decl.Body = GenerateBodyBlock();
			return decl;
		}

		DestructorDeclaration ConvertDestructor(IMethod dtor)
		{
			DestructorDeclaration decl = new DestructorDeclaration();
			if (dtor.DeclaringTypeDefinition != null)
				decl.Name = dtor.DeclaringTypeDefinition.Name;
			if (AddResolveResultAnnotations) {
				decl.AddAnnotation(new MemberResolveResult(null, dtor));
			}
			decl.Body = GenerateBodyBlock();
			return decl;
		}
		#endregion

		#region Convert Modifiers
		public static Modifiers ModifierFromAccessibility(Accessibility accessibility)
		{
			switch (accessibility) {
				case Accessibility.Private:
					return Modifiers.Private;
				case Accessibility.Public:
					return Modifiers.Public;
				case Accessibility.Protected:
					return Modifiers.Protected;
				case Accessibility.Internal:
					return Modifiers.Internal;
				case Accessibility.ProtectedOrInternal:
					return Modifiers.Protected | Modifiers.Internal;
				case Accessibility.ProtectedAndInternal:
					return Modifiers.Private | Modifiers.Protected;
				default:
					return Modifiers.None;
			}
		}

		bool NeedsAccessibility(IMember member)
		{
			var declaringType = member.DeclaringType;
			if ((declaringType != null && declaringType.Kind == TypeKind.Interface) || member.IsExplicitInterfaceImplementation)
				return false;
			switch (member.SymbolKind) {
				case SymbolKind.Constructor:
					return !member.IsStatic;
				case SymbolKind.Destructor:
					return false;
				default:
					return true;
			}
		}

		Modifiers GetMemberModifiers(IMember member)
		{
			Modifiers m = Modifiers.None;
			if (this.ShowAccessibility && NeedsAccessibility(member)) {
				m |= ModifierFromAccessibility (member.Accessibility);
			}
			if (this.ShowModifiers) {
				if (member.IsStatic) {
					m |= Modifiers.Static;
				} else {
					var declaringType = member.DeclaringType;
					if (member.IsAbstract && declaringType != null && declaringType.Kind != TypeKind.Interface)
						m |= Modifiers.Abstract;
					if (member.IsOverride)
						m |= Modifiers.Override;
					if (member.IsVirtual && !member.IsAbstract && !member.IsOverride && declaringType.Kind != TypeKind.Interface)
						m |= Modifiers.Virtual;
					if (member.IsSealed)
						m |= Modifiers.Sealed;
				}
			}
			return m;
		}
		#endregion

		#region Convert Type Parameter
		TypeParameterDeclaration ConvertTypeParameter(ITypeParameter tp)
		{
			TypeParameterDeclaration decl = new TypeParameterDeclaration();
			decl.Variance = tp.Variance;
			decl.Name = tp.Name;
			if (ShowAttributes)
				decl.Attributes.AddRange(ConvertAttributes(tp.GetAttributes()));
			return decl;
		}

		Constraint ConvertTypeParameterConstraint(ITypeParameter tp)
		{
			if (!tp.HasDefaultConstructorConstraint && !tp.HasReferenceTypeConstraint && !tp.HasValueTypeConstraint && tp.DirectBaseTypes.All(IsObjectOrValueType)) {
				return null;
			}
			Constraint c = new Constraint();
			c.TypeParameter = new SimpleType (tp.Name);
			if (tp.HasReferenceTypeConstraint) {
				c.BaseTypes.Add(new PrimitiveType("class"));
			} else if (tp.HasValueTypeConstraint) {
				c.BaseTypes.Add(new PrimitiveType("struct"));
			}
			foreach (IType t in tp.DirectBaseTypes) {
				if (!IsObjectOrValueType(t))
					c.BaseTypes.Add(ConvertType(t));
			}
			if (tp.HasDefaultConstructorConstraint && !tp.HasValueTypeConstraint) {
				c.BaseTypes.Add(new PrimitiveType("new"));
			}
			return c;
		}

		static bool IsObjectOrValueType(IType type)
		{
			ITypeDefinition d = type.GetDefinition();
			return d != null && (d.KnownTypeCode == KnownTypeCode.Object || d.KnownTypeCode == KnownTypeCode.ValueType);
		}
		#endregion

		#region Convert Variable
		public VariableDeclarationStatement ConvertVariable(IVariable v)
		{
			VariableDeclarationStatement decl = new VariableDeclarationStatement();
			decl.Modifiers = v.IsConst ? Modifiers.Const : Modifiers.None;
			decl.Type = ConvertType(v.Type);
			Expression initializer = null;
			if (v.IsConst)
				initializer = ConvertConstantValue(v.Type, v.ConstantValue);
			decl.Variables.Add(new VariableInitializer(v.Name, initializer));
			return decl;
		}
		#endregion

		NamespaceDeclaration ConvertNamespaceDeclaration(INamespace ns)
		{
			return new NamespaceDeclaration(ns.FullName);
		}

		AstType GetExplicitInterfaceType (IMember member)
		{
			if (member.IsExplicitInterfaceImplementation) {
				var baseMember = member.ExplicitlyImplementedInterfaceMembers.FirstOrDefault ();
				if (baseMember != null)
					return ConvertType (baseMember.DeclaringType);
			}
			return null;
		}
	}
}
