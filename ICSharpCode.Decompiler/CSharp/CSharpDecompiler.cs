// Copyright (c) 2014 Daniel Grunwald
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
using System.Text.RegularExpressions;
using System.Threading;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.CSharp.Syntax;
using dnlib.DotNet;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.CSharp.TypeSystem;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

using System.Text;
using ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching;
using dnlib.DotNet.Emit;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace ICSharpCode.Decompiler.CSharp
{
	/// <summary>
	/// Main class of the C# decompiler engine.
	/// </summary>
	/// <remarks>
	/// Instances of this class are not thread-safe. Use separate instances to decompile multiple members in parallel.
	/// (in particular, the transform instances are not thread-safe)
	/// </remarks>
	public class CSharpDecompiler
	{
		readonly IDecompilerTypeSystem typeSystem;
		readonly MetadataModule module;
		readonly ModuleDef metadata;
		public readonly DecompilerSettings settings;
		SyntaxTree syntaxTree;

		List<IILTransform> ilTransforms = GetILTransforms();

		/// <summary>
		/// Pre-yield/await transforms.
		/// </summary>
		internal static List<IILTransform> EarlyILTransforms(bool aggressivelyDuplicateReturnBlocks = false)
		{
			return new List<IILTransform> {
				new ControlFlowSimplification {
					aggressivelyDuplicateReturnBlocks = aggressivelyDuplicateReturnBlocks
				},
				new SplitVariables(),
				new ILInlining(),
			};
		}

		/// <summary>
		/// Returns all built-in transforms of the ILAst pipeline.
		/// </summary>
		public static List<IILTransform> GetILTransforms()
		{
			return new List<IILTransform> {
				new ControlFlowSimplification(),
				// Run SplitVariables only after ControlFlowSimplification duplicates return blocks,
				// so that the return variable is split and can be inlined.
				new SplitVariables(),
				new ILInlining(),
				new InlineReturnTransform(), // must run before DetectPinnedRegions
				new RemoveInfeasiblePathTransform(),
				new DetectPinnedRegions(), // must run after inlining but before non-critical control flow transforms
				new YieldReturnDecompiler(), // must run after inlining but before loop detection
				new AsyncAwaitDecompiler(),  // must run after inlining but before loop detection
				new DetectCatchWhenConditionBlocks(), // must run after inlining but before loop detection
				new DetectExitPoints(),
				new LdLocaDupInitObjTransform(),
				new EarlyExpressionTransforms(),
				new SplitVariables(), // split variables once again, because the stobj(ldloca V, ...) may open up new replacements
				// RemoveDeadVariableInit must run after EarlyExpressionTransforms so that stobj(ldloca V, ...)
				// is already collapsed into stloc(V, ...).
				new RemoveDeadVariableInit(),
				new ControlFlowSimplification(), //split variables may enable new branch to leave inlining
				new DynamicCallSiteTransform(),
				new SwitchDetection(),
				new SwitchOnStringTransform(),
				new SwitchOnNullableTransform(),
				new SplitVariables(), // split variables once again, because SwitchOnNullableTransform eliminates ldloca
				new IntroduceRefReadOnlyModifierOnLocals(),
				new BlockILTransform { // per-block transforms
					PostOrderTransforms = {
						// Even though it's a post-order block-transform as most other transforms,
						// let's keep LoopDetection separate for now until there's a compelling
						// reason to combine it with the other block transforms.
						// If we ran loop detection after some if structures are already detected,
						// we might make our life introducing good exit points more difficult.
						new LoopDetection()
					}
				},
				// re-run DetectExitPoints after loop detection
				new DetectExitPoints(),
				new PatternMatchingTransform(), // must run after LoopDetection and before ConditionDetection
				new BlockILTransform { // per-block transforms
					PostOrderTransforms = {
						new ConditionDetection(),
						new LockTransform(),
						new UsingTransform(),
						// CachedDelegateInitialization must run after ConditionDetection and before/in LoopingBlockTransform
						// and must run before NullCoalescingTransform
						new CachedDelegateInitialization(),
						new StatementTransform(
							// per-block transforms that depend on each other, and thus need to
							// run interleaved (statement by statement).
							// Pretty much all transforms that open up new expression inlining
							// opportunities belong in this category.
							new ILInlining() { options = InliningOptions.AllowInliningOfLdloca },
							// Inlining must be first, because it doesn't trigger re-runs.
							// Any other transform that opens up new inlining opportunities should call RequestRerun().
							new ExpressionTransforms(),
							new DynamicIsEventAssignmentTransform(),
							new TransformAssignment(), // inline and compound assignments
							new NullCoalescingTransform(),
							new NullableLiftingStatementTransform(),
							new NullPropagationStatementTransform(),
							new TransformArrayInitializers(),
							new TransformCollectionAndObjectInitializers(),
							new TransformExpressionTrees(),
							new IndexRangeTransform(),
							new DeconstructionTransform(),
							new NamedArgumentTransform(),
							new RemoveUnconstrainedGenericReferenceTypeCheck(),
							new UserDefinedLogicTransform(),
							new InterpolatedStringTransform()
						),
					}
				},
				new ProxyCallReplacer(),
				new FixRemainingIncrements(),
				new CopyPropagation(),
				new DelegateConstruction(),
				new LocalFunctionDecompiler(),
				new TransformDisplayClassUsage(),
				new HighLevelLoopTransform(),
				new ReduceNestingTransform(),
				new RemoveRedundantReturn(),
				new IntroduceDynamicTypeOnLocals(),
				new IntroduceNativeIntTypeOnLocals(),
				new AssignVariableNames(),
			};
		}

		List<IAstTransform> astTransforms = GetAstTransforms();

		/// <summary>
		/// Returns all built-in transforms of the C# AST pipeline.
		/// </summary>
		public static List<IAstTransform> GetAstTransforms()
		{
			return new List<IAstTransform> {
				new PatternStatementTransform(),
				new ReplaceMethodCallsWithOperators(), // must run before DeclareVariables.EnsureExpressionStatementsAreValid
				new IntroduceUnsafeModifier(),
				new AddCheckedBlocks(),
				new DeclareVariables(), // should run after most transforms that modify statements
				new TransformFieldAndConstructorInitializers(), // must run after DeclareVariables
				new PrettifyAssignments(), // must run after DeclareVariables
				new IntroduceUsingDeclarations(),
				new IntroduceExtensionMethods(), // must run after IntroduceUsingDeclarations
				new IntroduceQueryExpressions(), // must run after IntroduceExtensionMethods
				new CombineQueryExpressions(),
				new NormalizeBlockStatements(),
				new FlattenSwitchBlocks(),
			};
		}

		/// <summary>
		/// Token to check for requested cancellation of the decompilation.
		/// </summary>
		public CancellationToken CancellationToken { get; set; }

		/// <summary>
		/// The type system created from the main module and referenced modules.
		/// </summary>
		public IDecompilerTypeSystem TypeSystem => typeSystem;

		/// <summary>
		/// Gets or sets the optional provider for XML documentation strings.
		/// </summary>
		public IDocumentationProvider DocumentationProvider { get; set; }

		/// <summary>
		/// IL transforms.
		/// </summary>
		public IList<IILTransform> ILTransforms {
			get { return ilTransforms; }
		}

		/// <summary>
		/// C# AST transforms.
		/// </summary>
		public IList<IAstTransform> AstTransforms {
			get { return astTransforms; }
		}

		public CSharpDecompiler()
		{
		}

		/// <summary>
		/// Creates a new <see cref="CSharpDecompiler"/> instance from the given <paramref name="module"/> and <paramref name="settings"/>.
		/// </summary>
		public CSharpDecompiler(ModuleDef module, DecompilerSettings settings)
			: this(new MetadataFile(module), settings)
		{
		}

		/// <summary>
		/// Creates a new <see cref="CSharpDecompiler"/> instance from the given <paramref name="module"/> and <paramref name="settings"/>.
		/// </summary>
		public CSharpDecompiler(MetadataFile module, DecompilerSettings settings)
			: this(new DecompilerTypeSystem(module, settings), settings)
		{
		}

		/// <summary>
		/// Creates a new <see cref="CSharpDecompiler"/> instance from the given <paramref name="typeSystem"/> and the given <paramref name="settings"/>.
		/// </summary>
		public CSharpDecompiler(IDecompilerTypeSystem typeSystem, DecompilerSettings settings)
		{
			this.typeSystem = typeSystem ?? throw new ArgumentNullException(nameof(typeSystem));
			this.settings = settings;
			this.module = typeSystem.MainModule;
			this.metadata = module.metadata;
		}

		#region MemberIsHidden
		/// <summary>
		/// Determines whether a <paramref name="member"/> should be hidden from the decompiled code. This is used to exclude compiler-generated code that is handled by transforms from the output.
		/// </summary>
		/// <param name="member">The member. Can be a TypeDef, MethodDef or FieldDef.</param>
		/// <param name="settings">THe settings used to determine whether code should be hidden. E.g. if async methods are not transformed, async state machines are included in the decompiled code.</param>
		public static bool MemberIsHidden(IMDTokenProvider member, DecompilerSettings settings)
		{
			MethodDef method = member as MethodDef;
			if (method != null) {
				if (method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn)
					return true;
				if (method.Name == ".ctor" && method.RVA == 0 && method.DeclaringType.IsImport)
					return true;
				if (settings.ForceShowAllMembers)
					return false;
				if (IsAccessorInterfaceImplementationRuntimeHelper(method))
					return true;
				if (settings.LocalFunctions && LocalFunctionDecompiler.IsLocalFunctionMethod(null, method))
					return true;
				if (settings.AnonymousMethods && method.HasGeneratedName() && method.IsCompilerGenerated())
					return method.Name != "<Extension>$";
				if (settings.AsyncAwait && AsyncAwaitDecompiler.IsCompilerGeneratedMainMethod(method))
					return true;
				return false;
			}

			TypeDef type = member as TypeDef;
			if (type != null) {
				if (settings.ForceShowAllMembers)
					return false;
				if (type.DeclaringType != null) {
					if (settings.LocalFunctions && LocalFunctionDecompiler.IsLocalFunctionDisplayClass(null, type))
						return true;
					if (settings.AnonymousMethods && IsClosureType(type))
						return true;
					if (settings.YieldReturn && YieldReturnDecompiler.IsCompilerGeneratorEnumerator(type))
						return true;
					if (settings.AsyncAwait && AsyncAwaitDecompiler.IsCompilerGeneratedStateMachine(type))
						return true;
					if (settings.AsyncEnumerator && AsyncAwaitDecompiler.IsCompilerGeneratorAsyncEnumerator(type))
						return true;
					if (settings.FixedBuffers && type.Name.StartsWith("<", StringComparison.Ordinal) && type.Name.Contains("__FixedBuffer"))
						return true;
					if (settings.InlineArrays && type.Name.StartsWith("<>y__InlineArray", StringComparison.Ordinal) && type.Name.EndsWith("`1", StringComparison.Ordinal))
						return true;
					if (settings.ExtensionMembers && (type.Name.StartsWith("<>E__", StringComparison.Ordinal) || type.Name.StartsWith("<G>$", StringComparison.Ordinal)))
						return true;
				} else if (type.IsCompilerGenerated()) {
					if (settings.ArrayInitializers && type.Name.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal))
						return true;
					if (settings.AnonymousTypes && type.IsAnonymousType())
						return true;
					if (settings.Dynamic && type.IsDelegate && (type.Name.StartsWith("<>A", StringComparison.Ordinal) || type.Name.StartsWith("<>F", StringComparison.Ordinal)))
						return true;
				}
				if (settings.ArrayInitializers && settings.SwitchStatementOnString && type.Name.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal))
					return true;
				return false;
			}

			FieldDef field = member as FieldDef;
			if (field != null) {
				if (settings.ForceShowAllMembers)
					return false;
				if (field.IsCompilerGenerated()) {
					if (settings.AnonymousMethods && IsAnonymousMethodCacheField(field))
						return true;
					if (settings.UsePrimaryConstructorSyntaxForNonRecordTypes && IsPrimaryConstructorParameterBackingField(field))
						return true;
					if (settings.AutomaticProperties && IsAutomaticPropertyBackingField(field, out var propertyName))
					{
						if (!settings.GetterOnlyAutomaticProperties && IsGetterOnlyProperty(propertyName))
							return false;

						bool IsGetterOnlyProperty(string propertyName)
						{
							var properties = field.DeclaringType.Properties;
							foreach (var pd in properties)
							{
								if (pd.Name != propertyName)
									continue;
								return pd.GetMethod is not null && pd.SetMethod is null;
							}
							return false;
						}

						return true;
					}
					if (settings.SwitchStatementOnString && IsSwitchOnStringCache(field))
						return true;
				}
				// event-fields are not [CompilerGenerated]
				if (settings.AutomaticEvents)
				{
					foreach (var ev in field.DeclaringType.Events)
					{
						var eventName = ev.Name;
						var fieldName = field.Name;
						if (IsEventBackingFieldName(fieldName, eventName, out _))
							return true;
					}
				}
				if (settings.ArrayInitializers && field.DeclaringType.Name.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal)) {
					// hide fields starting with '__StaticArrayInit'
					if (field.Name.StartsWith("__StaticArrayInit", StringComparison.Ordinal))
						return true;
					if (field.FieldType.TypeName.StartsWith("__StaticArrayInit", StringComparison.Ordinal))
						return true;
					// hide fields starting with '$$method'
					if (field.Name.StartsWith("$$method", StringComparison.Ordinal))
						return true;
				}
			}

			return false;
		}

		static bool IsPrimaryConstructorParameterBackingField(FieldDef field)
		{
			return field.Name.StartsWith("<", StringComparison.Ordinal) && field.Name.EndsWith(">P", StringComparison.Ordinal);
		}

		static bool IsAccessorInterfaceImplementationRuntimeHelper(MethodDef method)
		{
			if ((method.Attributes & dnlib.DotNet.MethodAttributes.Static) != 0)
				return false;
			string rawName = (method.Name);
			int dot = rawName.LastIndexOf('.');
			if (dot < 0)
				return false;
			string name = rawName.Substring(dot + 1);
			if (method.Overrides.Count == 0)
				return false;
			if (method.RVA == 0)
				return false;
			if (!name.StartsWith("get_", StringComparison.Ordinal) &&
				!name.StartsWith("set_", StringComparison.Ordinal) &&
				!name.StartsWith("add_", StringComparison.Ordinal) &&
				!name.StartsWith("remove_", StringComparison.Ordinal) &&
				!name.StartsWith("raise_", StringComparison.Ordinal))
			{
				return false;
			}

			var signature = method.MethodSig;
			(int genericParameterCount, int parameterCount) =
				((int)signature.GenParamCount, signature.Params.Count);
			if (genericParameterCount == -1 || parameterCount == -1)
				return false;

			int maximumMethodSize = 4 * (parameterCount + 1) + 5 + 1;

			var body = method.Body;

			// if (body.RemainingBytes > maximumMethodSize)
			// 	return false;

			int bodyIdx = 0;

			for (int i = 0; i < parameterCount + 1; i++)
			{
				var instr = body.Instructions[bodyIdx++];
				switch (instr.OpCode.Code)
				{
					case Code.Ldarg:
					case Code.Ldarg_S:
					case Code.Ldarg_0:
					case Code.Ldarg_1:
					case Code.Ldarg_2:
					case Code.Ldarg_3:
						if (instr.GetParameterIndex() != i)
							return false;
						break;
					default:
						return false;
				}
			}

			var callInstr = body.Instructions[bodyIdx++];
			if (callInstr.OpCode.Code != Code.Call)
				return false;

			IMDTokenProvider targetHandle = callInstr.Operand as IMDTokenProvider;
			if (targetHandle is null)
				return false;

			var retInstr = body.Instructions[bodyIdx++];
			if (retInstr.OpCode.Code != Code.Ret)
				return false;

			if (bodyIdx != body.Instructions.Count)
				return false;

			MethodSig signature2;
			string otherName;
			switch (targetHandle)
			{
				case MethodDef methodDef:
					if (genericParameterCount != 0)
						return false;
					signature2 = methodDef.MethodSig;
					otherName = (methodDef.Name);
					break;
				case MethodSpec methodSpec:
					if (genericParameterCount == 0)
						return false;
					var instantiationBlob = methodSpec.GenericInstMethodSig;
					if (!IsIdentityInstantiation(instantiationBlob, genericParameterCount))
						return false;
					switch (methodSpec.Method)
					{
						case MethodDef methodSpecDef:
							signature2 = methodSpecDef.MethodSig;
							otherName = (methodSpecDef.Name);
							break;
						case MemberRef methodSpecRef when methodSpecRef.IsMethodRef:
							signature2 = methodSpecRef.MethodSig;
							otherName = (methodSpecRef.Name);
							break;
						default:
							return false;
					}
					break;
				case MemberRef methodRef when methodRef.IsMethodRef:
					if (genericParameterCount != 0)
						return false;
					signature2 = methodRef.MethodSig;
					otherName = (methodRef.Name);
					break;
				default:
					return false;
			}

			if (otherName != name)
				return false;

			return new SigComparer(SigComparerOptions.IgnoreModifiers).Equals(signature, signature2);

			static bool IsIdentityInstantiation(GenericInstMethodSig instSig, int expectedCount)
			{
				if (instSig.GenericArguments.Count != expectedCount)
				{
					return false;
				}
				for (var i = 0; i < instSig.GenericArguments.Count; i++)
				{
					if (instSig.GenericArguments[i] is GenericMVar mVar && mVar.Number != i)
						return false;
				}
				return true;
			}
		}

		static bool IsSwitchOnStringCache(dnlib.DotNet.IField field)
		{
			return field.Name.StartsWith("<>f__switch", StringComparison.Ordinal);
		}


		static readonly Regex automaticPropertyBackingFieldRegex = new Regex(@"^<(.*)>k__BackingField$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		static bool IsAutomaticPropertyBackingField(FieldDef field, out string propertyName)
		{
			propertyName = null;
			var name = field.Name;
			var m = automaticPropertyBackingFieldRegex.Match(name);
			if (m.Success)
			{
				propertyName = m.Groups[1].Value;
				return true;
			}
			if (name.StartsWith("_", StringComparison.Ordinal))
			{
				propertyName = name.Substring(1);
				return field.IsCompilerGenerated();
			}
			return false;
		}

		internal static bool IsEventBackingFieldName(string fieldName, string eventName, out int suffixLength)
		{
			suffixLength = 0;
			if (fieldName == eventName)
				return true;
			var vbSuffixLength = "Event".Length;
			if (fieldName.Length == eventName.Length + vbSuffixLength && fieldName.StartsWith(eventName, StringComparison.Ordinal) && fieldName.EndsWith("Event", StringComparison.Ordinal))
			{
				suffixLength = vbSuffixLength;
				return true;
			}
			return false;
		}

		static bool IsAnonymousMethodCacheField(dnlib.DotNet.IField field)
		{
			return field.Name.StartsWith("CS$<>", StringComparison.Ordinal) || field.Name.StartsWith("<>f__am", StringComparison.Ordinal) || field.Name.StartsWith("<>f__mg", StringComparison.Ordinal);
		}

		static bool IsClosureType(TypeDef type)
		{
			if (!type.IsCompilerGenerated())
				return false;
			if (type.Name.StartsWith("_Closure$__"))
				return true;
			if (!type.HasGeneratedName())
				return false;
			if (type.Name.Contains("DisplayClass") || type.Name.Contains("AnonStorey")|| type.Name.Contains("Closure$"))
				return true;
			return type.BaseType.FullName == "System.Object" && !type.HasInterfaces;
		}

		internal static bool IsTransparentIdentifier(string identifier)
		{
			return identifier.StartsWith("<>", StringComparison.Ordinal)
				&& (identifier.Contains("TransparentIdentifier") || identifier.Contains("TranspIdent"));
		}
		#endregion

		#region NativeOrdering

		/// <summary>
		/// Determines whether a given type requires that its methods be ordered precisely as they were originally defined.
		/// </summary>
		/// <param name="typeDef">The type whose members may need native ordering.</param>
		internal bool RequiresNativeOrdering(ITypeDefinition typeDef)
		{
			// The main scenario for requiring the native method ordering is COM interop, where the V-table is fixed by the ABI
			return ComHelper.IsComImport(typeDef);
		}

		/// <summary>
		/// Compare handles with the method definition ordering intact by using the underlying method's MetadataToken,
		/// which is defined as the index into a given metadata table. This should equate to the original order that
		/// methods and properties were defined by the author.
		/// </summary>
		/// <param name="typeDef">The type whose members to order using their method's MetadataToken</param>
		/// <returns>A sequence of all members ordered by MetadataToken</returns>
		internal IEnumerable<IMember> GetMembersWithNativeOrdering(ITypeDefinition typeDef)
		{
			IMDTokenProvider GetOrderingHandle(IMember member)
			{
				// Note! Technically COM interfaces could define property getters and setters out of order or interleaved with other
				// methods, but C# doesn't support this so we can't define it that way.

				if (member is ICSharpCode.Decompiler.TypeSystem.IMethod)
					return member.MetadataToken;
				else if (member is IProperty property)
					return property.Getter?.MetadataToken ?? (IMDTokenProvider)property.Setter?.MetadataToken ?? property.MetadataToken;
				else if (member is IEvent @event)
					return @event.AddAccessor?.MetadataToken ?? @event.RemoveAccessor?.MetadataToken ?? (IMDTokenProvider)@event.InvokeAccessor?.MetadataToken ?? @event.MetadataToken;
				else
					return member.MetadataToken;
			}

			return typeDef.Fields.Concat<IMember>(typeDef.Properties).Concat(typeDef.Methods).Concat(typeDef.Events).OrderBy((member) => GetOrderingHandle(member).MDToken.Raw);
		}

		#endregion

		static TypeSystemAstBuilder CreateAstBuilder(DecompilerSettings settings)
		{
			var typeSystemAstBuilder = new TypeSystemAstBuilder();
			typeSystemAstBuilder.ShowAttributes = true;
			typeSystemAstBuilder.UsePrivateProtectedAccessibility = settings.IntroducePrivateProtectedAccessibility;
			typeSystemAstBuilder.SortAttributes = settings.SortCustomAttributes;
			typeSystemAstBuilder.AlwaysUseShortTypeNames = true;
			typeSystemAstBuilder.AddResolveResultAnnotations = true;
			typeSystemAstBuilder.UseNullableSpecifierForValueTypes = settings.LiftNullables;
			typeSystemAstBuilder.SupportInitAccessors = settings.InitAccessors;
			typeSystemAstBuilder.SupportRecordClasses = settings.RecordClasses;
			typeSystemAstBuilder.SupportRecordStructs = settings.RecordStructs;
			typeSystemAstBuilder.SupportUnsignedRightShift = settings.UnsignedRightShift;
			typeSystemAstBuilder.SupportOperatorChecked = settings.CheckedOperators;
			typeSystemAstBuilder.AlwaysUseGlobal = settings.AlwaysUseGlobal;
			typeSystemAstBuilder.SupportExtensionDeclarations = settings.ExtensionMembers;
			return typeSystemAstBuilder;
		}

		IDocumentationProvider CreateDefaultDocumentationProvider()
		{
			//TODO: replace this with proper code
			return new DummyDocumentationProvider();
		}

		DecompileRun CreateDecompileRun(HashSet<string> namespaces)
		{
			List<INamespace> resolvedNamespaces = new List<INamespace>();
			foreach (var ns in namespaces)
			{
				var resolvedNamespace = typeSystem.GetNamespaceByFullName(ns);
				if (resolvedNamespace != null)
				{
					resolvedNamespaces.Add(resolvedNamespace);
				}
			}

			UsingScope usingScope = new UsingScope(
				new CSharpTypeResolveContext(typeSystem.MainModule),
				typeSystem.RootNamespace,
				resolvedNamespaces.ToImmutableArray()
			);

			return new DecompileRun(new DecompilerContext(settings.SettingsVersion, metadata)
				{ Settings = settings, CancellationToken = CancellationToken }, usingScope) {
				DocumentationProvider = DocumentationProvider ?? CreateDefaultDocumentationProvider(),
				Namespaces = namespaces
			};
		}

		void RunTransforms(AstNode rootNode, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
		{
			var typeSystemAstBuilder = CreateAstBuilder(decompileRun.Settings);
			var context = new TransformContext(typeSystem, decompileRun, decompilationContext, typeSystemAstBuilder, new StringBuilder());
			foreach (var transform in astTransforms)
			{
				CancellationToken.ThrowIfCancellationRequested();
				transform.Run(rootNode, context);
			}
			CancellationToken.ThrowIfCancellationRequested();
			rootNode.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
			CancellationToken.ThrowIfCancellationRequested();
			GenericGrammarAmbiguityVisitor.ResolveAmbiguities(rootNode);
		}

		/// <summary>
		/// Decompile module attributes.
		/// </summary>
		public SyntaxTree DecompileModule()
		{
			var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
			var namespaces = new HashSet<string>();
			syntaxTree = new SyntaxTree();
			RequiredNamespaceCollector.CollectAttributeNamespacesOnlyModule(module, namespaces);
			DecompileRun decompileRun = CreateDecompileRun(namespaces);
			DoDecompileModuleAttributes(module.metadata, decompileRun, decompilationContext, syntaxTree);
			RunTransforms(syntaxTree, decompileRun, decompilationContext);
			return syntaxTree;
		}

		/// <summary>
		/// Decompile assembly attributes.
		/// </summary>
		public SyntaxTree DecompileAssembly()
		{
			var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
			var namespaces = new HashSet<string>();
			syntaxTree = new SyntaxTree();
			RequiredNamespaceCollector.CollectAttributeNamespacesOnlyAssembly(module, namespaces);
			DecompileRun decompileRun = CreateDecompileRun(namespaces);
			DoDecompileAssemblyAttributes(module.metadata.Assembly, decompileRun, decompilationContext, syntaxTree);
			RunTransforms(syntaxTree, decompileRun, decompilationContext);
			return syntaxTree;
		}

		void DoDecompileAssemblyAttributes(AssemblyDef assemblyDef, DecompileRun decompileRun, ITypeResolveContext decompilationContext, SyntaxTree syntaxTree)
		{
			try {
				var astBuilder = CreateAstBuilder(decompileRun.Settings);
				foreach (var a in typeSystem.MainModule.GetAssemblyAttributes()) {
					var attrSection = new AttributeSection(astBuilder.ConvertAttribute(a));
					attrSection.AttributeTarget = "assembly";
					syntaxTree.AddChild(attrSection, SyntaxTree.MemberRole);
				}
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(assemblyDef, innerException, "Error decompiling module and assembly attributes of " + module.AssemblyName);
			}
		}

		void DoDecompileModuleAttributes(ModuleDef moduleDef, DecompileRun decompileRun, ITypeResolveContext decompilationContext, SyntaxTree syntaxTree)
		{
			try {
				var astBuilder = CreateAstBuilder(decompileRun.Settings);
				foreach (var a in typeSystem.MainModule.GetModuleAttributes()) {
					var attrSection = new AttributeSection(astBuilder.ConvertAttribute(a));
					attrSection.AttributeTarget = "module";
					syntaxTree.AddChild(attrSection, SyntaxTree.MemberRole);
				}
			}
			catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException))
			{
				throw new DecompilerException(moduleDef, innerException, "Error decompiling module and assembly attributes of " + module.AssemblyName);
			}
		}

		void DoDecompileTypes(IEnumerable<TypeDef> types, DecompileRun decompileRun, ITypeResolveContext decompilationContext, SyntaxTree syntaxTree)
		{
			string currentNamespace = null;
			AstNode groupNode = null;
			foreach (var cecilType in types) {
				var typeDef = module.GetDefinition(cecilType);
				if (typeDef.Name == "<Module>" && typeDef.Members.Count == 0)
					continue;
				if (MemberIsHidden(cecilType, settings))
					continue;
				if (string.IsNullOrEmpty(cecilType.Namespace))
				{
					groupNode = syntaxTree;
				}
				else
				{
					if (currentNamespace != cecilType.Namespace)
					{
						groupNode = new NamespaceDeclaration(cecilType.Namespace);
						syntaxTree.AddChild(groupNode, SyntaxTree.MemberRole);
					}
				}
				currentNamespace = cecilType.Namespace;
				var typeDecl = DoDecompile(typeDef, decompileRun, decompilationContext.WithCurrentTypeDefinition(typeDef));
				groupNode.AddChild(typeDecl, SyntaxTree.MemberRole);
			}
		}

		/// <summary>
		/// Creates an <see cref="ILTransformContext"/> for the given <paramref name="function"/>.
		/// </summary>
		public ILTransformContext CreateILTransformContext(ILFunction function)
		{
			var namespaces = new HashSet<string>();
			RequiredNamespaceCollector.CollectNamespaces(function.Method, module, namespaces);
			var decompileRun = CreateDecompileRun(namespaces);
			return new ILTransformContext(function, typeSystem, settings) {
				CancellationToken = CancellationToken,
				DecompileRun = decompileRun
			};
		}

		/// <summary>
		/// Determines the "code-mappings" for a given TypeDef. See <see cref="CodeMappingInfo"/> for more information.
		/// </summary>
		public static CodeMappingInfo GetCodeMappingInfo(TypeDef declaringType)
		{
			var info = new CodeMappingInfo(declaringType);

			var connectedMethods = new Queue<MethodDef>();
			var processedMethods = new HashSet<MethodDef>();
			var processedNestedTypes = new HashSet<TypeDef>();
			foreach (var method in declaringType.Methods) {
				connectedMethods.Clear();
				processedMethods.Clear();
				processedNestedTypes.Clear();

				connectedMethods.Enqueue(method);

				while (connectedMethods.Count > 0) {
					var part = connectedMethods.Dequeue();
					if (!processedMethods.Add(part))
						continue;
					try
					{
						if (TryGetExtensionImplementation(part, out var impl))
						{
							connectedMethods.Enqueue(impl);
						}

						ReadCodeMappingInfo(info, method, part, connectedMethods, processedNestedTypes);
					}
					catch (BadImageFormatException)
					{
						// ignore invalid IL
					}
				}
			}

			return info;
		}

		private static void ReadCodeMappingInfo(CodeMappingInfo info, MethodDef parent, MethodDef part, Queue<MethodDef> connectedMethods, HashSet<TypeDef> processedNestedTypes)
		{
			if (part.HasBody) {
				var declaringType = parent.DeclaringType;
				for (int i = 0; i < part.Body.Instructions.Count; i++) {
					var instr = part.Body.Instructions[i];
					switch (instr.OpCode.Code) {
						case Code.Newobj:
						case Code.Stfld:
							// async and yield fsms:
							TypeDef fsmTypeDef;
							switch (instr.Operand) {
								case MethodDef fsmMethod: {
									fsmTypeDef = fsmMethod.DeclaringType;
									break;
								}
								case FieldDef fsmField: {
									fsmTypeDef = fsmField.DeclaringType;
									break;
								}
								case MemberRef memberRef: {
									fsmTypeDef = ExtractDeclaringType(memberRef);
									break;
								}
								default:
									continue;
							}

							if (fsmTypeDef != null) {
								// Must be a nested type of the containing type.
								if (fsmTypeDef.DeclaringType != declaringType)
									break;
								if (YieldReturnDecompiler.IsCompilerGeneratorEnumerator(fsmTypeDef)
									|| AsyncAwaitDecompiler.IsCompilerGeneratedStateMachine(fsmTypeDef)) {
									if (!processedNestedTypes.Add(fsmTypeDef))
										break;
									foreach (var h in fsmTypeDef.Methods) {
										if (h.SemanticsAttributes != 0)
											continue;
										if (!h.CustomAttributes.HasKnownAttribute(KnownAttribute.DebuggerHidden)) {
											connectedMethods.Enqueue(h);
										}
									}
								}
							}
							break;
						case Code.Ldftn:
							// deal with ldftn instructions, i.e., lambdas
							switch (instr.Operand) {
								case MethodDef def:
									if (def.IsCompilerGeneratedOrIsInCompilerGeneratedClass()) {
										connectedMethods.Enqueue(def);
									}
									continue;
								case MemberRef memberRef when memberRef.IsMethodRef:
									TypeDef closureType = ExtractDeclaringType(memberRef);
									if (closureType != null) {
										if (closureType != declaringType) {
											// Must be a nested type of the containing type.
											if (closureType.DeclaringType != declaringType)
												break;
											if (!processedNestedTypes.Add(closureType))
												break;
											foreach (var m in closureType.Methods) {
												connectedMethods.Enqueue(m);
											}
										} else {
											// Delegate body is declared in the same type
											foreach (var methodDef in closureType.Methods) {
												if (methodDef.Name == memberRef.Name && methodDef.IsCompilerGeneratedOrIsInCompilerGeneratedClass())
													connectedMethods.Enqueue(methodDef);
											}
										}
										break;
									}
									break;
								default:
									continue;
							}
							break;
						case Code.Call:
						case Code.Callvirt:
							// deal with call/callvirt instructions, i.e., local function invocations
							MethodDef method;
							switch (instr.Operand) {
								case MethodDef def:
									method = def;
									break;
								case MethodSpec spec when spec.Method is MethodDef specDef:
									method = specDef;
									break;
								default:
									continue;
							}
							if (LocalFunctionDecompiler.IsLocalFunctionMethod(null, method)) {
								connectedMethods.Enqueue(method);
							}
							break;
					}
				}
			}
			info.AddMapping(parent, part);

			TypeDef ExtractDeclaringType(MemberRef memberRef)
			{
				switch (memberRef.Class) {
					case TypeRef:
						// This should never happen in normal code, because we are looking at nested types
						// If it's not a nested type, it can't be a reference to the state machine or lambda anyway, and
						// those should be either TypeDef or TypeSpec.
						return null;
					case TypeDef defParent:
						return defParent;
					case TypeSpec ts when ts.TypeSig is GenericInstSig genericInstSig:
						return genericInstSig.GenericType.TypeDef;
				}
				return null;
			}
		}

		private static bool TryGetExtensionImplementation(MethodDef definitionPart, out MethodDef implementationPart)
		{
			implementationPart = default;

			var def = (definitionPart);
			var declTypeHandle = def.DeclaringType;
			var declType = (declTypeHandle);
			var name = (def.Name);
			var containerHandle = declType.DeclaringType;

			if (containerHandle is null)
				return false;

			if (declType.Name.StartsWith("<>E__") || declType.Name.StartsWith("<G>$"))
			{
				implementationPart = FindImplementations(containerHandle.Methods);
			}
			else if (declType.Name.StartsWith("<M>$"))
			{
				var container = (containerHandle);
				var groupHandle = container.DeclaringType;
				if (groupHandle is null)
					return false;

				implementationPart = FindImplementations(groupHandle.Methods);
			}
			else
			{
				return false;
			}

			return implementationPart is not null;

			MethodDef FindImplementations(IList<MethodDef> methods)
			{
				foreach (var m in methods)
				{
					if (!(m.Name == name))
						continue;

					// TODO : use SignatureBlobComparer to ensure that the correct method is resolved

					return m;
				}
				return default;
			}
		}

		/// <summary>
		/// Decompile the given types.
		/// </summary>
		/// <remarks>
		/// Unlike Decompile(IMemberDefinition[]), this method will add namespace declarations around the type definitions.
		/// </remarks>
		public SyntaxTree DecompileTypes(IEnumerable<TypeDef> types)
		{
			if (types == null)
				throw new ArgumentNullException(nameof(types));
			var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
			syntaxTree = new SyntaxTree();
			var namespaces = new HashSet<string>();
			foreach (var type in types)
			{
				CancellationToken.ThrowIfCancellationRequested();
				if (type is null)
					throw new ArgumentException("types contains null element");
				RequiredNamespaceCollector.CollectNamespaces(type, module, namespaces);
			}

			var decompileRun = CreateDecompileRun(namespaces);
			DoDecompileTypes(types, decompileRun, decompilationContext, syntaxTree);
			RunTransforms(syntaxTree, decompileRun, decompilationContext);
			return syntaxTree;
		}

		/// <summary>
		/// Decompile the specified types and/or members.
		/// </summary>
		public SyntaxTree Decompile(params IMemberDef[] definitions)
		{
			return Decompile((IEnumerable<IMemberDef>)definitions);
		}

		/// <summary>
		/// Decompile the specified types and/or members.
		/// </summary>
		public SyntaxTree Decompile(IEnumerable<IMemberDef> definitions)
		{
			if (definitions == null)
				throw new ArgumentNullException(nameof(definitions));
			syntaxTree = new SyntaxTree();
			var namespaces = new HashSet<string>();
			foreach (var entity in definitions)
			{
				if (entity is null)
					throw new ArgumentException("definitions contains null element");
				RequiredNamespaceCollector.CollectNamespaces(entity, module, namespaces);
			}
			var decompileRun = CreateDecompileRun(namespaces);

			bool first = true;
			ITypeDefinition parentTypeDef = null;
			ExtensionInfo parentExtensionInfo = null;
			foreach (var def in definitions) {
				switch (def) {
					case TypeDef typeDefinition:
						ITypeDefinition typeDef = module.GetDefinition(typeDefinition);
						syntaxTree.Members.Add(DoDecompile(typeDef, decompileRun, new SimpleTypeResolveContext(typeDef)));
						if (first) {
							parentTypeDef = typeDef.DeclaringTypeDefinition;
						} else if (parentTypeDef != null) {
							parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, typeDef.DeclaringTypeDefinition);
						}
						break;
					case MethodDef methodDefinition:
						Decompiler.TypeSystem.IMethod method = module.GetDefinition(methodDefinition);
						parentExtensionInfo = method.ResolveExtensionInfo();
						syntaxTree.Members.Add(DoDecompile(method, decompileRun, new SimpleTypeResolveContext(method), parentExtensionInfo));
						if (first) {
							parentTypeDef = method.DeclaringTypeDefinition;
						} else if (parentTypeDef != null) {
							parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, method.DeclaringTypeDefinition);
						}
						break;
					case FieldDef fieldDefinition:
						Decompiler.TypeSystem.IField field = module.GetDefinition(fieldDefinition);
						syntaxTree.Members.Add(DoDecompile(field, decompileRun, new SimpleTypeResolveContext(field)));
						parentTypeDef = field.DeclaringTypeDefinition;
						break;
					case PropertyDef propertyDefinition:
						IProperty property = module.GetDefinition(propertyDefinition);
						parentExtensionInfo = property.ResolveExtensionInfo();
						syntaxTree.Members.Add(DoDecompile(property, decompileRun, new SimpleTypeResolveContext(property), parentExtensionInfo));
						if (first) {
							parentTypeDef = property.DeclaringTypeDefinition;
						} else if (parentTypeDef != null) {
							parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, property.DeclaringTypeDefinition);
						}
						break;
					case EventDef eventDefinition:
						IEvent ev = module.GetDefinition(eventDefinition);
						syntaxTree.Members.Add(DoDecompile(ev, decompileRun, new SimpleTypeResolveContext(ev)));
						if (first) {
							parentTypeDef = ev.DeclaringTypeDefinition;
						} else if (parentTypeDef != null) {
							parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, ev.DeclaringTypeDefinition);
						}
						break;
					default:
						throw new NotSupportedException(def.GetType().Name);
				}
				first = false;
			}
			RunTransforms(syntaxTree, decompileRun, parentTypeDef != null ? new SimpleTypeResolveContext(parentTypeDef) : new SimpleTypeResolveContext(typeSystem.MainModule));
			return syntaxTree;
		}

		public SyntaxTree DecompileExtension(IMemberDef handle)
		{
			if (handle is null)
				throw new ArgumentNullException(nameof(handle));
			syntaxTree = new SyntaxTree();
			var namespaces = new HashSet<string>();
			RequiredNamespaceCollector.CollectNamespaces(handle, module, namespaces);
			var decompileRun = CreateDecompileRun(namespaces);

			switch (handle)
			{
				case TypeDef td:
					ITypeDefinition typeDef = module.GetDefinition(td);
					syntaxTree.Members.Add(DoDecompile(typeDef, decompileRun, new SimpleTypeResolveContext(typeDef), asExtension: true));
					RunTransforms(syntaxTree, decompileRun, new SimpleTypeResolveContext(typeDef));
					break;
				case MethodDef md:
					Decompiler.TypeSystem.IMethod methodDef = module.GetDefinition(md);
					var extensionInfo = methodDef.ResolveExtensionInfo();
					Debug.Assert(extensionInfo != null);
					var memberInfo = extensionInfo.InfoOfExtensionMember((Decompiler.TypeSystem.IMethod)methodDef.MemberDefinition).GetValueOrDefault();
					var subst = new TypeParameterSubstitution(memberInfo.ExtensionGroupingTypeParameters, null);
					methodDef = methodDef.Specialize(subst);
					EntityDeclaration entity = DoDecompile(methodDef, decompileRun, new SimpleTypeResolveContext(methodDef), extensionInfo);
					syntaxTree.Members.Add(entity);
					RemoveAttribute(entity, KnownAttribute.ExtensionMarker);
					RunTransforms(syntaxTree, decompileRun, new SimpleTypeResolveContext(methodDef.DeclaringTypeDefinition));
					break;
				case PropertyDef pd:
					IProperty propDef = module.GetDefinition(pd);
					extensionInfo = propDef.ResolveExtensionInfo();
					Debug.Assert(extensionInfo != null);
					var accessor = propDef.Getter ?? propDef.Setter;
					memberInfo = extensionInfo.InfoOfExtensionMember((Decompiler.TypeSystem.IMethod)accessor.MemberDefinition).GetValueOrDefault();
					subst = new TypeParameterSubstitution(memberInfo.ExtensionGroupingTypeParameters, null);
					propDef = (IProperty)propDef.Specialize(subst);
					EntityDeclaration prop = DoDecompile(propDef, decompileRun, new SimpleTypeResolveContext(propDef), extensionInfo);
					syntaxTree.Members.Add(prop);
					RemoveAttribute(prop, KnownAttribute.ExtensionMarker);
					if (propDef.Getter != null)
					{
						RemoveAttribute(prop.GetChildByRole(PropertyDeclaration.GetterRole), KnownAttribute.ExtensionMarker);
					}
					if (propDef.Setter != null)
					{
						RemoveAttribute(prop.GetChildByRole(PropertyDeclaration.SetterRole), KnownAttribute.ExtensionMarker);
					}
					RunTransforms(syntaxTree, decompileRun, new SimpleTypeResolveContext(propDef.DeclaringTypeDefinition));
					break;
				default:
					throw new NotSupportedException($"HandleKind {handle.GetType()} is not supported!");
			}
			return syntaxTree;
		}

		ITypeDefinition FindCommonDeclaringTypeDefinition(ITypeDefinition a, ITypeDefinition b)
		{
			if (a == null || b == null)
				return null;
			var declaringTypes = a.GetDeclaringTypeDefinitions();
			var set = new HashSet<ITypeDefinition>(b.GetDeclaringTypeDefinitions());
			return declaringTypes.FirstOrDefault(set.Contains);
		}

		readonly Dictionary<TypeDef, PartialTypeInfo> partialTypes = new();

		public void AddPartialTypeDefinition(PartialTypeInfo info)
		{
			if (!partialTypes.TryGetValue(info.DeclaringTypeDefinitionHandle, out var existingInfo))
			{
				partialTypes.Add(info.DeclaringTypeDefinitionHandle, info);
			}
			else
			{
				existingInfo.AddDeclaredMembers(info);
			}
		}

		internal static IEnumerable<EntityDeclaration> AddInterfaceImplHelpers(MetadataModule module, EntityDeclaration memberDecl,
				ICSharpCode.Decompiler.TypeSystem.IMethod method,
				TypeSystemAstBuilder astBuilder)
		{
			if (!memberDecl.GetChildByRole(EntityDeclaration.PrivateImplementationTypeRole).IsNull)
			{
				yield break; // cannot create forwarder for existing explicit interface impl
			}
			if (method.IsStatic)
			{
				yield break; // cannot create forwarder for static interface impl
			}
			if (memberDecl.HasModifier(Modifiers.Extern))
			{
				yield break; // cannot create forwarder for extern method
			}
			var genericContext = new Decompiler.TypeSystem.GenericContext(method);
			var methodHandle = (MethodDef)method.MetadataToken;
			foreach (var h in methodHandle.Overrides) {
				ICSharpCode.Decompiler.TypeSystem.IMethod m = module.ResolveMethod(h.MethodDeclaration, genericContext);
				if (m == null || m.DeclaringType.Kind != TypeKind.Interface)
					continue;
				var methodDecl = new MethodDeclaration();
				methodDecl.ReturnType = memberDecl.ReturnType.Clone();
				methodDecl.PrivateImplementationType = astBuilder.ConvertType(m.DeclaringType);
				methodDecl.Name = m.Name;
				methodDecl.TypeParameters.AddRange(memberDecl.GetChildrenByRole(Roles.TypeParameter)
												   .Select(n => (TypeParameterDeclaration)n.Clone()));
				methodDecl.Parameters.AddRange(memberDecl.GetChildrenByRole(Roles.Parameter).Select(n => n.Clone()));
				// Constraints are not copied because explicit interface implementations cannot have constraints. CS0460

				methodDecl.Body = new BlockStatement();
				methodDecl.Body.AddChild(new Comment(
					"ILSpy generated this explicit interface implementation from .override directive in " + memberDecl.Name),
					Roles.Comment);

				var member = new MemberReferenceExpression {
					Target = new ThisReferenceExpression().WithAnnotation(methodHandle.DeclaringType),
					MemberNameToken = Identifier.Create(memberDecl.Name).WithAnnotation(method.OriginalMember)
				}.WithAnnotation(method.OriginalMember);
				member.TypeArguments.AddRange(methodDecl.TypeParameters.Select(tp => new SimpleType(tp.Name)));

				var forwardingCall = new InvocationExpression(member,
					methodDecl.Parameters.Select(ForwardParameter)
				).WithAnnotation(method.OriginalMember);
				if (m.ReturnType.IsKnownType(KnownTypeCode.Void))
				{
					methodDecl.Body.Add(new ExpressionStatement(forwardingCall));
				}
				else
				{
					methodDecl.Body.Add(new ReturnStatement(forwardingCall));
				}
				yield return methodDecl;
			}
		}

		internal static Expression ForwardParameter(ParameterDeclaration p)
		{
			switch (p.ParameterModifier)
			{
				case ReferenceKind.None:
					return new IdentifierExpression(p.Name);
				case ReferenceKind.Ref:
				case ReferenceKind.RefReadOnly:
					return new DirectionExpression(FieldDirection.Ref, new IdentifierExpression(p.Name));
				case ReferenceKind.Out:
					return new DirectionExpression(FieldDirection.Out, new IdentifierExpression(p.Name));
				case ReferenceKind.In:
					return new DirectionExpression(FieldDirection.In, new IdentifierExpression(p.Name));
				default:
					throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Sets new modifier if the member hides some other member from a base type.
		/// </summary>
		/// <param name="member">The node of the member which new modifier state should be determined.</param>
		internal static void SetNewModifier(EntityDeclaration member)
		{
			if (member is ExtensionDeclaration)
				return;

			var entity = (IEntity)member.GetSymbol();
			var lookup = new MemberLookup(entity.DeclaringTypeDefinition, entity.ParentModule);

			var baseTypes = entity.DeclaringType.GetNonInterfaceBaseTypes().Where(t => entity.DeclaringType != t).ToList();

			// A constant, field, property, event, or type introduced in a class or struct hides all base class members with the same name.
			bool hideBasedOnSignature = !(entity is ITypeDefinition
				|| entity.SymbolKind == SymbolKind.Field
				|| entity.SymbolKind == SymbolKind.Property
				|| entity.SymbolKind == SymbolKind.Event);

			const GetMemberOptions options = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

			if (HidesMemberOrTypeOfBaseType())
				member.Modifiers |= Modifiers.New;

			bool HidesMemberOrTypeOfBaseType()
			{
				var parameterListComparer = ParameterListComparer.WithOptions(includeModifiers: true);

				foreach (Decompiler.TypeSystem.IType baseType in baseTypes) {
					if (!hideBasedOnSignature) {
						if (baseType.GetNestedTypes(t => t.Name == entity.Name && lookup.IsAccessible(t, true), options).Any())
							return true;
						if (baseType.GetMembers(m => m.Name == entity.Name && m.SymbolKind != SymbolKind.Indexer && lookup.IsAccessible(m, true), options).Any())
							return true;
					} else {
						if (entity.SymbolKind == SymbolKind.Indexer) {
							// An indexer introduced in a class or struct hides all base class indexers with the same signature (parameter count and types).
							if (baseType.GetProperties(p => p.SymbolKind == SymbolKind.Indexer && lookup.IsAccessible(p, true))
									.Any(p => parameterListComparer.Equals(((IProperty)entity).Parameters, p.Parameters)))
							{
								return true;
							}
						} else if (entity.SymbolKind == SymbolKind.Method) {
							// A method introduced in a class or struct hides all non-method base class members with the same name, and all
							// base class methods with the same signature (method name and parameter count, modifiers, and types).
							if (baseType.GetMembers(m => m.SymbolKind != SymbolKind.Indexer
														 && m.SymbolKind != SymbolKind.Constructor
														 && m.SymbolKind != SymbolKind.Destructor
														 && m.Name == entity.Name && lookup.IsAccessible(m, true))
										.Any(m => m.SymbolKind != SymbolKind.Method ||
												  (((ICSharpCode.Decompiler.TypeSystem.IMethod)entity).TypeParameters.Count == ((ICSharpCode.Decompiler.TypeSystem.IMethod)m).TypeParameters.Count
												   && parameterListComparer.Equals(((ICSharpCode.Decompiler.TypeSystem.IMethod)entity).Parameters, ((ICSharpCode.Decompiler.TypeSystem.IMethod)m).Parameters))))
							{
								return true;
							}
						}
					}
				}

				return false;
			}
		}

		internal static void FixParameterNames(EntityDeclaration entity)
		{
			int i = 0;
			foreach (var parameter in entity.GetChildrenByRole(Roles.Parameter))
			{
				if (string.IsNullOrWhiteSpace(parameter.Name) && !parameter.Type.IsArgList())
				{
					// needs to be consistent with logic in ILReader.CreateILVariable
					parameter.Name = "P_" + i;
				}
				i++;
			}
		}

		EntityDeclaration DoDecompile(ITypeDefinition typeDef, DecompileRun decompileRun, ITypeResolveContext decompilationContext, bool asExtension = false)
		{
			Debug.Assert(decompilationContext.CurrentTypeDefinition == typeDef);
			var entityMap = new MultiDictionary<IEntity, EntityDeclaration>();
			var workList = new Queue<IEntity>();
			TypeSystemAstBuilder typeSystemAstBuilder;
			try
			{
				typeSystemAstBuilder = CreateAstBuilder(decompileRun.Settings);
				EntityDeclaration entityDecl;
				if (asExtension)
				{
					var extensionInfo = typeDef.DeclaringTypeDefinition?.ExtensionInfo ?? typeDef.DeclaringTypeDefinition?.DeclaringTypeDefinition?.ExtensionInfo;
					Debug.Assert(extensionInfo != null);
					extensionInfo.IsExtensionMarkerType(typeDef, out var extensionGroup);
					entityDecl = typeSystemAstBuilder.ConvertExtension(extensionGroup);
				}
				else
				{
					entityDecl = typeSystemAstBuilder.ConvertEntity(typeDef);
				}

				if (entityDecl is DelegateDeclaration delegateDeclaration)
				{
					// Fix empty parameter names in delegate declarations
					FixParameterNames(delegateDeclaration);
				}

				if (entityDecl is not TypeDeclaration typeDecl)
				{
					if (entityDecl is ExtensionDeclaration ext && settings.ExtensionMembers)
					{
						var extensionInfo = typeDef.DeclaringTypeDefinition.ExtensionInfo ?? typeDef.DeclaringTypeDefinition.DeclaringTypeDefinition.ExtensionInfo;
						extensionInfo.IsExtensionMarkerType(typeDef, out var group);
						DoDecompileExtensionMembers(ext, group.Marker, extensionInfo);
					}
					// e.g. DelegateDeclaration
					return entityDecl;
				}
				bool isRecord = typeDef.Kind switch {
					TypeKind.Class => settings.RecordClasses && typeDef.IsRecord,
					TypeKind.Struct => settings.RecordStructs && typeDef.IsRecord,
					_ => false,
				};
				RecordDecompiler recordDecompiler = isRecord ? new RecordDecompiler(typeSystem, typeDef, settings, CancellationToken) : null;
				if (recordDecompiler != null)
					decompileRun.RecordDecompilers.Add(typeDef, recordDecompiler);

				// With C# 9 records, the relative order of fields and properties matters:
				IEnumerable<IMember> fieldsAndProperties = isRecord
					? recordDecompiler.FieldsAndProperties
					: typeDef.Fields.Concat<IMember>(typeDef.Properties);

				// For COM interop scenarios, the relative order of virtual functions/properties matters:
				IEnumerable<IMember> allOrderedMembers = RequiresNativeOrdering(typeDef) ? GetMembersWithNativeOrdering(typeDef) :
					fieldsAndProperties.Concat(typeDef.Events).Concat(typeDef.Methods);

				var allOrderedEntities = typeDef.NestedTypes.Concat<IEntity>(allOrderedMembers).ToArray();

				if (!partialTypes.TryGetValue((TypeDef)typeDef.MetadataToken, out var partialTypeInfo))
				{
					partialTypeInfo = null;
				}

				if (settings.ExtensionMembers)
				{
					foreach (var group in typeDef.ExtensionInfo?.ExtensionGroups ?? [])
					{
						var ext = (ExtensionDeclaration)typeSystemAstBuilder.ConvertExtension(group);
						DoDecompileExtensionMembers(ext, group.Marker, typeDef.ExtensionInfo);

						typeDecl.Members.Add(ext);
					}
				}

				// Decompile members that are not compiler-generated.
				foreach (var entity in allOrderedEntities)
				{
					if (entity.MetadataToken is null || MemberIsHidden(entity.MetadataToken, settings))
					{
						continue;
					}
					DoDecompileMember(entity, recordDecompiler, partialTypeInfo, typeDef.ExtensionInfo);
				}

				// Decompile compiler-generated members that are still needed.
				while (workList.Count > 0)
				{
					var entity = workList.Dequeue();
					if (entityMap.Contains(entity) || entity.MetadataToken is null)
					{
						// Member is already decompiled.
						continue;
					}
					DoDecompileMember(entity, recordDecompiler, partialTypeInfo, typeDef.ExtensionInfo);
				}

				// Add all decompiled members to syntax tree in the correct order.
				foreach (var member in allOrderedEntities)
				{
					typeDecl.Members.AddRange(entityMap[member]);
				}

				if (typeDecl.Members.OfType<IndexerDeclaration>().Any(idx => idx.PrivateImplementationType.IsNull))
				{
					// Remove the [DefaultMember] attribute if the class contains indexers
					RemoveAttribute(typeDecl, KnownAttribute.DefaultMember);
				}
				if (partialTypeInfo != null)
				{
					typeDecl.Modifiers |= Modifiers.Partial;
				}
				if (settings.IntroduceRefModifiersOnStructs)
				{
					RemoveObsoleteAttribute(typeDecl, "Types with embedded references are not supported in this version of your compiler.");
					RemoveCompilerFeatureRequiredAttribute(typeDecl, "RefStructs");
				}
				if (settings.RequiredMembers)
				{
					RemoveAttribute(typeDecl, KnownAttribute.Required);
				}
				if (typeDecl.ClassType == ClassType.Enum)
				{
					Debug.Assert(typeDef.Kind == TypeKind.Enum);
					EnumValueDisplayMode displayMode = DetectBestEnumValueDisplayMode(typeDef);
					switch (displayMode)
					{
						case EnumValueDisplayMode.FirstOnly:
							foreach (var enumMember in typeDecl.Members.OfType<EnumMemberDeclaration>().Skip(1)) {
								enumMember.Initializer = null;
							}
							break;
						case EnumValueDisplayMode.None:
							foreach (var enumMember in typeDecl.Members.OfType<EnumMemberDeclaration>()) {
								enumMember.Initializer = null;
								if (enumMember.GetSymbol() is ICSharpCode.Decompiler.TypeSystem.IField f && f.GetConstantValue() == null) {
									typeDecl.InsertChildBefore(enumMember, new Comment(" error: enumerator has no value"), Roles.Comment);
								}
							}
							break;
						case EnumValueDisplayMode.All:
							// nothing needs to be changed.
							break;
						case EnumValueDisplayMode.AllHex:
							foreach (var enumMember in typeDecl.Members.OfType<EnumMemberDeclaration>())
							{
								var constantValue = (enumMember.GetSymbol() as ICSharpCode.Decompiler.TypeSystem.IField)?.GetConstantValue();
								if (constantValue == null || enumMember.Initializer is not PrimitiveExpression pe)
								{
									continue;
								}
								long initValue = (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, constantValue, false);
								if (initValue >= 10)
								{
									pe.Format = LiteralFormat.HexadecimalNumber;
								}
							}
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					foreach (var item in typeDecl.Members)
					{
						if (item is not EnumMemberDeclaration)
						{
							typeDecl.InsertChildBefore(item, new Comment(" error: nested types are not permitted in C#."), Roles.Comment);
						}
					}
				}
				return typeDecl;
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(typeDef.MetadataToken, innerException);
			}

			void DoDecompileMember(IEntity entity, RecordDecompiler recordDecompiler, PartialTypeInfo partialType, ExtensionInfo extensionInfo)
			{
				if (partialType != null && partialType.IsDeclaredMember(entity.MetadataToken))
				{
					return;
				}

				if (settings.ExtensionMembers && extensionInfo != null)
				{
					switch (entity)
					{
						case ITypeDefinition td when extensionInfo.IsExtensionGroupType(td) || extensionInfo.IsExtensionMarkerType(td, out _):
							return;
						case ICSharpCode.Decompiler.TypeSystem.IMethod m when extensionInfo.InfoOfImplementationMember(m).HasValue:
							return;
					}
				}

				EntityDeclaration entityDecl;
				switch (entity)
				{
					case ICSharpCode.Decompiler.TypeSystem.IField field:
						if (typeDef.Kind == TypeKind.Enum && !field.IsConst)
						{
							return;
						}
						if (TransformFieldAndConstructorInitializers.IsGeneratedPrimaryConstructorBackingField(field))
						{
							return;
						}
						entityDecl = DoDecompile(field, decompileRun, decompilationContext.WithCurrentMember(field));
						entityMap.Add(field, entityDecl);
						break;
					case IProperty property:
						if (recordDecompiler?.PropertyIsGenerated(property) == true)
						{
							return;
						}
						entityDecl = DoDecompile(property, decompileRun, decompilationContext.WithCurrentMember(property), null);
						entityMap.Add(property, entityDecl);
						break;
					case ICSharpCode.Decompiler.TypeSystem.IMethod method:
						if (recordDecompiler?.MethodIsGenerated(method) == true)
						{
							return;
						}
						entityDecl = DoDecompile(method, decompileRun, decompilationContext.WithCurrentMember(method), null);
						entityMap.Add(method, entityDecl);
						foreach (var helper in AddInterfaceImplHelpers(module, entityDecl, method, typeSystemAstBuilder))
						{
							entityMap.Add(method, helper);
						}
						break;
					case IEvent @event:
						entityDecl = DoDecompile(@event, decompileRun, decompilationContext.WithCurrentMember(@event));
						entityMap.Add(@event, entityDecl);
						break;
					case ITypeDefinition type:
						entityDecl = DoDecompile(type, decompileRun, decompilationContext.WithCurrentTypeDefinition(type));
						SetNewModifier(entityDecl);
						entityMap.Add(type, entityDecl);
						break;
					default:
						throw new ArgumentOutOfRangeException("Unexpected member type");
				}

				foreach (var node in entityDecl.Descendants)
				{
					var rr = node.GetResolveResult();
					if (rr is MemberResolveResult mrr
						&& mrr.Member.DeclaringTypeDefinition == typeDef
						&& !(mrr.Member is ICSharpCode.Decompiler.TypeSystem.IMethod { IsLocalFunction: true }))
					{
						workList.Enqueue(mrr.Member);
					}
					else if (rr is TypeResolveResult trr
						&& trr.Type.GetDefinition()?.DeclaringTypeDefinition == typeDef)
					{
						workList.Enqueue(trr.Type.GetDefinition());
					}
				}
			}

			void DoDecompileExtensionMembers(ExtensionDeclaration ext, ICSharpCode.Decompiler.TypeSystem.IMethod marker, ExtensionInfo extensionInfo)
			{
				foreach (var member in extensionInfo.GetMembersOfGroup(marker))
				{
					var extMember = member;
					if (entityMap.Contains(extMember) || extMember.MetadataToken is null)
					{
						// Member is already decompiled.
						continue;
					}
					EntityDeclaration extMemberDecl;
					switch (extMember)
					{
						case IProperty p:
							var prop = DoDecompile(p, decompileRun, decompilationContext.WithCurrentMember(p), extensionInfo);
							RemoveAttribute(prop, KnownAttribute.ExtensionMarker);
							if (p.Getter != null)
							{
								RemoveAttribute(prop.GetChildByRole(PropertyDeclaration.GetterRole), KnownAttribute.ExtensionMarker);
							}
							if (p.Setter != null)
							{
								RemoveAttribute(prop.GetChildByRole(PropertyDeclaration.SetterRole), KnownAttribute.ExtensionMarker);
							}
							extMemberDecl = prop;
							break;
						case Decompiler.TypeSystem.IMethod m:
							var meth = DoDecompile(m, decompileRun, decompilationContext.WithCurrentMember(m), extensionInfo);
							RemoveAttribute(meth, KnownAttribute.ExtensionMarker);
							extMemberDecl = meth;
							break;
						default:
							throw new NotSupportedException($"Extension member {extMember} is not supported for decompilation.");
					}
					ext.Members.Add(extMemberDecl);
					entityMap.Add(extMember, extMemberDecl);
				}
			}
		}

		EnumValueDisplayMode DetectBestEnumValueDisplayMode(ITypeDefinition typeDef)
		{
			if (typeDef.HasAttribute(KnownAttribute.Flags))
				return EnumValueDisplayMode.AllHex;
			bool first = true;
			long firstValue = 0, previousValue = 0;
			bool allPowersOfTwo = true;
			bool allConsecutive = true;
			foreach (var field in typeDef.Fields)
			{
				if (MemberIsHidden(field.MetadataToken, settings))
					continue;
				object constantValue = field.GetConstantValue();
				if (constantValue == null)
					continue;
				long currentValue = (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, constantValue, false);
				allConsecutive = allConsecutive && (first || previousValue + 1 == currentValue);
				// N & (N - 1) == 0, iff N is a power of 2, for all N != 0.
				// We define that 0 is a power of 2 in the context of enum values.
				allPowersOfTwo = allPowersOfTwo && unchecked(currentValue & (currentValue - 1)) == 0;
				if (first)
				{
					firstValue = currentValue;
					first = false;
				}
				else if (currentValue <= previousValue)
				{
					// If the values are out of order, we fallback to displaying all values.
					return EnumValueDisplayMode.All;
				}
				else if (!allConsecutive && !allPowersOfTwo)
				{
					// We already know that the values are neither consecutive nor all powers of 2,
					// so we can abort, and just display all values as-is.
					return EnumValueDisplayMode.All;
				}
				previousValue = currentValue;
			}
			if (allPowersOfTwo)
			{
				if (previousValue > 8)
				{
					// If all values are powers of 2 and greater 8, display all enum values, but use hex.
					return EnumValueDisplayMode.AllHex;
				}
				else if (!allConsecutive)
				{
					// If all values are powers of 2, display all enum values.
					return EnumValueDisplayMode.All;
				}
			}
			if (settings.AlwaysShowEnumMemberValues)
			{
				// The user always wants to see all enum values, but we know hex is not necessary.
				return EnumValueDisplayMode.All;
			}
			// We know that all values are consecutive, so if the first value is not 0
			// display the first enum value only.
			return firstValue == 0 ? EnumValueDisplayMode.None : EnumValueDisplayMode.FirstOnly;
		}

		EntityDeclaration DoDecompile(Decompiler.TypeSystem.IMethod method, DecompileRun decompileRun, ITypeResolveContext decompilationContext, ExtensionInfo extensionInfo)
		{
			Debug.Assert(decompilationContext.CurrentMember == method);
			try
			{
				var typeSystemAstBuilder = CreateAstBuilder(decompileRun.Settings);
				var methodDecl = typeSystemAstBuilder.ConvertEntity(method);
				int lastDot = method.Name.LastIndexOf('.');
				if (methodDecl is not OperatorDeclaration && method.IsExplicitInterfaceImplementation && lastDot >= 0)
				{
					methodDecl.NameToken.Name = method.Name.Substring(lastDot + 1);
				}
				FixParameterNames(methodDecl);
				var methodDefinition = (MethodDef)method.MetadataToken;
				if (!settings.LocalFunctions && LocalFunctionDecompiler.LocalFunctionNeedsAccessibilityChange(method.ParentModule.MetadataFile, methodDefinition))
				{
					// if local functions are not active and we're dealing with a local function,
					// reduce the visibility of the method to private,
					// otherwise this leads to compile errors because the display classes have lesser accessibility.
					// Note: removing and then adding the static modifier again is necessary to set the private modifier before all other modifiers.
					methodDecl.Modifiers &= ~(Modifiers.Internal | Modifiers.Static);
					methodDecl.Modifiers |= Modifiers.Private | (method.IsStatic ? Modifiers.Static : 0);
				}
				if (methodDefinition.HasBody)
				{
					DecompileBody(method, methodDecl, decompileRun, decompilationContext, extensionInfo);
				}
				else if (!method.IsAbstract && method.DeclaringType.Kind != TypeKind.Interface)
				{
					methodDecl.Modifiers |= Modifiers.Extern;
				}
				if (method.SymbolKind == SymbolKind.Method && !method.IsExplicitInterfaceImplementation
					&& methodDefinition.IsVirtual == methodDefinition.IsNewSlot)
				{
					SetNewModifier(methodDecl);
				}
				else if (!method.IsStatic && !method.IsExplicitInterfaceImplementation
					&& !method.IsVirtual && method.IsOverride
					&& InheritanceHelper.GetBaseMember(method) == null && IsTypeHierarchyKnown(method.DeclaringType))
				{
					methodDecl.Modifiers &= ~Modifiers.Override;
					if (!method.DeclaringTypeDefinition.IsSealed)
					{
						methodDecl.Modifiers |= Modifiers.Virtual;
					}
				}
				if (IsCovariantReturnOverride(method))
				{
					RemoveAttribute(methodDecl, KnownAttribute.PreserveBaseOverrides);
					methodDecl.Modifiers &= ~(Modifiers.New | Modifiers.Virtual);
					methodDecl.Modifiers |= Modifiers.Override;
				}
				if (method.IsConstructor && settings.RequiredMembers && RemoveCompilerFeatureRequiredAttribute(methodDecl, "RequiredMembers"))
				{
					RemoveObsoleteAttribute(methodDecl, "Constructors of types with required members are not supported in this version of your compiler.");
				}
				return methodDecl;

				bool IsTypeHierarchyKnown(ICSharpCode.Decompiler.TypeSystem.IType type)
				{
					var definition = type.GetDefinition();
					if (definition == null)
					{
						return false;
					}

					if (decompileRun.TypeHierarchyIsKnown.TryGetValue(definition, out var value))
						return value;
					value = method.DeclaringType.GetNonInterfaceBaseTypes().All(t => t.Kind != TypeKind.Unknown);
					decompileRun.TypeHierarchyIsKnown.Add(definition, value);
					return value;
				}
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(method.MetadataToken, innerException);
			}
		}

		private bool IsCovariantReturnOverride(IEntity entity)
		{
			if (!settings.CovariantReturns)
				return false;
			if (!entity.HasAttribute(KnownAttribute.PreserveBaseOverrides))
				return false;
			return true;
		}

		internal static bool IsWindowsFormsInitializeComponentMethod(ICSharpCode.Decompiler.TypeSystem.IMethod method)
		{
			return method.ReturnType.Kind == TypeKind.Void && method.Name == "InitializeComponent" && method.DeclaringTypeDefinition.GetNonInterfaceBaseTypes().Any(t => t.FullName == "System.Windows.Forms.Control");
		}

		void DecompileBody(Decompiler.TypeSystem.IMethod method, EntityDeclaration entityDecl, DecompileRun decompileRun, ITypeResolveContext decompilationContext, ExtensionInfo extensionInfo)
		{
			try {
				var ilReader = new ILReader(typeSystem.MainModule) {
					UseDebugSymbols = settings.UseDebugSymbols,
					UseRefLocalsForAccurateOrderOfEvaluation = settings.UseRefLocalsForAccurateOrderOfEvaluation,
				};

				int parameterOffset = 0;
				if (extensionInfo != null)
				{
					if (!method.IsStatic)
						parameterOffset = 1; // implementation method has an additional receiver parameter
					method = extensionInfo.InfoOfExtensionMember((Decompiler.TypeSystem.IMethod)method.MemberDefinition).Value.ImplementationMethod;
				}

				var body = BlockStatement.Null;
				var methodDefinition = (MethodDef)method.MetadataToken;
				var function = ilReader.ReadIL(methodDefinition, cancellationToken: CancellationToken);
				function.CheckInvariant(ILPhase.Normal);

				AddAnnotationsToDeclaration(method, entityDecl, function, parameterOffset);

				var localSettings = settings.Clone();
				if (IsWindowsFormsInitializeComponentMethod(method))
				{
					localSettings.UseImplicitMethodGroupConversion = false;
					localSettings.UsingDeclarations = false;
					localSettings.AlwaysCastTargetsOfExplicitInterfaceImplementationCalls = true;
					localSettings.NamedArguments = false;
				}

				var context = new ILTransformContext(function, typeSystem, localSettings) {
					CancellationToken = CancellationToken,
					DecompileRun = decompileRun
				};
				foreach (var transform in ilTransforms)
				{
					CancellationToken.ThrowIfCancellationRequested();
					transform.Run(function, context);
					function.CheckInvariant(ILPhase.Normal);
					// When decompiling definitions only, we can cancel decompilation of all steps
					// after yield and async detection, because only those are needed to properly set
					// IsAsync/IsIterator flags on ILFunction.
					if (!localSettings.DecompileMemberBodies && transform is AsyncAwaitDecompiler)
						break;
				}

				// Generate C# AST only if bodies should be displayed.
				if (localSettings.DecompileMemberBodies) {
					AddDefinesForConditionalAttributes(function, decompileRun);
					var statementBuilder = new StatementBuilder(
						typeSystem,
						decompilationContext,
						function,
						localSettings,
						decompileRun,
						new StringBuilder(),
						CancellationToken
					);
					body = statementBuilder.ConvertAsBlock(function.Body);

					AssignSourceLocals(function);

					Comment prev = null;
					foreach (string warning in function.Warnings)
					{
						body.InsertChildAfter(prev, prev = new Comment(warning), Roles.Comment);
					}

					entityDecl.AddChild(body, Roles.Body);
				}

				CleanUpMethodDeclaration(entityDecl, body, function, localSettings.DecompileMemberBodies);
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(method.MetadataToken, innerException);
			}
		}

		internal static void AddAnnotationsToDeclaration(ICSharpCode.Decompiler.TypeSystem.IMethod method, EntityDeclaration entityDecl, ILFunction function, int parameterOffset = 0)
		{
			int i = parameterOffset;
			var parameters = function.Variables.Where(v => v.Kind == VariableKind.Parameter).ToDictionary(v => v.Index);
			foreach (var parameter in entityDecl.GetChildrenByRole(Roles.Parameter))
			{
				if (parameters.TryGetValue(i, out var v))
					parameter.AddAnnotation(new ILVariableResolveResult(v, method.Parameters[i].Type));
				i++;
			}
			entityDecl.AddAnnotation(function);
		}

		internal static void CleanUpMethodDeclaration(EntityDeclaration entityDecl, BlockStatement body, ILFunction function, bool decompileBody = true)
		{
			if (function.IsIterator) {
				if (decompileBody && !body.Descendants.Any(d => d is YieldReturnStatement || d is YieldBreakStatement)) {
					body.Add(new YieldBreakStatement());
				}
				if (function.IsAsync) {
					RemoveAttribute(entityDecl, KnownAttribute.AsyncIteratorStateMachine);
				} else {
					RemoveAttribute(entityDecl, KnownAttribute.IteratorStateMachine);
				}
				if (function.StateMachineCompiledWithMono) {
					RemoveAttribute(entityDecl, KnownAttribute.DebuggerHidden);
				}
				if (function.StateMachineCompiledWithLegacyVisualBasic)
				{
					RemoveAttribute(entityDecl, KnownAttribute.DebuggerStepThrough);
					if (function.Method?.IsAccessor == true && entityDecl.Parent is EntityDeclaration parentDecl)
					{
						RemoveAttribute(parentDecl, KnownAttribute.DebuggerStepThrough);
					}
				}
			}
			if (function.IsAsync) {
				entityDecl.Modifiers |= Modifiers.Async;
				RemoveAttribute(entityDecl, KnownAttribute.AsyncStateMachine);
				RemoveAttribute(entityDecl, KnownAttribute.DebuggerStepThrough);
			}
		}

		private static void AssignSourceLocals(ILFunction function)
		{
			foreach (ILFunction ilFunction in function.Descendants.OfType<ILFunction>()) {
				var dict = new Dictionary<Local, SourceLocal>();
				foreach (ILVariable variable in ilFunction.Variables) {
					if (variable.OriginalVariable is null) {
						if (variable.OriginalParameter is null) {
							Debug.Assert(variable.Type is not null);
						}
						continue;
					}

					if (dict.TryGetValue(variable.OriginalVariable, out var existing)) {
						variable.sourceParamOrLocal = existing;
					} else {
						dict[variable.OriginalVariable] = variable.GetSourceLocal();
					}
				}
			}
		}

		internal static bool RemoveAttribute(EntityDeclaration entityDecl, KnownAttribute attributeType)
		{
			bool found = false;
			foreach (var section in entityDecl.Attributes)
			{
				foreach (var attr in section.Attributes)
				{
					var symbol = attr.Type.GetSymbol();
					if (symbol is ITypeDefinition td && td.FullTypeName == attributeType.GetTypeName()) {
						attr.Remove();
						found = true;
					}
				}
				if (section.Attributes.Count == 0)
				{
					section.Remove();
				}
			}
			return found;
		}

		internal static bool RemoveCompilerFeatureRequiredAttribute(EntityDeclaration entityDecl, string feature)
		{
			bool found = false;
			foreach (var section in entityDecl.Attributes)
			{
				foreach (var attr in section.Attributes)
				{
					var symbol = attr.Type.GetSymbol();
					if (symbol is ITypeDefinition td && td.FullTypeName == KnownAttribute.CompilerFeatureRequired.GetTypeName()
						&& attr.Arguments.Count == 1 && attr.Arguments.SingleOrDefault() is PrimitiveExpression pe
						&& pe.Value is string s && s == feature)
					{
						attr.Remove();
						found = true;
					}
				}
				if (section.Attributes.Count == 0)
				{
					section.Remove();
				}
			}
			return found;
		}

		internal static bool RemoveObsoleteAttribute(EntityDeclaration entityDecl, string message)
		{
			bool found = false;
			foreach (var section in entityDecl.Attributes)
			{
				foreach (var attr in section.Attributes)
				{
					var symbol = attr.Type.GetSymbol();
					if (symbol is ITypeDefinition td && td.FullTypeName == KnownAttribute.Obsolete.GetTypeName()
						&& attr.Arguments.Count >= 1 && attr.Arguments.First() is PrimitiveExpression pe
						&& pe.Value is string s && s == message)
					{
						attr.Remove();
						found = true;
					}
				}
				if (section.Attributes.Count == 0)
				{
					section.Remove();
				}
			}
			return found;
		}

		bool FindAttribute(EntityDeclaration entityDecl, KnownAttribute attributeType, out Syntax.Attribute attribute)
		{
			attribute = null;
			foreach (var section in entityDecl.Attributes)
			{
				foreach (var attr in section.Attributes)
				{
					var symbol = attr.Type.GetSymbol();
					if (symbol is ITypeDefinition td && td.FullTypeName == attributeType.GetTypeName()) {
						attribute = attr;
						return true;
					}
				}
			}
			return false;
		}

		void AddDefinesForConditionalAttributes(ILFunction function, DecompileRun decompileRun)
		{
			foreach (var call in function.Descendants.OfType<CallInstruction>()) {
				var attr = call.Method.GetAttribute(KnownAttribute.Conditional, inherit: true);
				var symbolName = attr?.FixedArguments.FirstOrDefault().Value as string;
				if (symbolName == null || !decompileRun.DefinedSymbols.Add(symbolName))
					continue;
				syntaxTree.InsertChildAfter(null, new PreProcessorDirective(PreProcessorDirectiveType.Define, symbolName), Roles.PreProcessorDirective);
			}
		}

		EntityDeclaration DoDecompile(Decompiler.TypeSystem.IField field, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
		{
			Debug.Assert(decompilationContext.CurrentMember == field);
			try {
				var typeSystemAstBuilder = CreateAstBuilder(decompileRun.Settings);
				if (decompilationContext.CurrentTypeDefinition.Kind == TypeKind.Enum && field.IsConst) {
					var enumDec = new EnumMemberDeclaration();
					enumDec.WithAnnotation(field.MetadataToken);
					enumDec.NameToken = Identifier.Create(field.Name).WithAnnotation(field.MetadataToken);
					object constantValue = field.GetConstantValue();
					if (constantValue != null)
					{
						enumDec.Initializer = typeSystemAstBuilder.ConvertConstantValue(decompilationContext.CurrentTypeDefinition.EnumUnderlyingType, constantValue);
					}

					enumDec.Attributes.AddRange(field.GetAttributes().Select(a => new AttributeSection(typeSystemAstBuilder.ConvertAttribute(a))));
					enumDec.AddAnnotation(new MemberResolveResult(null, field));
					return enumDec;
				}
				bool isMathPIOrE = ((field.Name == "PI" || field.Name == "E") && (field.DeclaringType.FullName == "System.Math" || field.DeclaringType.FullName == "System.MathF"));
				typeSystemAstBuilder.UseSpecialConstants = !(field.DeclaringType.Equals(field.ReturnType) || isMathPIOrE);
				var fieldDecl = typeSystemAstBuilder.ConvertEntity(field);
				SetNewModifier(fieldDecl);
				if (settings.RequiredMembers && RemoveAttribute(fieldDecl, KnownAttribute.Required))
				{
					fieldDecl.Modifiers |= Modifiers.Required;
				}
				if (settings.FixedBuffers && IsFixedField(field, out var elementType, out var elementCount))
				{
					var fixedFieldDecl = new FixedFieldDeclaration();
					fieldDecl.Attributes.MoveTo(fixedFieldDecl.Attributes);
					fixedFieldDecl.Modifiers = fieldDecl.Modifiers;
					fixedFieldDecl.ReturnType = typeSystemAstBuilder.ConvertType(elementType);
					fixedFieldDecl.Variables.Add(new FixedVariableInitializer {
						NameToken = Identifier.Create(field.Name).WithAnnotation(field.MetadataToken),
						CountExpression = new PrimitiveExpression(elementCount)
					}.WithAnnotation(field.MetadataToken));
					fixedFieldDecl.Variables.Single().CopyAnnotationsFrom(((FieldDeclaration)fieldDecl).Variables.Single());
					fixedFieldDecl.CopyAnnotationsFrom(fieldDecl);
					RemoveAttribute(fixedFieldDecl, KnownAttribute.FixedBuffer);
					return fixedFieldDecl;
				}
				var fieldDefinition = (FieldDef)field.MetadataToken;
				if (fieldDefinition.HasFieldRVA && fieldDefinition.InitialValue.Length > 0) {
					// Field data as specified in II.16.3.2 of ECMA-335 6th edition:
					// .data I_X = int32(123)
					// .field public static int32 _x at I_X
					var message = string.Format(" Not supported: data({0}) ", BitConverter.ToString(fieldDefinition.InitialValue).Replace('-', ' '));
					((FieldDeclaration)fieldDecl).Variables.Single().AddChild(new Comment(message, CommentType.MultiLine), Roles.Comment);
				}
				return fieldDecl;
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(field.MetadataToken, innerException);
			}
		}

		internal static bool IsFixedField(ICSharpCode.Decompiler.TypeSystem.IField field, out ICSharpCode.Decompiler.TypeSystem.IType type, out int elementCount)
		{
			type = null;
			elementCount = 0;
			IAttribute attr = field.GetAttribute(KnownAttribute.FixedBuffer);
			if (attr != null && attr.FixedArguments.Length == 2)
			{
				if (attr.FixedArguments[0].Value is ICSharpCode.Decompiler.TypeSystem.IType trr && attr.FixedArguments[1].Value is int length)
				{
					type = trr;
					elementCount = length;
					return true;
				}
			}
			return false;
		}

		EntityDeclaration DoDecompile(IProperty property, DecompileRun decompileRun, ITypeResolveContext decompilationContext, ExtensionInfo extensionInfo)
		{
			Debug.Assert(decompilationContext.CurrentMember == property);
			try {
				var typeSystemAstBuilder = CreateAstBuilder(decompileRun.Settings);
				EntityDeclaration propertyDecl = typeSystemAstBuilder.ConvertEntity(property);
				if (property.IsExplicitInterfaceImplementation && !property.IsIndexer) {
					int lastDot = property.Name.LastIndexOf('.');
					propertyDecl.NameToken.Name = property.Name.Substring(lastDot + 1);
				}
				FixParameterNames(propertyDecl);
				Accessor getter, setter;
				if (propertyDecl is PropertyDeclaration propertyDeclaration) {
					getter = propertyDeclaration.Getter;
					setter = propertyDeclaration.Setter;
				} else {
					getter = ((IndexerDeclaration)propertyDecl).Getter;
					setter = ((IndexerDeclaration)propertyDecl).Setter;
				}

				bool getterHasBody = property.CanGet && property.Getter.HasBody;
				bool setterHasBody = property.CanSet && property.Setter.HasBody;
				if (getterHasBody)
				{
					DecompileBody(property.Getter, getter, decompileRun, decompilationContext, extensionInfo);
				}
				if (setterHasBody)
				{
					DecompileBody(property.Setter, setter, decompileRun, decompilationContext, extensionInfo);
				}
				if (!getterHasBody && !setterHasBody && !property.IsAbstract && property.DeclaringType.Kind != TypeKind.Interface) {
					propertyDecl.Modifiers |= Modifiers.Extern;
				}
				var accessor = (MethodDef)(property.Getter ?? property.Setter).MetadataToken;
				if (!accessor.HasOverrides && accessor.IsVirtual == accessor.IsNewSlot)
				{
					SetNewModifier(propertyDecl);
				}
				if (property.CanGet && IsCovariantReturnOverride(property.Getter))
				{
					RemoveAttribute(getter, KnownAttribute.PreserveBaseOverrides);
					propertyDecl.Modifiers &= ~(Modifiers.New | Modifiers.Virtual);
					propertyDecl.Modifiers |= Modifiers.Override;
				}
				if (settings.RequiredMembers && RemoveAttribute(propertyDecl, KnownAttribute.Required))
				{
					propertyDecl.Modifiers |= Modifiers.Required;
				}
				return propertyDecl;
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(property.MetadataToken, innerException);
			}
		}

		EntityDeclaration DoDecompile(IEvent ev, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
		{
			Debug.Assert(decompilationContext.CurrentMember == ev);
			try {
				bool adderHasBody = ev.CanAdd && ev.AddAccessor.HasBody;
				bool removerHasBody = ev.CanRemove && ev.RemoveAccessor.HasBody;
				var typeSystemAstBuilder = CreateAstBuilder(decompileRun.Settings);
				typeSystemAstBuilder.UseCustomEvents = ev.DeclaringTypeDefinition.Kind != TypeKind.Interface
					|| ev.IsExplicitInterfaceImplementation
					|| adderHasBody
					|| removerHasBody;
				var eventDecl = typeSystemAstBuilder.ConvertEntity(ev);
				if (ev.IsExplicitInterfaceImplementation) {
					int lastDot = ev.Name.LastIndexOf('.');
					eventDecl.NameToken.Name = ev.Name.Substring(lastDot + 1);
				}
				if (adderHasBody)
				{
					DecompileBody(ev.AddAccessor, ((CustomEventDeclaration)eventDecl).AddAccessor, decompileRun, decompilationContext, null);
				}
				if (removerHasBody)
				{
					DecompileBody(ev.RemoveAccessor, ((CustomEventDeclaration)eventDecl).RemoveAccessor, decompileRun, decompilationContext, null);
				}
				if (!adderHasBody && !removerHasBody && !ev.IsAbstract && ev.DeclaringType.Kind != TypeKind.Interface)
				{
					eventDecl.Modifiers |= Modifiers.Extern;
				}
				var accessor = (MethodDef)(ev.AddAccessor ?? ev.RemoveAccessor).MetadataToken;
				if (accessor.IsVirtual == accessor.IsNewSlot) {
					SetNewModifier(eventDecl);
				}
				return eventDecl;
			} catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
				throw new DecompilerException(ev.MetadataToken, innerException);
			}
		}

		#region Convert Type Reference
		/// <summary>
		/// Converts a type reference.
		/// </summary>
		/// <param name="type">The Cecil type reference that should be converted into
		/// a type system type reference.</param>
		/// <param name="typeAttributes">Attributes associated with the Cecil type reference.
		/// This is used to support the 'dynamic' type.</param>
		public static AstType ConvertType(ITypeDefOrRef type, StringBuilder sb, IHasCustomAttribute typeAttributes = null, ConvertTypeOptions options = ConvertTypeOptions.None)
		{
			int typeIndex = 0;
			return ConvertType(type, typeAttributes, ref typeIndex, options, 0, sb);
		}

		public static AstType ConvertType(TypeSig type, StringBuilder sb, IHasCustomAttribute typeAttributes = null, ConvertTypeOptions options = ConvertTypeOptions.None)
		{
			int typeIndex = 0;
			return ConvertType(type, typeAttributes, ref typeIndex, options, 0, sb);
		}

		const int MAX_CONVERTTYPE_DEPTH = 50;
		static AstType ConvertType(TypeSig type, IHasCustomAttribute typeAttributes, ref int typeIndex, ConvertTypeOptions options, int depth, StringBuilder sb)
		{
			if (depth++ > MAX_CONVERTTYPE_DEPTH)
				return AstType.Null;
			type = type.RemovePinnedAndModifiers();
			if (type == null) {
				return AstType.Null;
			}

			if (type is ByRefSig) {
				typeIndex++;
				// by reference type cannot be represented in C#; so we'll represent it as a pointer instead
				return ConvertType((type as ByRefSig).Next, typeAttributes, ref typeIndex, options, depth, sb)
					.MakePointerType();
			} else if (type is PtrSig) {
				typeIndex++;
				return ConvertType((type as PtrSig).Next, typeAttributes, ref typeIndex, options, depth, sb)
					.MakePointerType();
			} else if (type is ArraySigBase) {
				typeIndex++;
				return ConvertType((type as ArraySigBase).Next, typeAttributes, ref typeIndex, options, depth, sb)
					.MakeArrayType((int)(type as ArraySigBase).Rank);
			} else if (type is GenericInstSig) {
				GenericInstSig gType = (GenericInstSig)type;
				if (gType.GenericType != null && gType.GenericArguments.Count == 1 && gType.GenericType.IsSystemNullable()) {
					typeIndex++;
					return new ComposedType {
						BaseType = ConvertType(gType.GenericArguments[0], typeAttributes, ref typeIndex, options, depth, sb),
						HasNullableSpecifier = true
					};
				}
				AstType baseType = ConvertType(gType.GenericType == null ? null : gType.GenericType.TypeDefOrRef, typeAttributes, ref typeIndex, options & ~ConvertTypeOptions.IncludeTypeParameterDefinitions, depth, sb);
				List<AstType> typeArguments = new List<AstType>();
				foreach (var typeArgument in gType.GenericArguments) {
					typeIndex++;
					typeArguments.Add(ConvertType(typeArgument, typeAttributes, ref typeIndex, options, depth, sb));
				}
				ApplyTypeArgumentsTo(baseType, typeArguments);
				return baseType;
			} else if (type is GenericSig) {
				var sig = (GenericSig)type;
				var simpleType = new SimpleType(sig.GetName(sb)).WithAnnotation(sig.GenericParam).WithAnnotation(type);
				simpleType.IdentifierToken.WithAnnotation(sig.GenericParam).WithAnnotation(type);
				return simpleType;
			} else if (type is TypeDefOrRefSig) {
				return ConvertType(((TypeDefOrRefSig)type).TypeDefOrRef, typeAttributes, ref typeIndex, options, depth, sb);
			} else
				return ConvertType(type.ToTypeDefOrRef(), typeAttributes, ref typeIndex, options, depth, sb);
		}

		static AstType ConvertType(ITypeDefOrRef type, IHasCustomAttribute typeAttributes, ref int typeIndex, ConvertTypeOptions options, int depth, StringBuilder sb)
		{
			if (depth++ > MAX_CONVERTTYPE_DEPTH || type == null)
				return AstType.Null;

			var ts = type as TypeSpec;
			if (ts != null && !(ts.TypeSig is FnPtrSig))
				return ConvertType(ts.TypeSig, typeAttributes, ref typeIndex, options, depth, sb);

			if (type.DeclaringType != null && (options & ConvertTypeOptions.DoNotIncludeEnclosingType) == 0) {
				AstType typeRef = ConvertType(type.DeclaringType, typeAttributes, ref typeIndex, options & ~ConvertTypeOptions.IncludeTypeParameterDefinitions, depth, sb);
				string namepart = ReflectionHelper.SplitTypeParameterCountFromReflectionName(type.Name);
				MemberType memberType = new MemberType { Target = typeRef, MemberNameToken = Identifier.Create(namepart).WithAnnotation(type) };
				memberType.AddAnnotation(type);
				if ((options & ConvertTypeOptions.IncludeTypeParameterDefinitions) == ConvertTypeOptions.IncludeTypeParameterDefinitions) {
					AddTypeParameterDefininitionsTo(type, memberType);
				}
				return memberType;
			} else {
				string ns = type.GetNamespace(sb) ?? string.Empty;
				string name = type.GetName(sb);
				if (ts != null)
					name = DnlibExtensions.GetFnPtrName(ts.TypeSig as FnPtrSig);
				if (name == null)
					throw new InvalidOperationException("type.Name returned null. Type: " + type.ToString());

				if (name == "Object" && ns == "System" && HasDynamicAttribute(typeAttributes, typeIndex)) {
					return new Syntax.PrimitiveType("dynamic");
				} else {
					if (ns == "System") {
						if ((options & ConvertTypeOptions.DoNotUsePrimitiveTypeNames)
							!= ConvertTypeOptions.DoNotUsePrimitiveTypeNames) {
							switch (name) {
								case "SByte":
									return new Syntax.PrimitiveType("sbyte").WithAnnotation(type);
								case "Int16":
									return new Syntax.PrimitiveType("short").WithAnnotation(type);
								case "Int32":
									return new Syntax.PrimitiveType("int").WithAnnotation(type);
								case "Int64":
									return new Syntax.PrimitiveType("long").WithAnnotation(type);
								case "Byte":
									return new Syntax.PrimitiveType("byte").WithAnnotation(type);
								case "UInt16":
									return new Syntax.PrimitiveType("ushort").WithAnnotation(type);
								case "UInt32":
									return new Syntax.PrimitiveType("uint").WithAnnotation(type);
								case "UInt64":
									return new Syntax.PrimitiveType("ulong").WithAnnotation(type);
								case "String":
									return new Syntax.PrimitiveType("string").WithAnnotation(type);
								case "Single":
									return new Syntax.PrimitiveType("float").WithAnnotation(type);
								case "Double":
									return new Syntax.PrimitiveType("double").WithAnnotation(type);
								case "Decimal":
									return new Syntax.PrimitiveType("decimal").WithAnnotation(type);
								case "Char":
									return new Syntax.PrimitiveType("char").WithAnnotation(type);
								case "Boolean":
									return new Syntax.PrimitiveType("bool").WithAnnotation(type);
								case "Void":
									return new Syntax.PrimitiveType("void").WithAnnotation(type);
								case "Object":
									return new Syntax.PrimitiveType("object").WithAnnotation(type);
							}
						}
					}

					name = ReflectionHelper.SplitTypeParameterCountFromReflectionName(name);

					AstType astType;
					if ((options & ConvertTypeOptions.IncludeNamespace) == ConvertTypeOptions.IncludeNamespace && ns.Length > 0) {
						string[] parts = ns.Split('.');
						var nsAsm = type.DefinitionAssembly;
						sb.Clear();
						sb.Append(parts[0]);
						SimpleType simpleType;
						AstType nsType = simpleType = new SimpleType(parts[0]).WithAnnotation(BoxedTextColor.Namespace);
						simpleType.IdentifierToken.WithAnnotation(BoxedTextColor.Namespace).WithAnnotation(new NamespaceReference(nsAsm, parts[0]));
						for (int i = 1; i < parts.Length; i++) {
							sb.Append('.');
							sb.Append(parts[i]);
							var nsPart = sb.ToString();
							nsType = new MemberType { Target = nsType, MemberNameToken = Identifier.Create(parts[i]).WithAnnotation(BoxedTextColor.Namespace).WithAnnotation(new NamespaceReference(nsAsm, nsPart)) }.WithAnnotation(BoxedTextColor.Namespace);
						}
						astType = new MemberType { Target = nsType, MemberNameToken = Identifier.Create(name).WithAnnotation(type) };
					} else {
						astType = new SimpleType(name);
					}
					astType.AddAnnotation(type);

					if ((options & ConvertTypeOptions.IncludeTypeParameterDefinitions) == ConvertTypeOptions.IncludeTypeParameterDefinitions) {
						AddTypeParameterDefininitionsTo(type, astType);
					}
					return astType;
				}
			}
		}

		static void AddTypeParameterDefininitionsTo(ITypeDefOrRef type, AstType astType)
		{
			TypeDef typeDef = type.ResolveTypeDef();
			if (typeDef != null && typeDef.HasGenericParameters) {
				List<AstType> typeArguments = new List<AstType>();
				foreach (GenericParam gp in typeDef.GenericParameters) {
					typeArguments.Add(new SimpleType(gp.Name).WithAnnotation(gp));
				}
				ApplyTypeArgumentsTo(astType, typeArguments);
			}
		}

		static void ApplyTypeArgumentsTo(AstType baseType, List<AstType> typeArguments)
		{
			SimpleType st = baseType as SimpleType;
			if (st != null) {
				ITypeDefOrRef type = st.Annotation<ITypeDefOrRef>();
				if (type != null) {
					ReflectionHelper.SplitTypeParameterCountFromReflectionName(type.Name, out int typeParameterCount);
					if (typeParameterCount > typeArguments.Count)
						typeParameterCount = typeArguments.Count;
					st.TypeArguments.AddRange(typeArguments.GetRange(typeArguments.Count - typeParameterCount, typeParameterCount));
				} else {
					st.TypeArguments.AddRange(typeArguments);
				}
			}
			MemberType mt = baseType as MemberType;
			if (mt != null) {
				ITypeDefOrRef type = mt.Annotation<ITypeDefOrRef>();
				if (type != null) {
					ReflectionHelper.SplitTypeParameterCountFromReflectionName(type.Name, out int typeParameterCount);
					if (typeParameterCount > typeArguments.Count)
						typeParameterCount = typeArguments.Count;
					mt.TypeArguments.AddRange(typeArguments.GetRange(typeArguments.Count - typeParameterCount, typeParameterCount));
					typeArguments.RemoveRange(typeArguments.Count - typeParameterCount, typeParameterCount);
					if (typeArguments.Count > 0)
						ApplyTypeArgumentsTo(mt.Target, typeArguments);
				} else {
					mt.TypeArguments.AddRange(typeArguments);
				}
			}
		}

		static readonly UTF8String systemRuntimeCompilerServicesString = new UTF8String("System.Runtime.CompilerServices");
		static readonly UTF8String dynamicAttributeString = new UTF8String("DynamicAttribute");
		static bool HasDynamicAttribute(IHasCustomAttribute attributeProvider, int typeIndex)
		{
			if (attributeProvider == null)
				return false;
			foreach (CustomAttribute a in attributeProvider.CustomAttributes) {
				if (a.AttributeType.Compare(systemRuntimeCompilerServicesString, dynamicAttributeString)) {
					if (a.ConstructorArguments.Count == 1) {
						IList<CAArgument> values = a.ConstructorArguments[0].Value as IList<CAArgument>;
						if (values != null && typeIndex < values.Count && values[typeIndex].Value is bool)
							return (bool)values[typeIndex].Value;
					}
					return true;
				}
			}
			return false;
		}
		#endregion

		#region Sequence Points
		/// <summary>
		/// Creates sequence points for the given syntax tree.
		///
		/// This only works correctly when the nodes in the syntax tree have line/column information.
		/// </summary>
		public Dictionary<ILFunction, List<DebugInfo.SequencePoint>> CreateSequencePoints(SyntaxTree syntaxTree)
		{
			SequencePointBuilder spb = new SequencePointBuilder();
			syntaxTree.AcceptVisitor(spb);
			return spb.GetSequencePoints();
		}
		#endregion
	}

	[Flags]
	public enum ConvertTypeOptions
	{
		None = 0,
		IncludeNamespace = 1,
		IncludeTypeParameterDefinitions = 2,
		DoNotUsePrimitiveTypeNames = 4,
		DoNotIncludeEnclosingType = 8,
	}
}
