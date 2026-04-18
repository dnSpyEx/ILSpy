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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	/// <summary>
	/// Custom attribute loaded from metadata.
	/// </summary>
	sealed class MetadataCustomAttribute : IAttribute
	{
		readonly MetadataModule module;
		readonly dnlib.DotNet.CustomAttribute handle;
		public IMethod Constructor { get; }

		// lazy-loaded:
		CustomAttributeValue<IType> value;
		bool valueDecoded;

		internal MetadataCustomAttribute(MetadataModule module, IMethod attrCtor, dnlib.DotNet.CustomAttribute handle)
		{
			Debug.Assert(module != null);
			Debug.Assert(attrCtor != null);
			Debug.Assert(handle != null);
			this.module = module;
			this.Constructor = attrCtor;
			this.handle = handle;
		}

		public IType AttributeType => Constructor.DeclaringType;

		public ImmutableArray<CustomAttributeTypedArgument<IType>> FixedArguments {
			get {
				DecodeValue();
				return value.FixedArguments;
			}
		}

		public ImmutableArray<CustomAttributeNamedArgument<IType>> NamedArguments {
			get {
				DecodeValue();
				return value.NamedArguments;
			}
		}

		public ICustomAttribute MetadataObject => handle;

		public bool HasDecodeErrors {
			get {
				return handle.IsRawBlob;
			}
		}

		void DecodeValue()
		{
			lock (this) {
				if (!valueDecoded) {
					List<CustomAttributeTypedArgument<IType>> ctor = new List<CustomAttributeTypedArgument<IType>>();
					foreach (CAArgument caArgument in handle.ConstructorArguments) {
						ctor.Add(Convert(caArgument, new GenericContext()));
					}

					List<CustomAttributeNamedArgument<IType>> named = new List<CustomAttributeNamedArgument<IType>>();
					foreach (CANamedArgument namedArgument in handle.NamedArguments) {
						var converted = Convert(namedArgument.Argument, new GenericContext());
						named.Add(new CustomAttributeNamedArgument<IType>(namedArgument.Name.String,
							namedArgument.IsField
								? CustomAttributeNamedArgumentKind.Field
								: CustomAttributeNamedArgumentKind.Property,
							converted.Type, converted.Value));
					}
					value = new CustomAttributeValue<IType>(ctor.ToImmutableArray(), named.ToImmutableArray());
					valueDecoded = true;
				}
			}
		}

		CustomAttributeTypedArgument<IType> Convert(CAArgument argument, GenericContext genericContext)
		{
			var convertedType = argument.Type.DecodeSignature(module, genericContext);
			if (argument.Value is TypeSig ts) {
				return new CustomAttributeTypedArgument<IType>(convertedType,
					ts.DecodeSignature(module, genericContext));
			}
			if (argument.Value is IList<CAArgument> list) {
				List<CustomAttributeTypedArgument<IType>> converted = new List<CustomAttributeTypedArgument<IType>>();
				foreach (CAArgument caArgument in list) {
					converted.Add(Convert(caArgument, genericContext));
				}
				return new CustomAttributeTypedArgument<IType>(convertedType, converted.ToImmutableArray());
			}
			if (argument.Value is UTF8String utf8String) {
				return new CustomAttributeTypedArgument<IType>(convertedType, utf8String.String);
			}
			if (argument.Value is CAArgument caArgument2) {
				return Convert(caArgument2, genericContext);
			}
			return new CustomAttributeTypedArgument<IType>(convertedType, argument.Value);
		}

		internal static IMember MemberForNamedArgument(IType attributeType, CustomAttributeNamedArgument<IType> namedArgument)
		{
			switch (namedArgument.Kind) {
				case CustomAttributeNamedArgumentKind.Field:
					return attributeType.GetFields(f => f.Name == namedArgument.Name).LastOrDefault();
				case CustomAttributeNamedArgumentKind.Property:
					return attributeType.GetProperties(p => p.Name == namedArgument.Name).LastOrDefault();
				default:
					return null;
			}
		}
	}
}
