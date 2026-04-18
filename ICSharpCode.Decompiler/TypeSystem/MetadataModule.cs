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

		readonly MetadataNamespace rootNamespace;
		readonly MetadataTypeDefinition[] typeDefs;
		readonly MetadataField[] fieldDefs;
		readonly MetadataMethod[] methodDefs;
		readonly MetadataProperty[] propertyDefs;
		readonly MetadataEvent[] eventDefs;

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
			} else {
				var moddef = metadata;
				this.AssemblyName = moddef.Name;
				this.FullAssemblyName = this.AssemblyName;
			}

			var customAttrs = metadata.CustomAttributes;
			this.NullableContext = customAttrs.GetNullableContext() ?? Nullability.Oblivious;
			this.minAccessibilityForNRT = FindMinimumAccessibilityForNRT(customAttrs);
			this.rootNamespace = new MetadataNamespace(this, null, string.Empty,
				NamespaceDefinition.GetRootNamespace(compilation.NameComparer, metadata.Types));

			if (!options.HasFlag(TypeSystemOptions.Uncached)) {
				// create arrays for resolved entities, indexed by row index
				uint typeCount = 0;
				uint fieldCount = 0;
				uint methodCount = 0;
				uint propertyCount = 0;
				uint eventCount = 0;

				if (metadata is ModuleDefMD md) {
					typeCount = md.Metadata.TablesStream.TypeDefTable.Rows + 1;
					fieldCount = md.Metadata.TablesStream.FieldTable.Rows + 1;
					methodCount = md.Metadata.TablesStream.MethodTable.Rows + 1;
					propertyCount = md.Metadata.TablesStream.PropertyTable.Rows + 1;
					eventCount = md.Metadata.TablesStream.EventTable.Rows + 1;
				} else {
					foreach (TypeDef type in metadata.GetTypes()) {
						typeCount++;
						fieldCount += (uint)type.Fields.Count;
						methodCount += (uint)type.Methods.Count;
						propertyCount += (uint)type.Properties.Count;
						eventCount += (uint)type.Events.Count;
					}
				}

				this.typeDefs = new MetadataTypeDefinition[typeCount];
				this.fieldDefs = new MetadataField[fieldCount];
				this.methodDefs = new MetadataMethod[methodCount];
				this.propertyDefs = new MetadataProperty[propertyCount];
				this.eventDefs = new MetadataEvent[eventCount];
			}
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

		public INamespace RootNamespace => rootNamespace;

		public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions => TypeDefinitions.Where(td => td.DeclaringTypeDefinition == null);

		public ITypeDefinition GetTypeDefinition(TopLevelTypeName topLevelTypeName)
		{
			var typeDefHandle = MetadataFile.GetTypeDefinition(topLevelTypeName);
			if (typeDefHandle == null)
			{
				var forwarderHandle = MetadataFile.GetTypeForwarder(topLevelTypeName);
				if (forwarderHandle != null)
				{
					var forwarder = forwarderHandle;
					return ResolveForwardedType(forwarder).GetDefinition();
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

		string[] internalsVisibleTo;

		string[] GetInternalsVisibleTo()
		{
			var result = LazyInit.VolatileRead(ref this.internalsVisibleTo);
			if (result != null) {
				return result;
			}
			if (metadata.Assembly != null) {
				var list = new List<string>();
				foreach (var attr in metadata.Assembly.CustomAttributes) {
					if (attr.IsKnownAttribute(KnownAttribute.InternalsVisibleTo)) {
						if (attr.ConstructorArguments.Count == 1) {
							if (attr.ConstructorArguments[0].Value is UTF8String s) {
								list.Add(GetShortName(s));
							}
						}
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

		static string GetShortName(string fullAssemblyName)
		{
			if (fullAssemblyName == null)
				return null;
			int pos = fullAssemblyName.IndexOf(',');
			if (pos < 0)
				return fullAssemblyName;
			else
				return fullAssemblyName.Substring(0, pos);
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
			if (handle == null)
				return null;
			if (typeDefs == null)
				return new MetadataTypeDefinition(this, handle);
			uint row = handle.Rid;
			if (row >= typeDefs.Length)
				HandleOutOfRange(handle);
			var typeDef = LazyInit.VolatileRead(ref typeDefs[row]);
			if (typeDef != null)
				return typeDef;
			typeDef = new MetadataTypeDefinition(this, handle);
			return LazyInit.GetOrSet(ref typeDefs[row], typeDef);
		}

		public IField GetDefinition(FieldDef handle)
		{
			if (handle == null)
				return null;
			if (fieldDefs == null)
				return new MetadataField(this, handle);
			uint row = handle.Rid;
			if (row >= fieldDefs.Length)
				HandleOutOfRange(handle);
			var field = LazyInit.VolatileRead(ref fieldDefs[row]);
			if (field != null)
				return field;
			field = new MetadataField(this, handle);
			return LazyInit.GetOrSet(ref fieldDefs[row], field);
		}

		public IMethod GetDefinition(MethodDef handle)
		{
			if (handle == null)
				return null;
			if (methodDefs == null)
				return new MetadataMethod(this, handle);
			uint row = handle.Rid;
			Debug.Assert(row != 0);
			if (row >= methodDefs.Length)
				HandleOutOfRange(handle);
			var method = LazyInit.VolatileRead(ref methodDefs[row]);
			if (method != null)
				return method;
			method = new MetadataMethod(this, handle);
			return LazyInit.GetOrSet(ref methodDefs[row], method);
		}

		public IProperty GetDefinition(PropertyDef handle)
		{
			if (handle == null)
				return null;
			if (propertyDefs == null)
				return new MetadataProperty(this, handle);
			uint row = handle.Rid;
			Debug.Assert(row != 0);
			if (row >= propertyDefs.Length)
				HandleOutOfRange(handle);
			var property = LazyInit.VolatileRead(ref propertyDefs[row]);
			if (property != null)
				return property;
			property = new MetadataProperty(this, handle);
			return LazyInit.GetOrSet(ref propertyDefs[row], property);
		}

		public IEvent GetDefinition(EventDef handle)
		{
			if (handle == null)
				return null;
			if (eventDefs == null)
				return new MetadataEvent(this, handle);
			uint row = handle.Rid;
			Debug.Assert(row != 0);
			if (row >= eventDefs.Length)
				HandleOutOfRange(handle);
			var ev = LazyInit.VolatileRead(ref eventDefs[row]);
			if (ev != null)
				return ev;
			ev = new MetadataEvent(this, handle);
			return LazyInit.GetOrSet(ref eventDefs[row], ev);
		}

		void HandleOutOfRange(IDnlibDef handle)
		{
			throw new BadImageFormatException("Handle with invalid row number.");
		}
		#endregion

		#region Resolve Type
		public IType ResolveType(dnlib.DotNet.IType typeRefDefSpec, GenericContext context, IHasCustomAttribute typeAttributes = null, Nullability nullableContext = Nullability.Oblivious)
		{
			return ResolveType(typeRefDefSpec, context, options, typeAttributes, nullableContext);
		}

		public IType ResolveType(dnlib.DotNet.IType typeRefDefSpec, GenericContext context, TypeSystemOptions customOptions, IHasCustomAttribute typeAttributes = null, Nullability nullableContext = Nullability.Oblivious)
		{
			if (typeRefDefSpec == null)
				return SpecialType.UnknownType;
			IType ty;
			switch (typeRefDefSpec.MDToken.Table) {
				case Table.TypeDef:
					ty =((TypeDef)typeRefDefSpec).DecodeSignature(this, new GenericContext());
					break;
				case Table.TypeRef:
					ty = ((TypeRef)typeRefDefSpec).DecodeSignature(this, new GenericContext());
					break;
				case Table.TypeSpec:
					if (typeRefDefSpec is TypeSpec) {
						var typeSpec = ((TypeSpec)typeRefDefSpec);
						ty = typeSpec.DecodeSignature(this, context);
					} else {
						var typeSpec = ((TypeSig)typeRefDefSpec);
						ty = typeSpec.DecodeSignature(this, context);
					}
					break;
				case Table.ExportedType:
					return ResolveForwardedType(((ExportedType)typeRefDefSpec));
				default:
					throw new BadImageFormatException("Not a type handle");
			}
			ty = ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, typeAttributes, metadata, customOptions, nullableContext);
			return ty;
		}

		IType ResolveDeclaringType(ITypeDefOrRef declaringTypeReference, GenericContext context)
		{
			// resolve without substituting dynamic/tuple types
			const TypeSystemOptions removedOptions = TypeSystemOptions.Dynamic | TypeSystemOptions.Tuple
				| TypeSystemOptions.NullabilityAnnotations | TypeSystemOptions.NativeIntegers | TypeSystemOptions.NativeIntegersWithoutAttribute;
			var ty = ResolveType(declaringTypeReference, context, options & ~removedOptions);
			// but substitute tuple types in type arguments:
			ty = ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, null, metadata, options, Nullability.Oblivious, typeChildrenOnly: true);
			return ty;
		}

		public IType IntroduceTupleTypes(IType ty)
		{
			// run ApplyAttributeTypeVisitor without attributes, in order to introduce tuple types
			return ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, Compilation, null, metadata, options, Nullability.Oblivious);
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

		IMethod ResolveMethodDefinition(MethodDef methodDefHandle, bool expandVarArgs)
		{
			var method = GetDefinition(methodDefHandle);
			if (expandVarArgs && method.Parameters.LastOrDefault()?.Type.Kind == TypeKind.ArgList) {
				method = new VarArgInstanceMethod(method, EmptyList<IType>.Instance);
			}
			return method;
		}

		IMethod ResolveMethodSpecification(MethodSpec methodSpec, GenericContext context, bool expandVarArgs)
		{
			var methodTypeArgs = methodSpec.DecodeSignature(this, context)
										   .SelectReadOnlyArray(IntroduceTupleTypes);
			IMethod method;
			if (methodSpec.Method.MDToken.Table == Table.Method) {
				// generic instance of a methoddef (=generic method in non-generic class in current assembly)
				method = ResolveMethodDefinition((MethodDef)methodSpec.Method, expandVarArgs);
				method = method.Specialize(new TypeParameterSubstitution(null, methodTypeArgs));
			} else {
				method = ResolveMethodReference((MemberRef)methodSpec.Method, context, methodTypeArgs, expandVarArgs);
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
		IMethod ResolveMethodReference(MemberRef memberRef, GenericContext context, IReadOnlyList<IType> methodTypeArguments = null, bool expandVarArgs = true)
		{
			Debug.Assert(memberRef.IsMethodRef);
			IReadOnlyList<IType> classTypeArguments = null;
			IMethod method;
			GenericContext vaRAgCtx;
			if (memberRef.Class is MethodDef methodDef) {
				method = ResolveMethodDefinition(methodDef, expandVarArgs: false);
				vaRAgCtx = context;
			} else {
				var declaringType = ResolveDeclaringType(memberRef.DeclaringType, context);
				var declaringTypeDefinition = declaringType.GetDefinition();
				if (declaringType.TypeArguments.Count > 0) {
					classTypeArguments = declaringType.TypeArguments;
				}
				// Note: declaringType might be parameterized, but the signature is for the original method definition.
				// We'll have to search the member directly on declaringTypeDefinition.
				string name = memberRef.Name;
				vaRAgCtx = new GenericContext(declaringTypeDefinition?.TypeParameters);
				var returnType = memberRef.MethodSig.RetType.DecodeSignature(this, vaRAgCtx);
				if (declaringTypeDefinition != null) {
					// Find the set of overloads to search:
					IEnumerable<IMethod> methods;
					if (name == ".ctor") {
						methods = declaringTypeDefinition.GetConstructors();
					} else if (name == ".cctor") {
						methods = declaringTypeDefinition.Methods.Where(m => m.IsConstructor && m.IsStatic);
					} else {
						methods = declaringTypeDefinition.GetMethods(m => m.Name == name, GetMemberOptions.IgnoreInheritedMembers)
														 .Concat(declaringTypeDefinition.GetAccessors(m => m.Name == name, GetMemberOptions.IgnoreInheritedMembers));
					}
					// Determine the expected parameters from the signature:
					ImmutableArray<IType> parameterTypes;
					if (memberRef.CallingConvention == CallingConvention.VarArg) {
						parameterTypes = memberRef.MethodSig.Params.Select(x => x.DecodeSignature(this, vaRAgCtx))
							.Concat(new[] { SpecialType.ArgList })
							.ToImmutableArray();
					} else {
						parameterTypes = memberRef.MethodSig.Params.Select(x => x.DecodeSignature(this, vaRAgCtx))
												  .ToImmutableArray();
					}
					// Search for the matching method:
					method = null;
					foreach (var m in methods) {
						if (m.TypeParameters.Count != ((dnlib.DotNet.IMethod)memberRef).NumberOfGenericParameters)
							continue;
						if (memberRef.HasThis != !m.IsStatic)
							continue;
						if (CompareSignatures(m.Parameters, parameterTypes) && CompareTypes(m.ReturnType, returnType)) {
							method = m;
							break;
						}
					}
				} else {
					method = null;
				}
				if (method == null) {
					var param = memberRef.MethodSig.Params.Select(x => x.DecodeSignature(this, vaRAgCtx))
										 .ToList();
					method = CreateFakeMethod(declaringType, name, !memberRef.HasThis, returnType,
						((dnlib.DotNet.IMethod)memberRef).NumberOfGenericParameters, param);
				}
			}
			if (classTypeArguments != null || methodTypeArguments != null) {
				method = method.Specialize(new TypeParameterSubstitution(classTypeArguments, methodTypeArguments));
			}
			if (expandVarArgs && memberRef.CallingConvention == CallingConvention.VarArg) {
				method = new VarArgInstanceMethod(method, memberRef.MethodSig.ParamsAfterSentinel is null ? new List<IType>() :
					memberRef.MethodSig.ParamsAfterSentinel.Select(x => x.DecodeSignature(this, vaRAgCtx)));
			}
			return method;
		}

		static readonly NormalizeTypeVisitor normalizeTypeVisitor = new NormalizeTypeVisitor {
			ReplaceClassTypeParametersWithDummy = true,
			ReplaceMethodTypeParametersWithDummy = true,
		};

		static bool CompareTypes(IType a, IType b)
		{
			IType type1 = a.AcceptVisitor(normalizeTypeVisitor);
			IType type2 = b.AcceptVisitor(normalizeTypeVisitor);
			return type1.Equals(type2);
		}

		static bool CompareSignatures(IReadOnlyList<IParameter> parameters, ImmutableArray<IType> parameterTypes)
		{
			if (parameterTypes.Length != parameters.Count)
				return false;
			for (int i = 0; i < parameterTypes.Length; i++)
			{
				if (!CompareTypes(parameterTypes[i], parameters[i].Type))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Create a dummy IMethod from the specified MethodReference
		/// </summary>
		IMethod CreateFakeMethod(IType declaringType, string name, bool isStatic, IType retType, int numOfGeneric, List<IType> param)
		{
			SymbolKind symbolKind = SymbolKind.Method;
			if (name == ".ctor" || name == ".cctor")
				symbolKind = SymbolKind.Constructor;
			var m = new FakeMethod(Compilation, symbolKind);
			m.DeclaringType = declaringType;
			m.Name = name;
			m.ReturnType = retType;
			m.IsStatic = isStatic;

			TypeParameterSubstitution substitution = null;
			if (numOfGeneric > 0) {
				var typeParameters = new List<ITypeParameter>();
				for (int i = 0; i < numOfGeneric; i++) {
					typeParameters.Add(new DefaultTypeParameter(m, i));
				}
				m.TypeParameters = typeParameters;
				substitution = new TypeParameterSubstitution(declaringType.TypeArguments, typeParameters);
			} else if (declaringType.TypeArguments.Count > 0) {
				substitution = declaringType.GetSubstitution();
			}
			var parameters = new List<IParameter>();
			for (int i = 0; i < param.Count; i++) {
				var type = param[i];
				if (substitution != null) {
					// replace the dummy method type parameters with the owned instances we just created
					type = type.AcceptVisitor(substitution);
				}
				parameters.Add(new DefaultParameter(type, ""));
			}
			m.Parameters = parameters;

			GuessFakeMethodAccessor(declaringType, name, retType, numOfGeneric, m, parameters);

			return m;
		}

		private void GuessFakeMethodAccessor(IType declaringType, string name, IType retType, int numOfGeneric, FakeMethod m, List<IParameter> parameters)
		{
			if (numOfGeneric > 0)
				return;

			var guessedGetter = name.StartsWith("get_", StringComparison.Ordinal);
			var guessedSetter = name.StartsWith("set_", StringComparison.Ordinal);
			if (guessedGetter || guessedSetter)
			{
				var propertyName = name.Substring(4);

				var fakeProperty = new FakeProperty(Compilation) {
					Name = propertyName,
					DeclaringType = declaringType,
					IsStatic = m.IsStatic,
				};

				if (guessedGetter)
				{
					if (retType.Kind == TypeKind.Void)
						return;

					m.AccessorKind = MethodSemanticsAttributes.Getter;
					m.AccessorOwner = fakeProperty;
					fakeProperty.Getter = m;
					fakeProperty.ReturnType = retType;
					fakeProperty.IsIndexer = parameters.Count > 0;
					fakeProperty.Parameters = parameters;
					return;
				}

				if (guessedSetter)
				{
					if (parameters.Count < 1 || retType.Kind != TypeKind.Void)
						return;

					m.AccessorKind = MethodSemanticsAttributes.Setter;
					m.AccessorOwner = fakeProperty;
					fakeProperty.Setter = m;
					fakeProperty.ReturnType = parameters.Last().Type;
					fakeProperty.IsIndexer = parameters.Count > 1;
					fakeProperty.Parameters = parameters.SkipLast(1).ToArray();
					return;
				}
			}

			const string addPrefix = "add_";
			const string removePrefix = "remove_";
			const string raisePrefix = "raise_";
			var guessedAdd = name.StartsWith(addPrefix, StringComparison.Ordinal);
			var guessedRemove = name.StartsWith(removePrefix, StringComparison.Ordinal);
			var guessedRaise = name.StartsWith(raisePrefix, StringComparison.Ordinal);
			if (guessedAdd || guessedRemove || guessedRaise)
			{
				var fakeEvent = new FakeEvent(Compilation) {
					DeclaringType = declaringType,
					IsStatic = m.IsStatic,
				};

				if (guessedAdd)
				{
					if (parameters.Count != 1)
						return;

					m.AccessorKind = MethodSemanticsAttributes.AddOn;
					m.AccessorOwner = fakeEvent;

					fakeEvent.Name = name.Substring(addPrefix.Length);
					fakeEvent.AddAccessor = m;
					fakeEvent.ReturnType = parameters.Single().Type;

					return;
				}

				if (guessedRemove)
				{
					if (parameters.Count != 1)
						return;

					m.AccessorKind = MethodSemanticsAttributes.RemoveOn;
					m.AccessorOwner = fakeEvent;

					fakeEvent.Name = name.Substring(removePrefix.Length);
					fakeEvent.RemoveAccessor = m;
					fakeEvent.ReturnType = parameters.Single().Type;

					return;
				}

				if (guessedRaise)
				{
					fakeEvent.Name = name.Substring(raisePrefix.Length);
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
			switch (entityHandle.MDToken.Table) {
				case Table.TypeRef:
				case Table.TypeDef:
				case Table.TypeSpec:
					// Using ResolveDeclaringType() here because ResolveType() might return
					// nint/nuint which are SpecialTypes without a definition.
					return ResolveDeclaringType((dnlib.DotNet.ITypeDefOrRef)entityHandle, context).GetDefinition();
				case Table.ExportedType:
					return ResolveForwardedType((ExportedType)entityHandle).GetDefinition();
				case Table.MemberRef:
					var memberReferenceHandle = (MemberRef)entityHandle;
					if (memberReferenceHandle.IsMethodRef) {
						// for consistency with the MethodDefinition case, never expand varargs
						return ResolveMethodReference(memberReferenceHandle, context, expandVarArgs: false);
					}
					else if (memberReferenceHandle.IsFieldRef) {
						return ResolveFieldReference(memberReferenceHandle, context);
					} else {
						throw new BadImageFormatException("Unknown MemberReferenceKind");
					}
				case Table.Method:
					return GetDefinition((MethodDef)entityHandle);
				case Table.MethodSpec:
					return ResolveMethodSpecification((MethodSpec)entityHandle, context, expandVarArgs: false);
				case Table.Field:
					return GetDefinition((FieldDef)entityHandle);
				case Table.Event:
					return GetDefinition((EventDef)entityHandle);
				case Table.Property:
					return GetDefinition((PropertyDef)entityHandle);
				default:
					return null;
			}
		}

		IField ResolveFieldReference(MemberRef memberRef, GenericContext context)
		{
			var declaringType = ResolveDeclaringType(memberRef.DeclaringType, context);
			var declaringTypeDefinition = declaringType.GetDefinition();
			string name = memberRef.Name;
			// field signature is for the definition, not the generic instance
			var signature = memberRef.FieldSig.Type.DecodeSignature(this,
				new GenericContext(declaringTypeDefinition?.TypeParameters));
			// 'f' in the predicate is also the definition, even if declaringType is a ParameterizedType
			var field = declaringType.GetFields(f => f.Name == name && CompareTypes(f.ReturnType, signature),
				GetMemberOptions.IgnoreInheritedMembers).FirstOrDefault();
			if (field == null) {
				// If it's a field in a generic type, we need to substitute the type arguments:
				if (declaringType.TypeArguments.Count > 0) {
					signature = signature.AcceptVisitor(declaringType.GetSubstitution());
				}
				field = new FakeField(Compilation) {
					ReturnType = signature,
					Name = name,
					DeclaringType = declaringType,
				};
			}
			return field;
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
				var assembly = metadata.Assembly;
				b.Add(assembly.CustomAttributes, SymbolKind.Module);
				b.AddSecurityAttributes(assembly.DeclSecurities);

				// AssemblyVersionAttribute
				if (assembly.Version != null) {
					b.Add(KnownAttribute.AssemblyVersion, KnownTypeCode.String, assembly.Version.ToString());
				}

				AddTypeForwarderAttributes(ref b);
			}
			return b.Build();
		}

		/// <summary>
		/// Gets the list of all module attributes in the project.
		/// </summary>
		public IEnumerable<IAttribute> GetModuleAttributes()
		{
			var b = new AttributeListBuilder(this);
			b.Add(metadata.CustomAttributes, SymbolKind.Module);
			if (metadata.Assembly == null) {
				AddTypeForwarderAttributes(ref b);
			}
			return b.Build();
		}

		void AddTypeForwarderAttributes(ref AttributeListBuilder b)
		{
			foreach (ExportedType type in metadata.ExportedTypes) {
				if (type.IsForwarder) {
					b.Add(KnownAttribute.TypeForwardedTo, KnownTypeCode.Type, ResolveForwardedType(type));
				}
			}
		}

		IType ResolveForwardedType(ExportedType forwarder)
		{
			IModule module = ResolveModule(forwarder);
			var typeName = forwarder.GetFullTypeName();
			if (module == null)
				return new UnknownType(typeName);
			using (var busyLock = BusyManager.Enter(this)) {
				if (busyLock.Success) {
					var td = module.GetTypeDefinition(typeName);
					if (td != null) {
						return td;
					}
				}
			}
			return new UnknownType(typeName);

			IModule ResolveModule(ExportedType type)
			{
				switch (type.Scope) {
					case IAssembly asmRef:
						string shortName = asmRef.Name;
						foreach (var asm in Compilation.Modules) {
							if (string.Equals(asm.AssemblyName, shortName, StringComparison.OrdinalIgnoreCase)) {
								return asm;
							}
						}
						return null;
					default:
						throw new BadImageFormatException("Expected implementation to be either an AssemblyFile, ExportedType or AssemblyReference.");
				}
			}
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
					if (customAttribute.ConstructorArguments.Count == 1 && customAttribute.ConstructorArguments[0].Value is bool includesInternals) {
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
