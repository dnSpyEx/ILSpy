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
using System.Text;
using System.Threading;

using dnlib.DotNet;

using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem
{
	/// <summary>
	/// Options that control how metadata is represented in the type system.
	/// </summary>
	[Flags]
	public enum TypeSystemOptions
	{
		/// <summary>
		/// No options enabled; stay as close to the metadata as possible.
		/// </summary>
		None = 0,
		/// <summary>
		/// [DynamicAttribute] is used to replace 'object' types with the 'dynamic' type.
		///
		/// If this option is not active, the 'dynamic' type is not used, and the attribute is preserved.
		/// </summary>
		Dynamic = 1,
		/// <summary>
		/// Tuple types are represented using the TupleType class.
		/// [TupleElementNames] is used to name the tuple elements.
		///
		/// If this option is not active, the tuples are represented using their underlying type, and the attribute is preserved.
		/// </summary>
		Tuple = 2,
		/// <summary>
		/// If this option is active, [ExtensionAttribute] is removed and methods are marked as IsExtensionMethod.
		/// Otherwise, the attribute is preserved but the methods are not marked.
		/// </summary>
		ExtensionMethods = 4,
		/// <summary>
		/// Only load the public API into the type system.
		/// </summary>
		OnlyPublicAPI = 8,
		/// <summary>
		/// If this option is active, [DecimalConstantAttribute] is removed and constant values are transformed into simple decimal literals.
		/// </summary>
		DecimalConstants = 0x20,
		/// <summary>
		/// If this option is active, modopt and modreq types are preserved in the type system.
		///
		/// Note: the decompiler currently does not support handling modified types;
		/// activating this option may lead to incorrect decompilation or internal errors.
		/// </summary>
		KeepModifiers = 0x40,
		/// <summary>
		/// If this option is active, [IsReadOnlyAttribute] on parameters+structs is removed
		/// and parameters are marked as in, structs as readonly.
		/// Otherwise, the attribute is preserved but the parameters and structs are not marked.
		/// </summary>
		ReadOnlyStructsAndParameters = 0x80,
		/// <summary>
		/// If this option is active, [IsByRefLikeAttribute] is removed and structs are marked as ref.
		/// Otherwise, the attribute is preserved but the structs are not marked.
		/// </summary>
		RefStructs = 0x100,
		/// <summary>
		/// If this option is active, [IsUnmanagedAttribute] is removed from type parameters,
		/// and HasUnmanagedConstraint is set instead.
		/// </summary>
		UnmanagedConstraints = 0x200,
		/// <summary>
		/// If this option is active, [NullableAttribute] is removed and reference types with
		/// nullability annotations are used instead.
		/// </summary>
		NullabilityAnnotations = 0x400,
		/// <summary>
		/// If this option is active, [IsReadOnlyAttribute] on methods is removed
		/// and the method marked as ThisIsRefReadOnly.
		/// </summary>
		ReadOnlyMethods = 0x800,
		/// <summary>
		/// [NativeIntegerAttribute] is used to replace 'IntPtr' types with the 'nint' type.
		/// </summary>
		NativeIntegers = 0x1000,
		/// <summary>
		/// Allow function pointer types. If this option is not enabled, function pointers are
		/// replaced with the 'IntPtr' type.
		/// </summary>
		FunctionPointers = 0x2000,
		/// <summary>
		/// Allow C# 11 scoped annotation. If this option is not enabled, ScopedRefAttribute
		/// will be reported as custom attribute.
		/// </summary>
		ScopedRef = 0x4000,
		/// <summary>
		/// Replace 'IntPtr' types with the 'nint' type even in absence of [NativeIntegerAttribute].
		/// Note: DecompilerTypeSystem constructor removes this setting from the options if
		/// not targeting .NET 7 or later.
		/// </summary>
		NativeIntegersWithoutAttribute = 0x8000,
		/// <summary>
		/// If this option is active, [RequiresLocationAttribute] on parameters is removed
		/// and parameters are marked as ref readonly.
		/// Otherwise, the attribute is preserved but the parameters are not marked
		/// as if it was a ref parameter without any attributes.
		/// </summary>
		RefReadOnlyParameters = 0x10000,
		/// <summary>
		/// If this option is active, [ParamCollectionAttribute] on parameters is removed
		/// and parameters are marked as params.
		/// Otherwise, the attribute is preserved but the parameters are not marked
		/// as if it was a normal parameter without any attributes.
		/// </summary>
		ParamsCollections = 0x20000,
		/// <summary>
		/// If this option is active, span types (Span&lt;T&gt; and ReadOnlySpan&lt;T&gt;) are treated like
		/// built-in types and language rules of C# 14 and later are applied.
		/// </summary>
		FirstClassSpanTypes = 0x40000,
		/// <summary>
		/// If this option is active, extension member groups are detected, otherwise the compiler-generated nested classes are left as-is.
		/// </summary>
		ExtensionMembers = 0x80000,
		/// <summary>
		/// Default settings: typical options for the decompiler, with all C# language features enabled.
		/// </summary>
		Default = Dynamic | Tuple | ExtensionMethods | DecimalConstants | ReadOnlyStructsAndParameters
			| RefStructs | UnmanagedConstraints | NullabilityAnnotations | ReadOnlyMethods
			| NativeIntegers | FunctionPointers | ScopedRef | NativeIntegersWithoutAttribute
			| RefReadOnlyParameters | ParamsCollections | FirstClassSpanTypes | ExtensionMembers
	}

	/// <summary>
	/// Manages the NRefactory type system for the decompiler.
	/// </summary>
	/// <remarks>
	/// This class is thread-safe.
	/// </remarks>
	public class DecompilerTypeSystem : IDecompilerTypeSystem
	{
		private TypeSystemOptions options;
		private ITypeResolveContext resolveContext;
		MetadataModule mainModule;
		private ModuleDef maindnlibMod;
		KnownTypeCache knownTypeCache;
		List<IModule> modules;
		Dictionary<ModuleDef, IModule> dnlibModules;
		List<IModule> referencedModules;

		INamespace rootNamespace;
		TypeSystemOptions typeSystemOptions;

		public static TypeSystemOptions GetOptions(DecompilerSettings settings)
		{
			var typeSystemOptions = TypeSystemOptions.None;
			if (settings.Dynamic)
				typeSystemOptions |= TypeSystemOptions.Dynamic;
			if (settings.TupleTypes)
				typeSystemOptions |= TypeSystemOptions.Tuple;
			if (settings.ExtensionMethods)
				typeSystemOptions |= TypeSystemOptions.ExtensionMethods;
			if (settings.DecimalConstants)
				typeSystemOptions |= TypeSystemOptions.DecimalConstants;
			if (settings.IntroduceRefModifiersOnStructs)
				typeSystemOptions |= TypeSystemOptions.RefStructs;
			if (settings.IntroduceReadonlyAndInModifiers)
				typeSystemOptions |= TypeSystemOptions.ReadOnlyStructsAndParameters;
			if (settings.IntroduceUnmanagedConstraint)
				typeSystemOptions |= TypeSystemOptions.UnmanagedConstraints;
			if (settings.NullableReferenceTypes)
				typeSystemOptions |= TypeSystemOptions.NullabilityAnnotations;
			if (settings.ReadOnlyMethods)
				typeSystemOptions |= TypeSystemOptions.ReadOnlyMethods;
			if (settings.NativeIntegers)
				typeSystemOptions |= TypeSystemOptions.NativeIntegers;
			if (settings.FunctionPointers)
				typeSystemOptions |= TypeSystemOptions.FunctionPointers;
			if (settings.ScopedRef)
				typeSystemOptions |= TypeSystemOptions.ScopedRef;
			if (settings.NumericIntPtr)
				typeSystemOptions |= TypeSystemOptions.NativeIntegersWithoutAttribute;
			if (settings.RefReadOnlyParameters)
				typeSystemOptions |= TypeSystemOptions.RefReadOnlyParameters;
			if (settings.ParamsCollections)
				typeSystemOptions |= TypeSystemOptions.ParamsCollections;
			if (settings.FirstClassSpanTypes)
				typeSystemOptions |= TypeSystemOptions.FirstClassSpanTypes;
			if (settings.ExtensionMembers)
				typeSystemOptions |= TypeSystemOptions.ExtensionMembers;
			return typeSystemOptions;
		}

		public DecompilerTypeSystem(MetadataFile mainModule, DecompilerSettings settings)
			: this(mainModule, GetOptions(settings ?? throw new ArgumentNullException(nameof(settings))))
		{
		}

		public DecompilerTypeSystem(MetadataFile mainModule, TypeSystemOptions typeSystemOptions)
		{
			if (mainModule == null)
				throw new ArgumentNullException(nameof(mainModule));
			maindnlibMod = mainModule.Metadata;
			options = typeSystemOptions;
			this.typeSystemOptions = typeSystemOptions;

			// if (!(identifier == TargetFrameworkIdentifier.NET && version >= new Version(7, 0)))
			// {
			// 	typeSystemOptions &= ~TypeSystemOptions.NativeIntegersWithoutAttribute;
			// }

			Init(mainModule.WithOptions(typeSystemOptions));

			var corLibAsm = maindnlibMod.Context.AssemblyResolver.Resolve(maindnlibMod.CorLibTypes.AssemblyRef, maindnlibMod);
			if (corLibAsm != null)
				GetOrAddModule(corLibAsm.ManifestModule);
			else
				modules.Add(FakeCorLibModule = new FakeCorLibTypesModule(this, maindnlibMod));
		}

		void Init(IModuleReference mainAssembly)
		{
			this.resolveContext = new SimpleTypeResolveContext(this);
			this.mainModule = (MetadataModule)mainAssembly.Resolve(resolveContext);
			dnlibModules = new Dictionary<ModuleDef, IModule> {
				[maindnlibMod] = mainModule
			};
			this.modules = new List<IModule> { this.mainModule };
			this.referencedModules = new List<IModule>();
			this.knownTypeCache = new KnownTypeCache(this);
		}

		public MetadataModule MainModule {
			get {
				return mainModule;
			}
		}

		public FakeCorLibTypesModule FakeCorLibModule { get; }

		IModule ICompilation.MainModule => MainModule;

		public IReadOnlyList<IModule> Modules {
			get {
				return modules.AsReadOnly();
			}
		}

		public IModule GetOrAddModule(ModuleDef module)
		{
			lock (dnlibModules) {
				if (dnlibModules.TryGetValue(module, out var tsMod))
					return tsMod;
				tsMod = new MetadataFile(module).WithOptions(options).Resolve(resolveContext);
				modules.Add(tsMod);
				referencedModules.Add(tsMod);
				dnlibModules[module] = tsMod;
				Interlocked.Exchange(ref this.rootNamespace, null);
				return tsMod;
			}
		}

		public INamespace RootNamespace {
			get {
				INamespace ns = LazyInit.VolatileRead(ref this.rootNamespace);
				if (ns != null) {
					return ns;
				}
				return LazyInit.GetOrSet(ref this.rootNamespace, CreateRootNamespace());
			}
		}

		private INamespace CreateRootNamespace()
		{
			// SimpleCompilation does not support extern aliases; but derived classes might.
			// CreateRootNamespace() is virtual so that derived classes can change the global namespace.
			lock (dnlibModules)
			{
				INamespace[] namespaces = new INamespace[referencedModules.Count + 1];
				namespaces[0] = mainModule.RootNamespace;
				for (int i = 0; i < referencedModules.Count; i++) {
					namespaces[i + 1] = referencedModules[i].RootNamespace;
				}
				return new MergedNamespace(this, namespaces);
			}
		}

		public CacheManager CacheManager { get; } = new CacheManager();

		public INamespace GetNamespaceForExternAlias(string alias)
		{
			if (string.IsNullOrEmpty(alias))
				return this.RootNamespace;
			// SimpleCompilation does not support extern aliases; but derived classes might.
			return null;
		}

		public IType FindType(KnownTypeCode typeCode)
		{
			return knownTypeCache.FindType(typeCode);
		}

		public StringComparer NameComparer {
			get { return StringComparer.Ordinal; }
		}

		public override string ToString()
		{
			return "[" + GetType().Name + " " + mainModule.AssemblyName + "]";
		}

		public TypeSystemOptions TypeSystemOptions => typeSystemOptions;
	}
}
