﻿// Copyright (c) 2018 Daniel Grunwald
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
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	sealed class MetadataProperty : IProperty
	{
		const Accessibility InvalidAccessibility = (Accessibility)0xff;

		readonly MetadataModule module;
		readonly PropertyDef propertyHandle;
		readonly IMethod getter;
		readonly IMethod setter;
		readonly string name;
		readonly SymbolKind symbolKind;

		// lazy-loaded:
		volatile Accessibility cachedAccessiblity = InvalidAccessibility;
		IParameter[] parameters;
		IType returnType;

		internal MetadataProperty(MetadataModule module, PropertyDef handle)
		{
			Debug.Assert(module != null);
			Debug.Assert(handle != null);
			this.module = module;
			this.propertyHandle = handle;

			getter = module.GetDefinition(handle.GetMethod);
			setter = module.GetDefinition(handle.SetMethod);
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
			return $"{propertyHandle.MDToken.Raw:X8} {DeclaringType?.ReflectionName}.{Name}";
		}

		public IMDTokenProvider MetadataToken => propertyHandle;
		public string Name => name;

		public bool CanGet => getter != null;
		public bool CanSet => setter != null;

		public IMethod Getter => getter;
		public IMethod Setter => setter;
		IMethod AnyAccessor => getter ?? setter;

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
				foreach (Parameter par in propertyHandle.GetParameters()) {
					if (par.IsNormalMethodParameter) {
						var deco = par.Type.DecodeSignature(module, gCtx);
						var parameterType = ApplyAttributeTypeVisitor.ApplyAttributesToType(
							deco, module.Compilation,
							par.ParamDef, module.metadata, module.TypeSystemOptions);
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
				var deocded = propertyHandle.PropertySig.RetType.DecodeSignature(module,
					new GenericContext(DeclaringType.TypeParameters));
				var ret = ApplyAttributeTypeVisitor.ApplyAttributesToType(deocded,
					module.Compilation, propertyHandle, module.metadata, module.TypeSystemOptions);
				return LazyInit.GetOrSet(ref this.returnType, ret);
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
			if (IsIndexer && Name != "Item" && !IsExplicitInterfaceImplementation) {
				b.Add(KnownAttribute.IndexerName, KnownTypeCode.String, Name);
			}
			b.Add(propertyHandle.CustomAttributes);
			return b.Build();
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
			if (IsOverride && (getter == null || setter == null)) {
				foreach (var baseMember in InheritanceHelper.GetBaseMembers(this, includeImplementedInterfaces: false)) {
					if (!baseMember.IsOverride)
						return baseMember.Accessibility;
				}
			}
			return MergePropertyAccessibility(
				this.Getter?.Accessibility ?? Accessibility.None,
				this.Setter?.Accessibility ?? Accessibility.None);
		}

		static internal Accessibility MergePropertyAccessibility(Accessibility left, Accessibility right)
		{
			if (left == Accessibility.Public || right == Accessibility.Public)
				return Accessibility.Public;

			if (left == Accessibility.ProtectedOrInternal || right == Accessibility.ProtectedOrInternal)
				return Accessibility.ProtectedOrInternal;

			if (left == Accessibility.Protected && right == Accessibility.Internal ||
				left == Accessibility.Internal && right == Accessibility.Protected)
				return Accessibility.ProtectedOrInternal;

			if (left == Accessibility.Protected || right == Accessibility.Protected)
				return Accessibility.Protected;

			if (left == Accessibility.Internal || right == Accessibility.Internal)
				return Accessibility.Internal;

			if (left == Accessibility.ProtectedAndInternal || right == Accessibility.ProtectedAndInternal)
				return Accessibility.ProtectedAndInternal;

			if (left == Accessibility.Private || right == Accessibility.Private)
				return Accessibility.Private;

			return left;
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
				return propertyHandle == p.propertyHandle && module.PEFile == p.module.PEFile;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return 0x32b6a76c ^ module.PEFile.GetHashCode() ^ propertyHandle.GetHashCode();
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
