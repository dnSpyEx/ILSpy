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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

using dnSpy.Contracts.Decompiler;

using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	sealed class MetadataProperty : IProperty
	{
		const Accessibility InvalidAccessibility = (Accessibility)0xff;

		readonly MetadataModule module;
		readonly PropertyDef handle;
		readonly string name;
		readonly SymbolKind symbolKind;

		// lazy-loaded:
		IMethod getter;
		IMethod setter;
		volatile Accessibility cachedAccessiblity = InvalidAccessibility;
		IParameter[] parameters;
		IType returnType;

		internal MetadataProperty(MetadataModule module, PropertyDef handle)
		{
			Debug.Assert(module != null);
			Debug.Assert(handle != null);
			this.module = module;
			this.handle = handle;

			name = handle.Name;
			// Maybe we should defer the calculation of symbolKind?
			if (DetermineIsIndexer(name)) {
				symbolKind = SymbolKind.Indexer;
			} else if (name.IndexOf('.') >= 0) {
				// explicit interface implementation
				var interfaceProp = this.ExplicitlyImplementedInterfaceMembers.FirstOrDefault() as IProperty;
				symbolKind = interfaceProp?.SymbolKind ?? SymbolKind.Property;
			} else {
				symbolKind = SymbolKind.Property;
			}
		}

		bool DetermineIsIndexer(string name)
		{
			if (name != (DeclaringTypeDefinition as MetadataTypeDefinition)?.DefaultMemberName)
				return false;
			return Parameters.Count > 0;
		}

		public override string ToString()
		{
			return $"{handle.MDToken.Raw:X8} {DeclaringType?.ReflectionName}.{Name}";
		}

		IMDTokenProvider IEntity.MetadataToken => handle;

		public IMDTokenProvider OriginalMember => handle;

		public PropertyDef MetadataToken => handle;
		public string Name => name;


		public bool CanGet => Getter != null;
		public bool CanSet => Setter != null;

		public IMethod Getter {
			get {
				var get = LazyInit.VolatileRead(ref this.getter);
				if (get != null)
					return get;
				get = module.GetDefinition(handle.GetMethod);
				return LazyInit.GetOrSet(ref this.getter, get);
			}
		}

		public IMethod Setter {
			get {
				var set = LazyInit.VolatileRead(ref this.setter);
				if (set != null)
					return set;
				set = module.GetDefinition(handle.SetMethod);
				return LazyInit.GetOrSet(ref this.setter, set);
			}
		}

		IMethod AnyAccessor => Getter ?? Setter;

		public bool IsIndexer => symbolKind == SymbolKind.Indexer;
		public SymbolKind SymbolKind => symbolKind;

		#region Signature (ReturnType + Parameters)
		public IReadOnlyList<IParameter> Parameters {
			get {
				var parameters = LazyInit.VolatileRead(ref this.parameters);
				if (parameters != null)
					return parameters;

				List<IParameter> param = new List<IParameter>();
				var gCtx = new GenericContext(DeclaringType.TypeParameters);
				var declTypeDef = this.DeclaringTypeDefinition;
				Nullability nullableContext;

				if (handle.GetMethod != null) {
					nullableContext = handle.GetMethod.CustomAttributes.GetNullableContext()
									  ?? declTypeDef?.NullableContext ?? Nullability.Oblivious;
				} else if (handle.SetMethod != null) {
					nullableContext = handle.SetMethod.CustomAttributes.GetNullableContext()
									  ?? declTypeDef?.NullableContext ?? Nullability.Oblivious;
				} else {
					nullableContext = declTypeDef?.NullableContext ?? Nullability.Oblivious;
				}

				// We call OptionsForEntity() for the declaring type, not the property itself,
				// because the property's accessibilty isn't stored in metadata but computed.
				// Otherwise we'd get infinite recursion, because computing the accessibility
				// requires decoding the signature for the GetBaseMembers() call.
				// Roslyn uses the same workaround (see the NullableTypeDecoder.TransformType
				// call in PEPropertySymbol).
				var typeOptions = module.OptionsForEntity(declTypeDef);

				foreach (Parameter par in handle.GetParameters()) {
					if (par.IsNormalMethodParameter) {
						var parameterType = module.ResolveType(par.Type, gCtx, typeOptions, par.ParamDef, nullableContext);
						param.Add(new MetadataParameter(module, this, parameterType, par));
					}
				}
				return LazyInit.GetOrSet(ref this.parameters, param.ToArray());
			}
		}

		public IType ReturnType {
			get {
				var returnType = LazyInit.VolatileRead(ref this.returnType);
				if (returnType != null)
					return returnType;

				var declTypeDef = this.DeclaringTypeDefinition;

				Nullability nullableContext;
				ParamDef retParam;
				if (handle.GetMethod != null) {
					retParam = handle.GetMethod.Parameters.ReturnParameter.ParamDef;
					nullableContext = handle.GetMethod.CustomAttributes.GetNullableContext()
									  ?? declTypeDef?.NullableContext ?? Nullability.Oblivious;
				} else if (handle.SetMethod != null) {
					retParam = handle.SetMethod.Parameters.ReturnParameter.ParamDef;
					nullableContext = handle.SetMethod.CustomAttributes.GetNullableContext()
									  ?? declTypeDef?.NullableContext ?? Nullability.Oblivious;
				} else {
					retParam = null;
					nullableContext = declTypeDef?.NullableContext ?? Nullability.Oblivious;
				}
				// We call OptionsForEntity() for the declaring type, not the property itself,
				// because the property's accessibilty isn't stored in metadata but computed.
				// Otherwise we'd get infinite recursion, because computing the accessibility
				// requires decoding the signature for the GetBaseMembers() call.
				// Roslyn uses the same workaround (see the NullableTypeDecoder.TransformType
				// call in PEPropertySymbol).
				var typeOptions = module.OptionsForEntity(declTypeDef);

				var decoded = handle.PropertySig.RetType.DecodeSignature(module, new GenericContext(DeclaringType.TypeParameters));
				var ret = ApplyAttributeTypeVisitor.ApplyAttributesToType(decoded,
					module.Compilation, retParam, typeOptions, nullableContext, additionalAttributes: handle);
				return LazyInit.GetOrSet(ref this.returnType, ret);
			}
		}

		public bool ReturnTypeIsRefReadOnly {
			get {
				return handle.CustomAttributes.HasKnownAttribute(KnownAttribute.IsReadOnly);
			}
		}

		#endregion

		public bool IsExplicitInterfaceImplementation => AnyAccessor?.IsExplicitInterfaceImplementation ?? false;
		public IEnumerable<IMember> ExplicitlyImplementedInterfaceMembers => GetInterfaceMembersFromAccessor(AnyAccessor);

		internal static IEnumerable<IMember> GetInterfaceMembersFromAccessor(IMethod method)
		{
			if (method == null)
				return EmptyList<IMember>.Instance;
			return method.ExplicitlyImplementedInterfaceMembers.Select(m => ((IMethod)m).AccessorOwner).Where(m => m != null);
		}

		public ITypeDefinition DeclaringTypeDefinition => AnyAccessor?.DeclaringTypeDefinition;
		public IType DeclaringType => AnyAccessor?.DeclaringType;
		IMember IMember.MemberDefinition => this;
		TypeParameterSubstitution IMember.Substitution => TypeParameterSubstitution.Identity;

		#region Attributes
		public IEnumerable<IAttribute> GetAttributes()
		{
			var b = new AttributeListBuilder(module);

			// SpecialName
			if ((handle.Attributes & (PropertyAttributes.SpecialName | PropertyAttributes.RTSpecialName)) == PropertyAttributes.SpecialName)
			{
				b.Add(KnownAttribute.SpecialName);
			}

			b.Add(handle.GetCustomAttributes(), symbolKind);
			return b.Build();
		}

		public bool HasAttribute(KnownAttribute attribute)
		{
			if (!attribute.IsCustomAttribute())
			{
				return GetAttributes().Any(attr => attr.AttributeType.IsKnownType(attribute));
			}
			var b = new AttributeListBuilder(module);
			return b.HasAttribute(handle.CustomAttributes, attribute, symbolKind);
		}

		public IAttribute GetAttribute(KnownAttribute attribute)
		{
			if (!attribute.IsCustomAttribute())
			{
				return GetAttributes().FirstOrDefault(attr => attr.AttributeType.IsKnownType(attribute));
			}
			var b = new AttributeListBuilder(module);
			return b.GetAttribute(handle.CustomAttributes, attribute, symbolKind);
		}
		#endregion

		#region Accessibility
		public Accessibility Accessibility {
			get {
				var acc = cachedAccessiblity;
				if (acc == InvalidAccessibility)
					return cachedAccessiblity = ComputeAccessibility();
				else
					return acc;
			}
		}

		Accessibility ComputeAccessibility()
		{
			if (IsOverride && (getter == null || setter == null))
			{
				// Overrides may override only one of the accessors, hence calculating the accessibility from
				// the declared accessors is not sufficient. We need to "copy" accessibility from the baseMember.
				foreach (var baseMember in InheritanceHelper.GetBaseMembers(this, includeImplementedInterfaces: false))
				{
					if (!baseMember.IsOverride)
					{
						// See https://github.com/icsharpcode/ILSpy/issues/2653
						// "protected internal" (ProtectedOrInternal) accessibility is "reduced"
						// to "protected" accessibility across assembly boundaries.
						if (baseMember.Accessibility == Accessibility.ProtectedOrInternal
							&& this.ParentModule?.MetadataFile != baseMember.ParentModule?.MetadataFile)
						{
							return Accessibility.Protected;
						}
						else
						{
							return baseMember.Accessibility;
						}
					}
				}
			}
			return AccessibilityExtensions.Union(
				this.Getter?.Accessibility ?? Accessibility.None,
				this.Setter?.Accessibility ?? Accessibility.None);
		}
		#endregion

		public bool IsStatic => AnyAccessor?.IsStatic ?? false;
		public bool IsAbstract => AnyAccessor?.IsAbstract ?? false;
		public bool IsSealed => AnyAccessor?.IsSealed ?? false;
		public bool IsVirtual => AnyAccessor?.IsVirtual ?? false;
		public bool IsOverride => AnyAccessor?.IsOverride ?? false;
		public bool IsOverridable => AnyAccessor?.IsOverridable ?? false;

		public IModule ParentModule => module;
		public ICompilation Compilation => module.Compilation;

		public string FullName => $"{DeclaringType?.FullName}.{Name}";
		public string ReflectionName => $"{DeclaringType?.ReflectionName}.{Name}";
		public string Namespace => DeclaringType?.Namespace ?? string.Empty;

		public override bool Equals(object obj)
		{
			if (obj is MetadataProperty p) {
				return handle == p.handle && module.MetadataFile == p.module.MetadataFile;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return 0x32b6a76c ^ module.MetadataFile.GetHashCode() ^ handle.GetHashCode();
		}

		bool IMember.Equals(IMember obj, TypeVisitor typeNormalization)
		{
			return Equals(obj);
		}

		public IMember Specialize(TypeParameterSubstitution substitution)
		{
			return SpecializedProperty.Create(this, substitution);
		}
	}
}
