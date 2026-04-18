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
using dnlib.DotNet.MD;
using dnSpy.Contracts.Decompiler;

using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem
{
	/// <summary>
	/// Type system implementation for Metadata.PEFile.
	/// </summary>
	[DebuggerDisplay("<MetadataModule: {AssemblyName}>")]
	public class MetadataModule : IModule
	{
		public ICompilation Compilation { get; }
		internal readonly ModuleDef metadata;
		readonly TypeSystemOptions options;
		internal readonly Nullability NullableContext;
		MetadataNamespace rootNamespace;

		private readonly Dictionary<TypeDef, MetadataTypeDefinition> typeDefDict = new Dictionary<TypeDef, MetadataTypeDefinition>();
		private readonly Dictionary<FieldDef, MetadataField> fieldDefDict = new Dictionary<FieldDef, MetadataField>();
		private readonly Dictionary<MethodDef, MetadataMethod> methodDefDict = new Dictionary<MethodDef, MetadataMethod>();
		private readonly Dictionary<PropertyDef, MetadataProperty> propertyDefDict = new Dictionary<PropertyDef, MetadataProperty>();
		private readonly Dictionary<EventDef, MetadataEvent> eventDefDict = new Dictionary<EventDef, MetadataEvent>();
		internal readonly Dictionary<TypeRef, IType> typeRefDict = new Dictionary<TypeRef, IType>();

		internal MetadataModule(ICompilation compilation, MetadataFile peFile, TypeSystemOptions options)
		{
			this.Compilation = compilation;
			this.MetadataFile = peFile;
			this.metadata = peFile.Metadata;
			this.options = options;

			// assembly metadata
			if (metadata.Assembly != null) {
				var asmdef = metadata.Assembly;
				this.AssemblyName = asmdef.Name;
				this.FullAssemblyName = asmdef.FullName;
				AssemblyVersion = asmdef.Version;
			} else {
				this.AssemblyName = metadata.Name;
				this.FullAssemblyName = this.AssemblyName;
			}

			var customAttrs = metadata.CustomAttributes;
			this.NullableContext = customAttrs.GetNullableContext() ?? Nullability.Oblivious;
			this.minAccessibilityForNRT = FindMinimumAccessibilityForNRT(customAttrs);
		}

		public TypeSystemOptions TypeSystemOptions => options;

		#region IModule interface
		public MetadataFile MetadataFile { get; }

		public bool IsMainModule => this == Compilation.MainModule;

		public string AssemblyName { get; }
		public Version AssemblyVersion { get; }
		public string FullAssemblyName { get; }
		string ISymbol.Name => AssemblyName;
		SymbolKind ISymbol.SymbolKind => SymbolKind.Module;

		public INamespace RootNamespace {
			get {
				var rootNamespace = LazyInit.VolatileRead(ref this.rootNamespace);
				if (rootNamespace != null)
					return rootNamespace;
				rootNamespace = new MetadataNamespace(this, null, NamespaceDefinition.GetRootNamespace(metadata.Types));
				return LazyInit.GetOrSet(ref this.rootNamespace, rootNamespace);
			}
		}

		public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions {
			get {
				foreach (var tdHandle in metadata.Types) {
					yield return GetDefinition(tdHandle);
				}
			}
		}

		public ITypeDefinition GetTypeDefinition(TopLevelTypeName topLevelTypeName)
		{
			var typeDefHandle = metadata.Find(topLevelTypeName.ReflectionName, true);
			if (typeDefHandle == null)
			{
				var forwarderHandle = MetadataFile.GetTypeForwarder(topLevelTypeName);
				if (forwarderHandle != null)
				{
					return ResolveForwardedType(forwarderHandle).GetDefinition();
				}
			}

			return GetDefinition(typeDefHandle);
		}

		#endregion

		#region InternalsVisibleTo

		public bool InternalsVisibleTo(IModule module)
		{
			if (this == module)
				return true;
			foreach (string shortName in GetInternalsVisibleTo())
			{
				if (string.Equals(module.AssemblyName, shortName, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private string[] internalsVisibleTo;
		static readonly UTF8String systemRuntimeCompilerServicesString = new UTF8String("System.Runtime.CompilerServices");
		static readonly UTF8String internalsVisibleToAttributeString = new UTF8String("InternalsVisibleToAttribute");

		private string[] GetInternalsVisibleTo()
		{
			var result = LazyInit.VolatileRead(ref this.internalsVisibleTo);
			if (result != null) {
				return result;
			}

			if (metadata.Assembly != null) {
				var list = new List<string>();
				foreach (var attr in metadata.Assembly.CustomAttributes)
				{
					if (attr.AttributeType.Compare(systemRuntimeCompilerServicesString, internalsVisibleToAttributeString) &&
						attr.ConstructorArguments.Count == 1 && attr.ConstructorArguments[0].Value is UTF8String s)
					{
						list.Add(GetShortName(s));
					}
				}

				result = list.ToArray();
			}
			else
			{
				result = Empty<string>.Array;
			}

			return LazyInit.GetOrSet(ref this.internalsVisibleTo, result);
		}

		private static string GetShortName(string fullAssemblyName)
		{
			int pos = fullAssemblyName.IndexOf(',');
			return pos < 0 ? fullAssemblyName : fullAssemblyName.Substring(0, pos);
		}

		#endregion

		#region GetDefinition

		/// <summary>
		/// Gets all types in the assembly, including nested types.
		/// </summary>
		public IEnumerable<ITypeDefinition> TypeDefinitions {
			get {
				foreach (var tdHandle in metadata.GetTypes()) {
					yield return GetDefinition(tdHandle);
				}
			}
		}

		public ITypeDefinition GetDefinition(TypeDef handle)
		{
			return GetDefinitionInternal(handle);
		}

		internal MetadataTypeDefinition GetDefinitionInternal(TypeDef handle)
		{
			if (handle == null)
				return null;
			lock (typeDefDict) {
				if (typeDefDict.TryGetValue(handle, out var tsType))
					return tsType;
				return typeDefDict[handle] = new MetadataTypeDefinition(this, handle);
			}
		}

		public IField GetDefinition(FieldDef handle)
		{
			return GetDefinitionInternal(handle);
		}

		internal MetadataField GetDefinitionInternal(FieldDef handle)
		{
			if (handle == null)
				return null;
			lock (fieldDefDict) {
				if (fieldDefDict.TryGetValue(handle, out var tsField))
					return tsField;
				return fieldDefDict[handle] = new MetadataField(this, handle);
			}
		}

		public IMethod GetDefinition(MethodDef handle)
		{
			return GetDefinitionInternal(handle);
		}

		internal MetadataMethod GetDefinitionInternal(MethodDef handle)
		{
			if (handle == null)
				return null;
			lock (methodDefDict) {
				if (methodDefDict.TryGetValue(handle, out var tsMethod))
					return tsMethod;
				return methodDefDict[handle] = new MetadataMethod(this, handle);
			}
		}

		public IProperty GetDefinition(PropertyDef handle)
		{
			if (handle == null)
				return null;
			lock (propertyDefDict) {
				if (propertyDefDict.TryGetValue(handle, out var tsProperty))
					return tsProperty;
				return propertyDefDict[handle] = new MetadataProperty(this, handle);
			}
		}

		public IEvent GetDefinition(EventDef handle)
		{
			if (handle == null)
				return null;
			lock (eventDefDict) {
				if (eventDefDict.TryGetValue(handle, out var tsEvent))
					return tsEvent;
				return eventDefDict[handle] = new MetadataEvent(this, handle);
			}
		}

		#endregion

		#region Resolve Type

		public IType ResolveType(ITypeDefOrRef typeRefDefSpec, GenericContext context,
			IHasCustomAttribute typeAttributes = null, Nullability nullableContext = Nullability.Oblivious)
		{
			return ResolveType(typeRefDefSpec, context, options, typeAttributes, nullableContext);
		}

		public IType ResolveType(TypeSig typeRefDefSpec, GenericContext context,
			IHasCustomAttribute typeAttributes = null, Nullability nullableContext = Nullability.Oblivious)
		{
			return ResolveType(typeRefDefSpec, context, options, typeAttributes, nullableContext);
		}

		public IType ResolveType(ITypeDefOrRef typeDefOrRef, GenericContext context, TypeSystemOptions customOptions,
			IHasCustomAttribute typeAttributes = null, Nullability nullableContext = Nullability.Oblivious)
		{
			if (typeDefOrRef == null)
				return SpecialType.UnknownType;

			IType ty = typeDefOrRef.DecodeSignature(this, context);
			ty = ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, typeAttributes, customOptions,
				nullableContext);
			return ty;
		}

		public IType ResolveType(TypeSig typeSpec, GenericContext context, TypeSystemOptions customOptions,
			IHasCustomAttribute typeAttributes = null, Nullability nullableContext = Nullability.Oblivious)
		{
			if (typeSpec == null)
				return SpecialType.UnknownType;

			IType ty = typeSpec.DecodeSignature(this, context);
			ty = ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, typeAttributes, customOptions,
				nullableContext);
			return ty;
		}

		IType ResolveDeclaringType(ITypeDefOrRef declaringTypeReference, GenericContext context)
		{
			// resolve without substituting dynamic/tuple types
			const TypeSystemOptions removedOptions = TypeSystemOptions.Dynamic | TypeSystemOptions.Tuple
				| TypeSystemOptions.NullabilityAnnotations | TypeSystemOptions.NativeIntegers | TypeSystemOptions.NativeIntegersWithoutAttribute;
			var ty = ResolveType(declaringTypeReference, context, options & ~removedOptions);
			// but substitute tuple types in type arguments:
			ty = ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, null, options, Nullability.Oblivious,
				typeChildrenOnly: true);
			return ty;
		}

		private IType IntroduceTupleTypes(IType ty)
		{
			// run ApplyAttributeTypeVisitor without attributes, in order to introduce tuple types
			return ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, null, options,
				Nullability.Oblivious);
		}

		#endregion

		#region Resolve Method

		public IMethod ResolveMethod(dnlib.DotNet.IMethod methodReference, GenericContext context)
		{
			if (methodReference == null)
				throw new ArgumentNullException(nameof(methodReference));
			switch (methodReference.MDToken.Table) {
				case Table.Method:
					return ResolveMethodDefinition((MethodDef)methodReference, expandVarArgs: true);
				case Table.MemberRef:
					return ResolveMethodReference((MemberRef)methodReference, context, expandVarArgs: true);
				case Table.MethodSpec:
					return ResolveMethodSpecification((MethodSpec)methodReference, context, expandVarArgs: true);
				default:
					throw new BadImageFormatException("Metadata token must be either a methoddef, memberref or methodspec");
			}
		}

		IMethod ResolveMethodDefinition(MethodDef methodDefHandle, bool expandVarArgs, IMDTokenProvider originalMember = null)
		{
			IMethod method = originalMember is not null
				? GetDefinitionInternal(methodDefHandle).WithOriginalMember(originalMember)
				: GetDefinitionInternal(methodDefHandle);
			if (expandVarArgs && methodDefHandle.CallingConvention == CallingConvention.VarArg) {
				method = new VarArgInstanceMethod(method, EmptyList<IType>.Instance);
			}

			return method;
		}

		IMethod ResolveMethodSpecification(MethodSpec methodSpec, GenericContext context, bool expandVarArgs)
		{
			var methodTypeArgs = methodSpec.GenericInstMethodSig.GenericArguments
										   .Select(sig => sig.DecodeSignature(this, context))
										   .Select(IntroduceTupleTypes).ToArray();
			IMethod method;
			if (methodSpec.Method is MethodDef methodDef) {
				// generic instance of a methoddef (=generic method in non-generic class in current assembly)
				method = ResolveMethodDefinition(methodDef, expandVarArgs, methodSpec)
					.Specialize(new TypeParameterSubstitution(null, methodTypeArgs));
			} else {
				method = ResolveMethodReference((MemberRef)methodSpec.Method, context, methodTypeArgs, expandVarArgs, methodSpec);
			}

			return method;
		}

		/// <summary>
		/// Resolves a method reference.
		/// </summary>
		/// <remarks>
		/// Class type arguments are provided by the declaring type stored in the memberRef.
		/// Method type arguments are provided by the caller.
		/// </remarks>
		IMethod ResolveMethodReference(MemberRef memberRef, GenericContext context,
			IReadOnlyList<IType> methodTypeArguments = null, bool expandVarArgs = true, IMDTokenProvider originalMember = null)
		{
			IReadOnlyList<IType> classTypeArguments = null;
			IMethod method;
			GenericContext vaRAgCtx;
			if (memberRef.Class is MethodDef methodDef) {
				method = GetDefinitionInternal(methodDef).WithOriginalMember(originalMember ?? memberRef);
				vaRAgCtx = context;
			}
			else {
				var declaringType = ResolveDeclaringType(memberRef.DeclaringType, context);

				if (declaringType.TypeArguments.Count > 0) {
					classTypeArguments = declaringType.TypeArguments;
				}

				var declaringTypeDefinition = declaringType.GetDefinition();
				vaRAgCtx = new GenericContext(declaringTypeDefinition?.TypeParameters);

				var resolved = memberRef.ResolveMethod();
				if (resolved is not null && Compilation.GetOrAddModule(resolved.Module) is MetadataModule mod)
					method = mod.GetDefinitionInternal(resolved).WithOriginalMember(originalMember ?? memberRef);
				else
					method = CreateUnresolvedMethod(memberRef, declaringType, context);
			}

			if (classTypeArguments != null || methodTypeArguments != null) {
				method = method.Specialize(new TypeParameterSubstitution(classTypeArguments, methodTypeArguments));
			}

			if (expandVarArgs && memberRef.CallingConvention == CallingConvention.VarArg) {
				method = new VarArgInstanceMethod(method,
					memberRef.MethodSig.ParamsAfterSentinel is null
						? new List<IType>()
						: memberRef.MethodSig.ParamsAfterSentinel.Select(x => x.DecodeSignature(this, vaRAgCtx)));
			}

			return method;
		}

		/// <summary>
		/// Create a dummy IMethod from the specified MethodReference
		/// </summary>
		IMethod CreateUnresolvedMethod(MemberRef memberRef, IType declaringType, GenericContext context)
		{
			var symbolKind = memberRef.Name == ".ctor" || memberRef.Name == ".cctor"
				? SymbolKind.Constructor
				: SymbolKind.Method;
			var unresolved = new MetadataUnresolvedMethod(this, memberRef, symbolKind) {
				DeclaringType = declaringType,
				IsStatic = !memberRef.HasThis
			};

			TypeParameterSubstitution substitution = null;
			if (memberRef.MethodSig.GenParamCount > 0)
			{
				var typeParameters = new List<ITypeParameter>();
				for (int i = 0; i < memberRef.MethodSig.GenParamCount; i++)
				{
					typeParameters.Add(new DefaultTypeParameter(unresolved, i));
				}

				unresolved.TypeParameters = typeParameters;
				substitution = new TypeParameterSubstitution(declaringType.TypeArguments, typeParameters);
			}
			else if (declaringType.TypeArguments.Count > 0)
			{
				substitution = declaringType.GetSubstitution();
			}

			var parameters = new List<IParameter>();
			foreach (var t in memberRef.MethodSig.Params.Select(x => x.DecodeSignature(this, context)))
			{
				var type = t;
				if (substitution != null)
				{
					// replace the dummy method type parameters with the owned instances we just created
					type = type.AcceptVisitor(substitution);
				}

				parameters.Add(new DefaultParameter(type, ""));
			}

			unresolved.Parameters = parameters;

			if (memberRef.MethodSig.GenParamCount == 0)
				GuessFakeMethodAccessor(unresolved);

			return unresolved;
		}

		private void GuessFakeMethodAccessor(MetadataUnresolvedMethod m)
		{
			var guessedGetter = m.Name.StartsWith("get_", StringComparison.Ordinal);
			var guessedSetter = m.Name.StartsWith("set_", StringComparison.Ordinal);
			if (guessedGetter || guessedSetter)
			{
				var propertyName = m.Name.Substring(4);

				var fakeProperty = new FakeProperty(Compilation) {
					Name = propertyName,
					DeclaringType = m.DeclaringType,
					IsStatic = m.IsStatic,
				};

				if (guessedGetter)
				{
					if (m.ReturnType.Kind == TypeKind.Void)
						return;

					m.AccessorKind = MethodSemanticsAttributes.Getter;
					m.AccessorOwner = fakeProperty;
					fakeProperty.Getter = m;
					fakeProperty.ReturnType = m.ReturnType;
					fakeProperty.IsIndexer = m.Parameters.Count > 0;
					fakeProperty.Parameters = m.Parameters;
					return;
				}

				if (guessedSetter)
				{
					if (m.Parameters.Count < 1 || m.ReturnType.Kind != TypeKind.Void)
						return;

					m.AccessorKind = MethodSemanticsAttributes.Setter;
					m.AccessorOwner = fakeProperty;
					fakeProperty.Setter = m;
					fakeProperty.ReturnType = m.Parameters[m.Parameters.Count - 1].Type;
					fakeProperty.IsIndexer = m.Parameters.Count > 1;
					fakeProperty.Parameters = m.Parameters.SkipLast(1).ToArray();
					return;
				}
			}

			const string addPrefix = "add_";
			const string removePrefix = "remove_";
			const string raisePrefix = "raise_";
			var guessedAdd = m.Name.StartsWith(addPrefix, StringComparison.Ordinal);
			var guessedRemove = m.Name.StartsWith(removePrefix, StringComparison.Ordinal);
			var guessedRaise = m.Name.StartsWith(raisePrefix, StringComparison.Ordinal);
			if (guessedAdd || guessedRemove || guessedRaise)
			{
				var fakeEvent = new FakeEvent(Compilation) {
					DeclaringType = m.DeclaringType,
					IsStatic = m.IsStatic,
				};

				if (guessedAdd)
				{
					if (m.Parameters.Count != 1)
						return;

					m.AccessorKind = MethodSemanticsAttributes.AddOn;
					m.AccessorOwner = fakeEvent;

					fakeEvent.Name = m.Name.Substring(addPrefix.Length);
					fakeEvent.AddAccessor = m;
					fakeEvent.ReturnType = m.Parameters.Single().Type;

					return;
				}

				if (guessedRemove)
				{
					if (m.Parameters.Count != 1)
						return;

					m.AccessorKind = MethodSemanticsAttributes.RemoveOn;
					m.AccessorOwner = fakeEvent;

					fakeEvent.Name = m.Name.Substring(removePrefix.Length);
					fakeEvent.RemoveAccessor = m;
					fakeEvent.ReturnType = m.Parameters.Single().Type;

					return;
				}

				if (guessedRaise)
				{
					fakeEvent.Name = m.Name.Substring(raisePrefix.Length);
					fakeEvent.InvokeAccessor = m;
					m.AccessorKind = MethodSemanticsAttributes.Fire;
					m.AccessorOwner = fakeEvent;
					return;
				}
			}
		}
		#endregion

		#region Resolve Entity

		/// <summary>
		/// Resolves a symbol.
		/// </summary>
		/// <remarks>
		/// * Types are resolved to their definition, as IType does not implement ISymbol.
		///    * types without definition will resolve to <c>null</c>
		///    * use ResolveType() to properly resolve types
		/// * When resolving methods, varargs signatures are not expanded.
		///    * use ResolveMethod() instead to get an IMethod instance suitable for call-sites
		/// * May return specialized members, where generics are involved.
		/// * Other types of handles that don't correspond to TS entities, will return <c>null</c>.
		/// </remarks>
		public IEntity ResolveEntity(IMDTokenProvider entityHandle, GenericContext context = default)
		{
			if (entityHandle is TypeDef typeDef)
				return GetDefinition(typeDef);
			if (entityHandle is MethodDef methodDef)
				return GetDefinition(methodDef);
			if (entityHandle is FieldDef fieldDef)
				return GetDefinition(fieldDef);
			if (entityHandle is EventDef eventDef)
				return GetDefinition(eventDef);
			if (entityHandle is PropertyDef propertyDef)
				return GetDefinition(propertyDef);
			if (entityHandle is ITypeDefOrRef typeDefOrRef)
				return ResolveDeclaringType(typeDefOrRef, context).GetDefinition();
			if (entityHandle is MemberRef memberRef) {
				if (memberRef.IsMethodRef) {
					// for consistency with the MethodDefinition case, never expand varargs
					return ResolveMethodReference(memberRef, context, expandVarArgs: false);
				}
				if (memberRef.IsFieldRef)
					return ResolveFieldReference(memberRef, context);
			}
			if (entityHandle is MethodSpec methodSpec)
				return ResolveMethodSpecification(methodSpec, context, expandVarArgs: false);
			return null;
		}

		IField ResolveFieldReference(MemberRef memberRef, GenericContext context)
		{
			var declaringType = ResolveDeclaringType(memberRef.DeclaringType, context);

			IField tsField;
			var resolved = memberRef.ResolveField();
			if (resolved is not null && Compilation.GetOrAddModule(resolved.Module) is MetadataModule mod)
				tsField = mod.GetDefinitionInternal(resolved).WithOriginalMember(memberRef);
			else
			{
				tsField = new MetadataUnresolvedField(this, memberRef) {
					DeclaringType = declaringType
				};
			}

			if (declaringType.TypeArguments.Count > 0)
				tsField = tsField.Specialize(declaringType.GetSubstitution());
			return tsField;
		}

		#endregion

		#region Decode Standalone Signature

		public FunctionPointerType DecodeMethodSignature(MethodSig standaloneSignature, GenericContext genericContext)
		{
			if (standaloneSignature is null)
				throw new BadImageFormatException("Expected Method signature");
			var retType = standaloneSignature.RetType.DecodeSignature(this, genericContext);
			var paramTypes = standaloneSignature.Params.Select(t => t.DecodeSignature(this, genericContext)).ToList();
			var fpt = FunctionPointerType.FromSignature(retType, paramTypes, standaloneSignature.CallingConvention, this);
			return (FunctionPointerType)IntroduceTupleTypes(fpt);
		}

		#endregion

		#region Module / Assembly attributes

		/// <summary>
		/// Gets the list of all assembly attributes in the project.
		/// </summary>
		public IEnumerable<IAttribute> GetAssemblyAttributes()
		{
			var b = new AttributeListBuilder(this);
			if (metadata.Assembly != null) {
				b.Add(metadata.Assembly.GetCustomAttributes(), SymbolKind.Module);
			}

			return b.Build();
		}

		/// <summary>
		/// Gets the list of all module attributes in the project.
		/// </summary>
		public IEnumerable<IAttribute> GetModuleAttributes()
		{
			var b = new AttributeListBuilder(this);
			b.Add(metadata.GetCustomAttributes(), SymbolKind.Module);
			if (metadata.Assembly == null) {
				AddTypeForwarderAttributes(ref b);
			}

			return b.Build();
		}

		void AddTypeForwarderAttributes(ref AttributeListBuilder b)
		{
			foreach (ExportedType type in metadata.ExportedTypes) {
				if (type.MovedToAnotherAssembly) {
					b.Add(KnownAttribute.TypeForwardedTo, KnownTypeCode.Type, ResolveForwardedType(type));
				}
			}
		}

		IType ResolveForwardedType(ExportedType forwarder)
		{
			var resolved = forwarder.Resolve();
			if (resolved != null && Compilation.GetOrAddModule(resolved.Module) is MetadataModule mod) {
				return mod.GetDefinition(resolved);
			}
			return new UnknownType(forwarder.GetFullTypeName());
		}

		#endregion

		#region Attribute Helpers

		/// <summary>
		/// Cache for parameterless known attribute types.
		/// </summary>
		readonly IType[] knownAttributeTypes = new IType[KnownAttributes.Count];

		internal IType GetAttributeType(KnownAttribute attr)
		{
			var ty = LazyInit.VolatileRead(ref knownAttributeTypes[(int)attr]);
			if (ty != null)
				return ty;
			ty = Compilation.FindType(attr.GetTypeName());
			return LazyInit.GetOrSet(ref knownAttributeTypes[(int)attr], ty);
		}

		/// <summary>
		/// Cache for parameterless known attributes.
		/// </summary>
		readonly IAttribute[] knownAttributes = new IAttribute[KnownAttributes.Count];

		/// <summary>
		/// Construct a builtin attribute.
		/// </summary>
		internal IAttribute MakeAttribute(KnownAttribute type)
		{
			var attr = LazyInit.VolatileRead(ref knownAttributes[(int)type]);
			if (attr != null)
				return attr;
			attr = new DefaultAttribute(GetAttributeType(type),
				ImmutableArray.Create<CustomAttributeTypedArgument<IType>>(),
				ImmutableArray.Create<CustomAttributeNamedArgument<IType>>());
			return LazyInit.GetOrSet(ref knownAttributes[(int)type], attr);
		}

		#endregion

		#region Visibility Filter

		internal bool IncludeInternalMembers => (options & TypeSystemOptions.OnlyPublicAPI) == 0;

		internal bool IsVisible(FieldAttributes att)
		{
			att &= FieldAttributes.FieldAccessMask;
			return IncludeInternalMembers
				   || att == FieldAttributes.Public
				   || att == FieldAttributes.Family
				   || att == FieldAttributes.FamORAssem;
		}

		internal bool IsVisible(MethodAttributes att)
		{
			att &= MethodAttributes.MemberAccessMask;
			return IncludeInternalMembers
				   || att == MethodAttributes.Public
				   || att == MethodAttributes.Family
				   || att == MethodAttributes.FamORAssem;
		}

		#endregion

		#region Nullability Reference Type Support

		readonly Accessibility minAccessibilityForNRT;

		static Accessibility FindMinimumAccessibilityForNRT(CustomAttributeCollection customAttributes)
		{
			// Determine the minimum effective accessibility an entity must have, so that the metadata stores the nullability for its type.
			foreach (var customAttribute in customAttributes) {
				if (customAttribute.IsKnownAttribute(KnownAttribute.NullablePublicOnly)) {
					if (customAttribute.ConstructorArguments.Count == 1 &&
						customAttribute.ConstructorArguments[0].Value is bool includesInternals) {
						return includesInternals ? Accessibility.ProtectedAndInternal : Accessibility.Protected;
					}
				}
			}

			return Accessibility.None;
		}

		internal bool ShouldDecodeNullableAttributes(IEntity entity)
		{
			if ((options & TypeSystemOptions.NullabilityAnnotations) == 0)
				return false;
			if (minAccessibilityForNRT == Accessibility.None || entity == null)
				return true;
			return minAccessibilityForNRT.LessThanOrEqual(entity.EffectiveAccessibility());
		}

		internal TypeSystemOptions OptionsForEntity(IEntity entity)
		{
			var opt = this.options;
			if ((opt & TypeSystemOptions.NullabilityAnnotations) != 0) {
				if (!ShouldDecodeNullableAttributes(entity)) {
					opt &= ~TypeSystemOptions.NullabilityAnnotations;
				}
			}

			return opt;
		}

		#endregion
	}
}
