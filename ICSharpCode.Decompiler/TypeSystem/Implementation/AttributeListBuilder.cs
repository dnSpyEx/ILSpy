// Copyright (c) 2018 Daniel Grunwald
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using ICSharpCode.Decompiler.Util;
using ICSharpCode.Decompiler.Semantics;
using System.Runtime.InteropServices;
using System.Linq;
using dnlib.DotNet;
using dnlib.IO;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	readonly struct AttributeListBuilder
	{
		readonly MetadataModule module;
		readonly List<IAttribute> attributes;

		public AttributeListBuilder(MetadataModule module)
		{
			Debug.Assert(module != null);
			this.module = module;
			this.attributes = new List<IAttribute>();
		}

		public AttributeListBuilder(MetadataModule module, int capacity)
		{
			Debug.Assert(module != null);
			this.module = module;
			this.attributes = new List<IAttribute>(capacity);
		}

		public void Add(IAttribute attr)
		{
			attributes.Add(attr);
		}

		/// <summary>
		/// Add a builtin attribute without any arguments.
		/// </summary>
		public void Add(KnownAttribute type)
		{
			// use the assemblies' cache for simple attributes
			Add(module.MakeAttribute(type));
		}

		/// <summary>
		/// Construct a builtin attribute with a single positional argument of known type.
		/// </summary>
		public void Add(KnownAttribute type, KnownTypeCode argType, object argValue)
		{
			Add(type, ImmutableArray.Create(new CustomAttributeTypedArgument<IType>(module.Compilation.FindType(argType), argValue)));
		}

		/// <summary>
		/// Construct a builtin attribute with a single positional argument of known type.
		/// </summary>
		public void Add(KnownAttribute type, TopLevelTypeName argType, object argValue)
		{
			Add(type, ImmutableArray.Create(new CustomAttributeTypedArgument<IType>(module.Compilation.FindType(argType), argValue)));
		}

		/// <summary>
		/// Construct a builtin attribute.
		/// </summary>
		public void Add(KnownAttribute type, ImmutableArray<CustomAttributeTypedArgument<IType>> fixedArguments)
		{
			Add(new DefaultAttribute(module.GetAttributeType(type), fixedArguments,
				ImmutableArray.Create<CustomAttributeNamedArgument<IType>>()));
		}

		#region MarshalAsAttribute (ConvertMarshalInfo)
		internal void AddMarshalInfo(MarshalType marshalInfo)
		{
			if (marshalInfo is null) return;
			Add(ConvertMarshalInfo(marshalInfo));
		}

		const string InteropServices = "System.Runtime.InteropServices";

		IAttribute ConvertMarshalInfo(MarshalType marshalInfo)
		{
			var b = new AttributeBuilder(module, KnownAttribute.MarshalAs);
			IType unmanagedTypeType = module.Compilation.FindType(new TopLevelTypeName(InteropServices, nameof(UnmanagedType)));

			b.AddFixedArg(unmanagedTypeType, (int)marshalInfo.NativeType);

			FixedArrayMarshalType fami = marshalInfo as FixedArrayMarshalType;
			if (fami != null) {
				b.AddNamedArg("SizeConst", KnownTypeCode.Int32, (int)fami.Size);
				if (fami.IsElementTypeValid)
					b.AddNamedArg("ArraySubType", unmanagedTypeType, (int)fami.ElementType);
			}
			SafeArrayMarshalType sami = marshalInfo as SafeArrayMarshalType;
			if (sami != null && sami.VariantType != VariantType.None) {
				var varEnumType = new TopLevelTypeName(InteropServices, nameof(VarEnum));
				b.AddNamedArg("SafeArraySubType", varEnumType, (int)sami.VariantType);
			}
			ArrayMarshalType ami = marshalInfo as ArrayMarshalType;
			if (ami != null) {
				if (ami.ElementType != NativeType.Max)
					b.AddNamedArg("ArraySubType", unmanagedTypeType, (int)ami.ElementType);
				if (ami.Size >= 0)
					b.AddNamedArg("SizeConst", KnownTypeCode.Int32, ami.Size);
				if (ami.Flags != 0 && ami.ParamNumber >= 0)
					b.AddNamedArg("SizeParamIndex", KnownTypeCode.Int16, (short)ami.ParamNumber);
			}
			CustomMarshalType cmi = marshalInfo as CustomMarshalType;
			if (cmi != null) {
				if (cmi.CustomMarshaler != null)
					b.AddNamedArg("MarshalType", KnownTypeCode.String, cmi.CustomMarshaler.FullName);

				if (!UTF8String.IsNullOrEmpty(cmi.Cookie))
					b.AddNamedArg("MarshalCookie", KnownTypeCode.String, cmi.Cookie.String);
			}
			FixedSysStringMarshalType fssmi = marshalInfo as FixedSysStringMarshalType;
			if (fssmi != null) {
				b.AddNamedArg("SizeConst", KnownTypeCode.Int32, fssmi.Size);
			}

			return b.Build();
		}
		#endregion

		#region Custom Attributes (ReadAttribute)
		public void Add(CustomAttributeCollection attributes, SymbolKind target)
		{
			foreach (var attribute in attributes)
			{
				// Attribute types shouldn't be open generic, so we don't need a generic context.
				var ctor = module.ResolveMethod(attribute.Constructor, new GenericContext());
				var type = ctor.DeclaringType;
				if (IgnoreAttribute(type, target)) {
					continue;
				}
				Add(new CustomAttribute(module, ctor, attribute));
			}
		}

		bool IgnoreAttribute(IType attributeType, SymbolKind target)
		{
			if (attributeType.DeclaringType != null || attributeType.TypeParameterCount != 0)
				return false;
			return IgnoreAttribute(new TopLevelTypeName(attributeType.Namespace, attributeType.Name), target);
		}

		internal bool IgnoreAttribute(TopLevelTypeName attributeType, SymbolKind target)
		{
			switch (attributeType.Namespace)
			{
				case "System.Runtime.CompilerServices":
					var options = module.TypeSystemOptions;
					switch (attributeType.Name) {
						case "DynamicAttribute":
							return (options & TypeSystemOptions.Dynamic) != 0;
						case "NativeIntegerAttribute":
							return (options & TypeSystemOptions.NativeIntegers) != 0;
						case "TupleElementNamesAttribute":
							return (options & TypeSystemOptions.Tuple) != 0;
						case "ExtensionAttribute":
							return (options & TypeSystemOptions.ExtensionMethods) != 0;
						case "DecimalConstantAttribute":
							return (options & TypeSystemOptions.DecimalConstants) != 0 && (target == SymbolKind.Field || target == SymbolKind.Parameter);
						case "IsReadOnlyAttribute":
							switch (target)
							{
								case SymbolKind.TypeDefinition:
								case SymbolKind.Parameter:
									return (options & TypeSystemOptions.ReadOnlyStructsAndParameters) != 0;
								case SymbolKind.Method:
								case SymbolKind.Accessor:
									return (options & TypeSystemOptions.ReadOnlyMethods) != 0;
								case SymbolKind.ReturnType:
								case SymbolKind.Property:
								case SymbolKind.Indexer:
								case SymbolKind.Field:
									return true;  // "ref readonly" is currently always active
								default:
									return false;
							}
						case "IsByRefLikeAttribute":
							return (options & TypeSystemOptions.RefStructs) != 0 && target == SymbolKind.TypeDefinition;
						case "IsUnmanagedAttribute":
							return (options & TypeSystemOptions.UnmanagedConstraints) != 0 && target == SymbolKind.TypeParameter;
						case "NullableAttribute":
							return (options & TypeSystemOptions.NullabilityAnnotations) != 0;
						case "NullableContextAttribute":
							return (options & TypeSystemOptions.NullabilityAnnotations) != 0
								&& (target == SymbolKind.TypeDefinition || IsMethodLike(target));
						case "ScopedRefAttribute":
							return (options & TypeSystemOptions.ScopedRef) != 0
								&& (target == SymbolKind.Parameter);
						case "RequiresLocationAttribute":
							return (options & TypeSystemOptions.RefReadOnlyParameters) != 0
								&& (target == SymbolKind.Parameter);
						case "ParamCollectionAttribute":
							return (options & TypeSystemOptions.ParamsCollections) != 0
								&& (target == SymbolKind.Parameter);
						default:
							return false;
					}
				case "System":
					return attributeType.Name == "ParamArrayAttribute" && target == SymbolKind.Parameter;
				default:
					return false;
			}
		}

		internal bool HasAttribute(CustomAttributeCollection customAttributes, KnownAttribute attribute, SymbolKind symbolKind)
		{
			Debug.Assert(attribute.IsCustomAttribute());
			foreach (var attr in customAttributes)
			{
				if (attr.IsKnownAttribute(attribute))
				{
					return !IgnoreAttribute(attribute.GetTypeName(), symbolKind);
				}
			}

			return false;
		}

		internal IAttribute GetAttribute(CustomAttributeCollection customAttributes, KnownAttribute attribute, SymbolKind symbolKind)
		{
			Debug.Assert(attribute.IsCustomAttribute());
			foreach (var attr in customAttributes)
			{
				if (attr.IsKnownAttribute(attribute)
				    && !IgnoreAttribute(attribute.GetTypeName(), symbolKind))
				{
					// Attribute types shouldn't be open generic, so we don't need a generic context.
					var ctor = module.ResolveMethod(attr.Constructor, new GenericContext());
					return new CustomAttribute(module, ctor, attr);
				}
			}

			return null;
		}

		static bool IsMethodLike(SymbolKind kind)
		{
			return kind switch {
				SymbolKind.Method => true,
				SymbolKind.Operator => true,
				SymbolKind.Constructor => true,
				SymbolKind.Destructor => true,
				SymbolKind.Accessor => true,
				_ => false
			};
		}
		#endregion

		#region Security Attributes
		public void AddSecurityAttributes(IList<DeclSecurity> securityDeclarations)
		{
			foreach (var secDecl in securityDeclarations) {
				if (secDecl is null)
					continue;
				AddSecurityAttributes(secDecl);
			}
		}

		public void AddSecurityAttributes(DeclSecurity secDecl)
		{
			var securityActionType = module.Compilation.FindType(new TopLevelTypeName("System.Security.Permissions", "SecurityAction"));
			var securityAction = new CustomAttributeTypedArgument<IType>(securityActionType, (int)secDecl.Action);

			if (secDecl.GetBlob()[0] == '.') {
				// binary attribute
				foreach (var secAttr in secDecl.SecurityAttributes) {
					Add(ReadBinarySecurityAttribute(secAttr, securityAction));
				}
			} else {
				// for backward compatibility with .NET 1.0: XML-encoded attribute
				Add(ReadXmlSecurityAttribute(secDecl, securityAction));
			}
		}

		private IAttribute ReadXmlSecurityAttribute(DeclSecurity secDecl, CustomAttributeTypedArgument<IType> securityAction)
		{
			string xml = secDecl.GetNet1xXmlString();
			var b = new AttributeBuilder(module, KnownAttribute.PermissionSet);
			b.AddFixedArg(securityAction);
			b.AddNamedArg("XML", KnownTypeCode.String, xml);
			return b.Build();
		}

		private IAttribute ReadBinarySecurityAttribute(SecurityAttribute secDecl, CustomAttributeTypedArgument<IType> securityAction)
		{
			var attributeType =secDecl.AttributeType.DecodeSignature(module, new GenericContext());

			List<CustomAttributeNamedArgument<IType>> named = new List<CustomAttributeNamedArgument<IType>>();
			foreach (CANamedArgument namedArgument in secDecl.NamedArguments) {
				var converted = Convert(namedArgument.Argument);
				named.Add(new CustomAttributeNamedArgument<IType>(namedArgument.Name.String,
					namedArgument.IsField
						? CustomAttributeNamedArgumentKind.Field
						: CustomAttributeNamedArgumentKind.Property,
					converted.Type, converted.Value));
			}

			return new DefaultAttribute(
					   attributeType,
					   fixedArguments: ImmutableArray.Create(securityAction),
					   namedArguments: named.ToImmutableArray());
		}

		CustomAttributeTypedArgument<IType> Convert(CAArgument argument)
		{
			var convertedType = argument.Type.DecodeSignature(module, new GenericContext());
			if (argument.Value is TypeSig ts) {
				return new CustomAttributeTypedArgument<IType>(convertedType,
					ts.DecodeSignature(module, new GenericContext()));
			}
			if (argument.Value is IList<CAArgument> list) {
				List<CustomAttributeTypedArgument<IType>> converted = new List<CustomAttributeTypedArgument<IType>>();
				foreach (CAArgument caArgument in list) {
					converted.Add(Convert(caArgument));
				}
				return new CustomAttributeTypedArgument<IType>(convertedType, converted.ToImmutableArray());
			}
			if (argument.Value is UTF8String utf8String) {
				return new CustomAttributeTypedArgument<IType>(convertedType, utf8String.String);
			}
			if (argument.Value is CAArgument) {
				throw new NotSupportedException();
			}
			return new CustomAttributeTypedArgument<IType>(convertedType, argument.Value);
		}

		#endregion

		public IAttribute[] Build()
		{
			if (attributes.Count == 0)
				return Empty<IAttribute>.Array;
			else
				return attributes.ToArray();
		}
	}

	struct AttributeBuilder
	{
		readonly ICompilation compilation;
		readonly IType attributeType;
		ImmutableArray<CustomAttributeTypedArgument<IType>>.Builder fixedArgs;
		ImmutableArray<CustomAttributeNamedArgument<IType>>.Builder namedArgs;

		public AttributeBuilder(MetadataModule module, KnownAttribute attributeType)
			: this(module, module.GetAttributeType(attributeType))
		{
		}

		public AttributeBuilder(MetadataModule module, IType attributeType)
		{
			this.compilation = module.Compilation;
			this.attributeType = attributeType;
			this.fixedArgs = ImmutableArray.CreateBuilder<CustomAttributeTypedArgument<IType>>();
			this.namedArgs = ImmutableArray.CreateBuilder<CustomAttributeNamedArgument<IType>>();
		}

		public void AddFixedArg(CustomAttributeTypedArgument<IType> arg)
		{
			fixedArgs.Add(arg);
		}

		public void AddFixedArg(KnownTypeCode type, object value)
		{
			AddFixedArg(compilation.FindType(type), value);
		}

		public void AddFixedArg(TopLevelTypeName type, object value)
		{
			AddFixedArg(compilation.FindType(type), value);
		}

		public void AddFixedArg(IType type, object value)
		{
			fixedArgs.Add(new CustomAttributeTypedArgument<IType>(type, value));
		}

		public void AddNamedArg(string name, KnownTypeCode type, object value)
		{
			AddNamedArg(name, compilation.FindType(type), value);
		}

		public void AddNamedArg(string name, TopLevelTypeName type, object value)
		{
			AddNamedArg(name, compilation.FindType(type), value);
		}

		public void AddNamedArg(string name, IType type, object value)
		{
			CustomAttributeNamedArgumentKind kind;
			if (attributeType.GetFields(f => f.Name == name, GetMemberOptions.ReturnMemberDefinitions).Any())
				kind = CustomAttributeNamedArgumentKind.Field;
			else
				kind = CustomAttributeNamedArgumentKind.Property;
			namedArgs.Add(new CustomAttributeNamedArgument<IType>(name, kind, type, value));
		}

		public IAttribute Build()
		{
			return new DefaultAttribute(attributeType, fixedArgs.ToImmutable(), namedArgs.ToImmutable());
		}
	}
}
