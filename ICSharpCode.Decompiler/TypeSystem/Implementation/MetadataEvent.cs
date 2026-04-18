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
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	sealed class MetadataEvent : IEvent
	{
		readonly MetadataModule module;
		readonly EventDef handle;
		readonly string name;

		// lazy-loaded:
		IType returnType;

		internal MetadataEvent(MetadataModule module, EventDef handle)
		{
			Debug.Assert(module != null);
			Debug.Assert(handle != null);
			this.module = module;
			this.handle = handle;

			name = handle.Name;
		}

		public override string ToString()
		{
			return $"{handle.MDToken.Raw:X8} {DeclaringType?.ReflectionName}.{Name}";
		}

		public dnlib.DotNet.IMemberDef MetadataToken => handle;
		public string Name => name;

		SymbolKind ISymbol.SymbolKind => SymbolKind.Event;

		public bool CanAdd => handle.AddMethod != null;
		public bool CanRemove => handle.RemoveMethod != null;
		public bool CanInvoke => handle.InvokeMethod != null;
		public IMethod AddAccessor => module.GetDefinition(handle.AddMethod);
		public IMethod RemoveAccessor => module.GetDefinition(handle.RemoveMethod);
		public IMethod InvokeAccessor => module.GetDefinition(handle.InvokeMethod);
		IMethod AnyAccessor => module.GetDefinition(handle.AddMethod ?? handle.RemoveMethod ?? handle.InvokeMethod);

		#region Signature (ReturnType + Parameters)
		public IType ReturnType {
			get {
				var returnType = LazyInit.VolatileRead(ref this.returnType);
				if (returnType != null)
					return returnType;
				var declaringTypeDef = DeclaringTypeDefinition;
				var context = new GenericContext(declaringTypeDef?.TypeParameters);
				var nullableContext = declaringTypeDef?.NullableContext ?? Nullability.Oblivious;
				// The event does not have explicit accessibility in metadata, so use its
				// containing type to determine whether nullability applies to this type.
				var typeOptions = module.OptionsForEntity(declaringTypeDef);
				returnType = module.ResolveType(handle.EventType, context, typeOptions, handle, nullableContext);
				return LazyInit.GetOrSet(ref this.returnType, returnType);
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
			if ((handle.Attributes & (EventAttributes.SpecialName | EventAttributes.RTSpecialName)) == EventAttributes.SpecialName)
			{
				b.Add(KnownAttribute.SpecialName);
			}

			b.Add(handle.CustomAttributes, SymbolKind.Event);
			return b.Build();
		}

		public bool HasAttribute(KnownAttribute attribute)
		{
			if (!attribute.IsCustomAttribute())
			{
				return GetAttributes().Any(attr => attr.AttributeType.IsKnownType(attribute));
			}
			var b = new AttributeListBuilder(module);
			return b.HasAttribute(handle.CustomAttributes, attribute, SymbolKind.Event);
		}

		public IAttribute GetAttribute(KnownAttribute attribute)
		{
			if (!attribute.IsCustomAttribute())
			{
				return GetAttributes().FirstOrDefault(attr => attr.AttributeType.IsKnownType(attribute));
			}
			var b = new AttributeListBuilder(module);
			return b.GetAttribute(handle.CustomAttributes, attribute, SymbolKind.Event);
		}
		#endregion

		public Accessibility Accessibility => AnyAccessor?.Accessibility ?? Accessibility.None;
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
			if (obj is MetadataEvent ev)
			{
				return handle == ev.handle && module.MetadataFile == ev.module.MetadataFile;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return 0x7937039a ^ module.MetadataFile.GetHashCode() ^ handle.GetHashCode();
		}

		bool IMember.Equals(IMember obj, TypeVisitor typeNormalization)
		{
			return Equals(obj);
		}

		public IMember Specialize(TypeParameterSubstitution substitution)
		{
			return SpecializedEvent.Create(this, substitution);
		}
	}
}
