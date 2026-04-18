using System;
using System.Collections.Generic;
using System.Linq;

using dnlib.DotNet;

using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	public sealed class FakeCorLibTypesModule : IModule
	{
		public ICompilation Compilation { get; }
		private readonly CorlibNamespace rootNamespace;
		private readonly Dictionary<ITypeDefOrRef, FakeCorlibTypeDefinition> fakeDefinitions;
		private readonly IAssembly corLibRef;

		public FakeCorLibTypesModule(ICompilation compilation, ModuleDef module)
		{
			this.Compilation = compilation;
			corLibRef = module.CorLibTypes.AssemblyRef;
			fakeDefinitions = BuildFakeTypes(module);
			this.rootNamespace = new CorlibNamespace(this, null, string.Empty, string.Empty);
		}

		private Dictionary<ITypeDefOrRef, FakeCorlibTypeDefinition> BuildFakeTypes(ModuleDef module)
		{
			var corLibTypeSigs = new List<CorLibTypeSig> {
				module.CorLibTypes.Void,
				module.CorLibTypes.Boolean,
				module.CorLibTypes.Char,
				module.CorLibTypes.SByte,
				module.CorLibTypes.Byte,
				module.CorLibTypes.Int16,
				module.CorLibTypes.UInt16,
				module.CorLibTypes.Int32,
				module.CorLibTypes.UInt32,
				module.CorLibTypes.Int64,
				module.CorLibTypes.UInt64,
				module.CorLibTypes.Single,
				module.CorLibTypes.Double,
				module.CorLibTypes.String,
				module.CorLibTypes.TypedReference,
				module.CorLibTypes.IntPtr,
				module.CorLibTypes.UIntPtr,
				module.CorLibTypes.Object
			};

			var dict = new Dictionary<ITypeDefOrRef, FakeCorlibTypeDefinition>(corLibTypeSigs.Count, TypeEqualityComparer.Instance);
			foreach (var sig in corLibTypeSigs) {
				var typeRef = sig.TypeDefOrRef;
				var knownType = KnownTypeReference.AllKnownTypes.FirstOrDefault(x => x.Namespace == typeRef.Namespace && x.Name == typeRef.Name);
				if (knownType is null)
					continue;
				dict[typeRef] = new FakeCorlibTypeDefinition(this, typeRef, knownType);
			}

			return dict;
		}

		public ITypeDefinition GetFakeCorLibType(ITypeDefOrRef typeDefOrRef)
		{
			if (fakeDefinitions.TryGetValue(typeDefOrRef, out var typeDef))
				return typeDef;
			var knownType = KnownTypeReference.AllKnownTypes.FirstOrDefault(x => x.Namespace == typeDefOrRef.Namespace && x.Name == typeDefOrRef.Name);
			if (knownType is not null)
				return fakeDefinitions[typeDefOrRef] = new FakeCorlibTypeDefinition(this, typeDefOrRef, knownType);
			return null;
		}

		bool IModule.IsMainModule => false;

		string IModule.AssemblyName => corLibRef.Name;
		Version IModule.AssemblyVersion => corLibRef.Version;
		string IModule.FullAssemblyName => corLibRef.FullName;
		string ISymbol.Name => corLibRef.Name;
		SymbolKind ISymbol.SymbolKind => SymbolKind.Module;

		Metadata.MetadataFile IModule.MetadataFile => null;
		INamespace IModule.RootNamespace => rootNamespace;

		public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions => fakeDefinitions.Values.Where(td => td != null);
		public IEnumerable<ITypeDefinition> TypeDefinitions => TopLevelTypeDefinitions;

		public ITypeDefinition GetTypeDefinition(TopLevelTypeName topLevelTypeName)
		{
			foreach (var typeDef in fakeDefinitions.Values)
			{
				if (typeDef.FullTypeName == topLevelTypeName)
					return typeDef;
			}
			return null;
		}

		IEnumerable<IAttribute> IModule.GetAssemblyAttributes() => EmptyList<IAttribute>.Instance;
		IEnumerable<IAttribute> IModule.GetModuleAttributes() => EmptyList<IAttribute>.Instance;

		bool IModule.InternalsVisibleTo(IModule module)
		{
			return module == this;
		}

		sealed class CorlibNamespace : INamespace {
			readonly FakeCorLibTypesModule corlib;

			public INamespace ParentNamespace { get; }
			public string FullName { get; }
			public string Name { get; }

			public CorlibNamespace(FakeCorLibTypesModule corlib, INamespace parentNamespace, string fullName, string name) {
				this.corlib = corlib;
				this.ParentNamespace = parentNamespace;
				this.FullName = fullName;
				this.Name = name;
			}

			string INamespace.ExternAlias => string.Empty;

			IEnumerable<INamespace> INamespace.ChildNamespaces => EmptyList<INamespace>.Instance;
			IEnumerable<ITypeDefinition> INamespace.Types => corlib.TopLevelTypeDefinitions.Where(td => td.Namespace == FullName);

			IEnumerable<IModule> INamespace.ContributingModules => new[] { corlib };

			SymbolKind ISymbol.SymbolKind => SymbolKind.Namespace;
			ICompilation ICompilationProvider.Compilation => corlib.Compilation;

			INamespace INamespace.GetChildNamespace(string name) => null;

			ITypeDefinition INamespace.GetTypeDefinition(string name, int typeParameterCount) => corlib.GetTypeDefinition(this.FullName, name, typeParameterCount);
		}

		sealed class FakeCorlibTypeDefinition : ITypeDefinition {
			private readonly FakeCorLibTypesModule corlib;
			private readonly KnownTypeCode typeCode;
			private readonly ITypeDefOrRef corLibTypeRef;
			private readonly KnownTypeReference ktr;

			public FakeCorlibTypeDefinition(FakeCorLibTypesModule corlib, ITypeDefOrRef corLibType, KnownTypeReference knownTypeReference) {
				this.corlib = corlib;
				corLibTypeRef = corLibType;
				ktr = knownTypeReference;
				typeCode = knownTypeReference.KnownTypeCode;
			}

			IReadOnlyList<ITypeDefinition> ITypeDefinition.NestedTypes => EmptyList<ITypeDefinition>.Instance;
			IReadOnlyList<IMember> ITypeDefinition.Members => EmptyList<IMember>.Instance;
			IEnumerable<IField> ITypeDefinition.Fields => EmptyList<IField>.Instance;
			IEnumerable<IMethod> ITypeDefinition.Methods => EmptyList<IMethod>.Instance;
			IEnumerable<IProperty> ITypeDefinition.Properties => EmptyList<IProperty>.Instance;
			IEnumerable<IEvent> ITypeDefinition.Events => EmptyList<IEvent>.Instance;
			KnownTypeCode ITypeDefinition.KnownTypeCode => typeCode;
			IType ITypeDefinition.EnumUnderlyingType => SpecialType.UnknownType;
			public FullTypeName FullTypeName => corLibTypeRef.GetFullTypeName();
			public string MetadataName => corLibTypeRef.Name;
			ITypeDefinition IEntity.DeclaringTypeDefinition => null;
			IType ITypeDefinition.DeclaringType => null;
			IType IType.DeclaringType => null;
			IType IEntity.DeclaringType => null;
			bool ITypeDefinition.HasExtensions => false;
			ExtensionInfo ITypeDefinition.ExtensionInfo => null;
			bool ITypeDefinition.IsReadOnly => false;
			TypeKind IType.Kind => ktr.typeKind;

			bool? IType.IsReferenceType {
				get {
					switch (ktr.typeKind) {
						case TypeKind.Class:
						case TypeKind.Interface:
							return true;
						case TypeKind.Struct:
						case TypeKind.Enum:
							return false;
						default:
							return null;
					}
				}
			}

			bool IType.IsByRefLike => false;
			Nullability IType.Nullability => Nullability.Oblivious;
			Nullability ITypeDefinition.NullableContext => Nullability.Oblivious;

			IType IType.ChangeNullability(Nullability nullability) {
				if (nullability == Nullability.Oblivious)
					return this;
				return new NullabilityAnnotatedType(this, nullability);
			}

			int IType.TypeParameterCount => 0;
			IReadOnlyList<ITypeParameter> IType.TypeParameters => EmptyList<ITypeParameter>.Instance;
			IReadOnlyList<IType> IType.TypeArguments => EmptyList<IType>.Instance;

			IEnumerable<IType> IType.DirectBaseTypes {
				get {
					var baseType = ktr.baseType;
					if (baseType != KnownTypeCode.None)
						return new[] { corlib.Compilation.FindType(baseType) };
					return EmptyList<IType>.Instance;
				}
			}

			IMDTokenProvider IEntity.OriginalMember => OriginalMember;
			public string Name => corLibTypeRef.Name;
			IModule IEntity.ParentModule => corlib;
			Accessibility IEntity.Accessibility => Accessibility.Public;
			bool IEntity.IsStatic => false;
			bool IEntity.IsAbstract => ktr.typeKind == TypeKind.Interface;
			bool IEntity.IsSealed => ktr.typeKind == TypeKind.Struct;
			SymbolKind ISymbol.SymbolKind => SymbolKind.TypeDefinition;
			ICompilation ICompilationProvider.Compilation => corlib.Compilation;
			string INamedElement.FullName {
				get => corLibTypeRef.FullName;
			}
			string INamedElement.ReflectionName => corLibTypeRef.ReflectionName;
			string INamedElement.Namespace => corLibTypeRef.Namespace;
			bool IEquatable<IType>.Equals(IType other) => this == other;
			IEnumerable<IMethod> IType.GetAccessors(Predicate<IMethod> filter, GetMemberOptions options) => EmptyList<IMethod>.Instance;
			public TypeDef MetadataToken => null;
			dnlib.DotNet.IType IType.MetadataToken => corLibTypeRef;
			IMDTokenProvider IEntity.MetadataToken => corLibTypeRef;
			public dnlib.DotNet.IType OriginalMember => corLibTypeRef;
			IEnumerable<IAttribute> IEntity.GetAttributes() => EmptyList<IAttribute>.Instance;
			bool IEntity.HasAttribute(KnownAttribute attribute) => false;
			IAttribute IEntity.GetAttribute(KnownAttribute attribute) => null;
			IEnumerable<IMethod> IType.GetConstructors(Predicate<IMethod> filter, GetMemberOptions options) => EmptyList<IMethod>.Instance;
			IEnumerable<IEvent> IType.GetEvents(Predicate<IEvent> filter, GetMemberOptions options) => EmptyList<IEvent>.Instance;
			IEnumerable<IField> IType.GetFields(Predicate<IField> filter, GetMemberOptions options) => EmptyList<IField>.Instance;
			IEnumerable<IMember> IType.GetMembers(Predicate<IMember> filter, GetMemberOptions options) => EmptyList<IMember>.Instance;
			IEnumerable<IMethod> IType.GetMethods(Predicate<IMethod> filter, GetMemberOptions options) => EmptyList<IMethod>.Instance;
			IEnumerable<IMethod> IType.GetMethods(IReadOnlyList<IType> typeArguments, Predicate<IMethod> filter, GetMemberOptions options) => EmptyList<IMethod>.Instance;
			IEnumerable<IType> IType.GetNestedTypes(Predicate<ITypeDefinition> filter, GetMemberOptions options) => EmptyList<IType>.Instance;
			IEnumerable<IType> IType.GetNestedTypes(IReadOnlyList<IType> typeArguments, Predicate<ITypeDefinition> filter, GetMemberOptions options) => EmptyList<IType>.Instance;
			IEnumerable<IProperty> IType.GetProperties(Predicate<IProperty> filter, GetMemberOptions options) => EmptyList<IProperty>.Instance;
			bool ITypeDefinition.IsRecord => false;
			ITypeDefinition IType.GetDefinition() => this;
			ITypeDefinitionOrUnknown IType.GetDefinitionOrUnknown() => this;
			TypeParameterSubstitution IType.GetSubstitution() => TypeParameterSubstitution.Identity;
			IType IType.AcceptVisitor(TypeVisitor visitor) => visitor.VisitTypeDefinition(this);
			IType IType.VisitChildren(TypeVisitor visitor) => this;
			public override string ToString() => $"[FakeCorlibType {typeCode}]";
		}
	}
}
