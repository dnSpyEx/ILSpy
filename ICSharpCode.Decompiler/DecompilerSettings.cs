// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;

namespace ICSharpCode.Decompiler
{
	public enum DecompilationObject {
		NestedTypes,
		Fields,
		Events,
		Properties,
		Methods,
	}

	/// <summary>
	/// Settings for the decompiler.
	/// </summary>
	public class DecompilerSettings : INotifyPropertyChanged, IEquatable<DecompilerSettings>
	{
		/// <summary>
		/// Equivalent to <c>new DecompilerSettings(LanguageVersion.Latest)</c>
		/// </summary>
		public DecompilerSettings()
		{
		}

		/// <summary>
		/// Creates a new DecompilerSettings instance with initial settings
		/// appropriate for the specified language version.
		/// </summary>
		/// <remarks>
		/// This does not imply that the resulting code strictly uses only language features from
		/// that version. Language constructs like generics or ref locals cannot be removed from
		/// the compiled code.
		/// </remarks>
		public DecompilerSettings(CSharp.LanguageVersion languageVersion)
		{
			SetLanguageVersion(languageVersion);
		}

		/// <summary>
		/// Deactivates all language features from versions newer than <paramref name="languageVersion"/>.
		/// </summary>
		public void SetLanguageVersion(CSharp.LanguageVersion languageVersion)
		{
			// By default, all decompiler features are enabled.
			// Disable some of them based on language version:
			if (languageVersion < CSharp.LanguageVersion.CSharp2)
			{
				anonymousMethods = false;
				liftNullables = false;
				yieldReturn = false;
				useImplicitMethodGroupConversion = false;
				useObjectCreationOfGenericTypeParameter = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp3)
			{
				anonymousTypes = false;
				useLambdaSyntax = false;
				objectCollectionInitializers = false;
				automaticProperties = false;
				extensionMethods = false;
				queryExpressions = false;
				expressionTrees = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp4)
			{
				dynamic = false;
				namedArguments = false;
				optionalArguments = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp5)
			{
				asyncAwait = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp6)
			{
				awaitInCatchFinally = false;
				useExpressionBodyForCalculatedGetterOnlyProperties = false;
				nullPropagation = false;
				stringInterpolation = false;
				dictionaryInitializers = false;
				extensionMethodsInCollectionInitializers = false;
				getterOnlyAutomaticProperties = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp7)
			{
				outVariables = false;
				throwExpressions = false;
				tupleTypes = false;
				tupleConversions = false;
				discards = false;
				localFunctions = false;
				deconstruction = false;
				patternMatching = false;
				useRefLocalsForAccurateOrderOfEvaluation = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp7_2)
			{
				introduceReadonlyAndInModifiers = false;
				introduceRefModifiersOnStructs = false;
				nonTrailingNamedArguments = false;
				refExtensionMethods = false;
				introducePrivateProtectedAccessibilty = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp7_3)
			{
				introduceUnmanagedConstraint = false;
				stackAllocInitializers = false;
				tupleComparisons = false;
				patternBasedFixedStatement = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp8_0)
			{
				nullableReferenceTypes = false;
				readOnlyMethods = false;
				asyncUsingAndForEachStatement = false;
				asyncEnumerator = false;
				useEnhancedUsing = false;
				staticLocalFunctions = false;
				ranges = false;
				switchExpressions = false;
				recursivePatternMatching = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp9_0)
			{
				nativeIntegers = false;
				initAccessors = false;
				functionPointers = false;
				forEachWithGetEnumeratorExtension = false;
				recordClasses = false;
				withExpressions = false;
				usePrimaryConstructorSyntax = false;
				covariantReturns = false;
				relationalPatterns = false;
				patternCombinators = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp10_0)
			{
				fileScopedNamespaces = false;
				recordStructs = false;
				structDefaultConstructorsAndFieldInitializers = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp11_0)
			{
				scopedRef = false;
				requiredMembers = false;
				numericIntPtr = false;
				utf8StringLiterals = false;
				unsignedRightShift = false;
				checkedOperators = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp12_0)
			{
				refReadOnlyParameters = false;
				usePrimaryConstructorSyntaxForNonRecordTypes = false;
				inlineArrays = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp13_0)
			{
				paramsCollections = false;
			}
			if (languageVersion < CSharp.LanguageVersion.CSharp14_0)
			{
				extensionMembers = false;
				firstClassSpanTypes = false;
			}
		}

		public CSharp.LanguageVersion GetMinimumRequiredVersion()
		{
			if (extensionMembers || firstClassSpanTypes)
				return CSharp.LanguageVersion.CSharp14_0;
			if (paramsCollections)
				return CSharp.LanguageVersion.CSharp13_0;
			if (refReadOnlyParameters || usePrimaryConstructorSyntaxForNonRecordTypes || inlineArrays)
				return CSharp.LanguageVersion.CSharp12_0;
			if (scopedRef || requiredMembers || numericIntPtr || utf8StringLiterals || unsignedRightShift || checkedOperators)
				return CSharp.LanguageVersion.CSharp11_0;
			if (fileScopedNamespaces || recordStructs || structDefaultConstructorsAndFieldInitializers)
				return CSharp.LanguageVersion.CSharp10_0;
			if (nativeIntegers || initAccessors || functionPointers || forEachWithGetEnumeratorExtension
				|| recordClasses || withExpressions || usePrimaryConstructorSyntax || covariantReturns
				|| relationalPatterns || patternCombinators)
				return CSharp.LanguageVersion.CSharp9_0;
			if (nullableReferenceTypes || readOnlyMethods || asyncEnumerator || asyncUsingAndForEachStatement
				|| staticLocalFunctions || ranges || switchExpressions || recursivePatternMatching)
				return CSharp.LanguageVersion.CSharp8_0;
			if (introduceUnmanagedConstraint || tupleComparisons || stackAllocInitializers
				|| patternBasedFixedStatement)
				return CSharp.LanguageVersion.CSharp7_3;
			if (introduceRefModifiersOnStructs || introduceReadonlyAndInModifiers
				|| nonTrailingNamedArguments || refExtensionMethods || introducePrivateProtectedAccessibilty)
				return CSharp.LanguageVersion.CSharp7_2;
			// C# 7.1 missing
			if (outVariables || throwExpressions || tupleTypes || tupleConversions
				|| discards || localFunctions || deconstruction || patternMatching || useRefLocalsForAccurateOrderOfEvaluation)
				return CSharp.LanguageVersion.CSharp7;
			if (awaitInCatchFinally || useExpressionBodyForCalculatedGetterOnlyProperties || nullPropagation
				|| stringInterpolation || dictionaryInitializers || extensionMethodsInCollectionInitializers
				|| getterOnlyAutomaticProperties)
				return CSharp.LanguageVersion.CSharp6;
			if (asyncAwait)
				return CSharp.LanguageVersion.CSharp5;
			if (dynamic || namedArguments || optionalArguments)
				return CSharp.LanguageVersion.CSharp4;
			if (anonymousTypes || objectCollectionInitializers || automaticProperties
				|| queryExpressions || expressionTrees)
				return CSharp.LanguageVersion.CSharp3;
			if (anonymousMethods || liftNullables || yieldReturn || useImplicitMethodGroupConversion || useObjectCreationOfGenericTypeParameter)
				return CSharp.LanguageVersion.CSharp2;
			return CSharp.LanguageVersion.CSharp1;
		}

		protected virtual void OnModified() {
		}

		DecompilationObject[] decompilationObjects = new DecompilationObject[5] {
			DecompilationObject.Methods,
			DecompilationObject.Properties,
			DecompilationObject.Events,
			DecompilationObject.Fields,
			DecompilationObject.NestedTypes,
		};

		public IEnumerable<DecompilationObject> DecompilationObjects {
			get { return decompilationObjects.AsEnumerable(); }
		}

		public DecompilationObject DecompilationObject0 {
			get { return decompilationObjects[0]; }
			set { SetDecompilationObject(0, value); }
		}

		public DecompilationObject DecompilationObject1 {
			get { return decompilationObjects[1]; }
			set { SetDecompilationObject(1, value); }
		}

		public DecompilationObject DecompilationObject2 {
			get { return decompilationObjects[2]; }
			set { SetDecompilationObject(2, value); }
		}

		public DecompilationObject DecompilationObject3 {
			get { return decompilationObjects[3]; }
			set { SetDecompilationObject(3, value); }
		}

		public DecompilationObject DecompilationObject4 {
			get { return decompilationObjects[4]; }
			set { SetDecompilationObject(4, value); }
		}

		void SetDecompilationObject(int index, DecompilationObject newValue) {
			if (decompilationObjects[index] == newValue)
				return;

			int otherIndex = Array.IndexOf(decompilationObjects, newValue);
			Debug.Assert(otherIndex >= 0);
			if (otherIndex >= 0) {
				decompilationObjects[otherIndex] = decompilationObjects[index];
				decompilationObjects[index] = newValue;

				OnPropertyChanged(string.Format(DecompilationObject_format, otherIndex));
			}
			OnPropertyChanged(string.Format(DecompilationObject_format, index));
		}
		static string DecompilationObject_format = nameof(DecompilationObject0).Substring(0, nameof(DecompilationObject0).Length - 1) + "{0}";

		bool nativeIntegers = true;

		/// <summary>
		/// Use C# 9 <c>nint</c>/<c>nuint</c> types.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.NativeIntegers")]
		public bool NativeIntegers {
			get { return nativeIntegers; }
			set {
				if (nativeIntegers != value)
				{
					nativeIntegers = value;
					OnPropertyChanged();
				}
			}
		}

		bool numericIntPtr = false; // TODO: reenable

		/// <summary>
		/// Treat <c>IntPtr</c>/<c>UIntPtr</c> as <c>nint</c>/<c>nuint</c>.
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.NumericIntPtr")]
		public bool NumericIntPtr {
			get { return numericIntPtr; }
			set {
				if (numericIntPtr != value)
				{
					numericIntPtr = value;
					OnPropertyChanged();
				}
			}
		}

		bool covariantReturns = true;

		/// <summary>
		/// Decompile C# 9 covariant return types.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.CovariantReturns")]
		public bool CovariantReturns {
			get { return covariantReturns; }
			set {
				if (covariantReturns != value)
				{
					covariantReturns = value;
					OnPropertyChanged();
				}
			}
		}

		bool initAccessors = true;

		/// <summary>
		/// Use C# 9 <c>init;</c> property accessors.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.InitAccessors")]
		public bool InitAccessors {
			get { return initAccessors; }
			set {
				if (initAccessors != value)
				{
					initAccessors = value;
					OnPropertyChanged();
				}
			}
		}

		bool recordClasses = true;

		/// <summary>
		/// Use C# 9 <c>record</c> classes.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.RecordClasses")]
		public bool RecordClasses {
			get { return recordClasses; }
			set {
				if (recordClasses != value)
				{
					recordClasses = value;
					OnPropertyChanged();
				}
			}
		}

		bool recordStructs = true;

		/// <summary>
		/// Use C# 10 <c>record</c> structs.
		/// </summary>
		[Category("C# 10.0 / VS 2022")]
		[Description("DecompilerSettings.RecordStructs")]
		public bool RecordStructs {
			get { return recordStructs; }
			set {
				if (recordStructs != value)
				{
					recordStructs = value;
					OnPropertyChanged();
				}
			}
		}

		bool structDefaultConstructorsAndFieldInitializers = true;

		/// <summary>
		/// Use field initializers in structs.
		/// </summary>
		[Category("C# 10.0 / VS 2022")]
		[Description("DecompilerSettings.StructDefaultConstructorsAndFieldInitializers")]
		public bool StructDefaultConstructorsAndFieldInitializers {
			get { return structDefaultConstructorsAndFieldInitializers; }
			set {
				if (structDefaultConstructorsAndFieldInitializers != value)
				{
					structDefaultConstructorsAndFieldInitializers = value;
					OnPropertyChanged();
				}
			}
		}

		bool withExpressions = true;

		/// <summary>
		/// Use C# 9 <c>with</c> initializer expressions.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.WithExpressions")]
		public bool WithExpressions {
			get { return withExpressions; }
			set {
				if (withExpressions != value)
				{
					withExpressions = value;
					OnPropertyChanged();
				}
			}
		}

		bool usePrimaryConstructorSyntax = true;

		/// <summary>
		/// Use primary constructor syntax with records.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.UsePrimaryConstructorSyntax")]
		public bool UsePrimaryConstructorSyntax {
			get { return usePrimaryConstructorSyntax; }
			set {
				if (usePrimaryConstructorSyntax != value)
				{
					usePrimaryConstructorSyntax = value;
					OnPropertyChanged();
				}
			}
		}

		bool functionPointers = true;

		/// <summary>
		/// Use C# 9 <c>delegate* unmanaged</c> types.
		/// If this option is disabled, function pointers will instead be decompiled with type `IntPtr`.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.FunctionPointers")]
		public bool FunctionPointers {
			get { return functionPointers; }
			set {
				if (functionPointers != value)
				{
					functionPointers = value;
					OnPropertyChanged();
				}
			}
		}

		bool scopedRef = true;

		/// <summary>
		/// Use C# 11 <c>scoped</c> modifier.
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.ScopedRef")]
		public bool ScopedRef {
			get { return scopedRef; }
			set {
				if (scopedRef != value)
				{
					scopedRef = value;
					OnPropertyChanged();
				}
			}
		}

		[Obsolete("Renamed to ScopedRef. This property will be removed in a future version of the decompiler.")]
		[Browsable(false)]
		public bool LifetimeAnnotations {
			get { return ScopedRef; }
			set { ScopedRef = value; }
		}

		bool requiredMembers = true;

		/// <summary>
		/// Use C# 11 <c>required</c> modifier.
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.RequiredMembers")]
		public bool RequiredMembers {
			get { return requiredMembers; }
			set {
				if (requiredMembers != value)
				{
					requiredMembers = value;
					OnPropertyChanged();
				}
			}
		}

		bool switchExpressions = true;

		/// <summary>
		/// Use C# 8 switch expressions.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.SwitchExpressions")]
		public bool SwitchExpressions {
			get { return switchExpressions; }
			set {
				if (switchExpressions != value)
				{
					switchExpressions = value;
					OnPropertyChanged();
				}
			}
		}

		bool fileScopedNamespaces = false; // TODO: reenable

		/// <summary>
		/// Use C# 10 file-scoped namespaces.
		/// </summary>
		[Category("C# 10.0 / VS 2022")]
		[Description("DecompilerSettings.FileScopedNamespaces")]
		public bool FileScopedNamespaces {
			get { return fileScopedNamespaces; }
			set {
				if (fileScopedNamespaces != value)
				{
					fileScopedNamespaces = value;
					OnPropertyChanged();
				}
			}
		}

		bool anonymousMethods = true;

		/// <summary>
		/// Decompile anonymous methods/lambdas.
		/// </summary>
		[Category("C# 2.0 / VS 2005")]
		[Description("DecompilerSettings.DecompileAnonymousMethodsLambdas")]
		public bool AnonymousMethods {
			get { return anonymousMethods; }
			set {
				if (anonymousMethods != value)
				{
					anonymousMethods = value;
					OnPropertyChanged();
				}
			}
		}

		bool anonymousTypes = true;

		/// <summary>
		/// Decompile anonymous types.
		/// </summary>
		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.DecompileAnonymousTypes")]
		public bool AnonymousTypes {
			get { return anonymousTypes; }
			set {
				if (anonymousTypes != value)
				{
					anonymousTypes = value;
					OnPropertyChanged();
				}
			}
		}

		bool useLambdaSyntax = true;

		/// <summary>
		/// Use C# 3 lambda syntax if possible.
		/// </summary>
		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.UseLambdaSyntaxIfPossible")]
		public bool UseLambdaSyntax {
			get { return useLambdaSyntax; }
			set {
				if (useLambdaSyntax != value)
				{
					useLambdaSyntax = value;
					OnPropertyChanged();
				}
			}
		}

		bool expressionTrees = true;

		/// <summary>
		/// Decompile expression trees.
		/// </summary>
		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.DecompileExpressionTrees")]
		public bool ExpressionTrees {
			get { return expressionTrees; }
			set {
				if (expressionTrees != value)
				{
					expressionTrees = value;
					OnPropertyChanged();
				}
			}
		}

		bool yieldReturn = true;

		/// <summary>
		/// Decompile enumerators.
		/// </summary>
		[Category("C# 2.0 / VS 2005")]
		[Description("DecompilerSettings.DecompileEnumeratorsYieldReturn")]
		public bool YieldReturn {
			get { return yieldReturn; }
			set {
				if (yieldReturn != value)
				{
					yieldReturn = value;
					OnPropertyChanged();
				}
			}
		}

		bool dynamic = true;

		/// <summary>
		/// Decompile use of the 'dynamic' type.
		/// </summary>
		[Category("C# 4.0 / VS 2010")]
		[Description("DecompilerSettings.DecompileUseOfTheDynamicType")]
		public bool Dynamic {
			get { return dynamic; }
			set {
				if (dynamic != value)
				{
					dynamic = value;
					OnPropertyChanged();
				}
			}
		}

		bool asyncAwait = true;

		/// <summary>
		/// Decompile async methods.
		/// </summary>
		[Category("C# 5.0 / VS 2012")]
		[Description("DecompilerSettings.DecompileAsyncMethods")]
		public bool AsyncAwait {
			get { return asyncAwait; }
			set {
				if (asyncAwait != value)
				{
					asyncAwait = value;
					OnPropertyChanged();
				}
			}
		}

		bool awaitInCatchFinally = true;

		/// <summary>
		/// Decompile await in catch/finally blocks.
		/// Only has an effect if <see cref="AsyncAwait"/> is enabled.
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.DecompileAwaitInCatchFinallyBlocks")]
		public bool AwaitInCatchFinally {
			get { return awaitInCatchFinally; }
			set {
				if (awaitInCatchFinally != value)
				{
					awaitInCatchFinally = value;
					OnPropertyChanged();
				}
			}
		}

		bool asyncEnumerator = true;

		/// <summary>
		/// Decompile IAsyncEnumerator/IAsyncEnumerable.
		/// Only has an effect if <see cref="AsyncAwait"/> is enabled.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.AsyncEnumerator")]
		public bool AsyncEnumerator {
			get { return asyncEnumerator; }
			set {
				if (asyncEnumerator != value)
				{
					asyncEnumerator = value;
					OnPropertyChanged();
				}
			}
		}

		bool decimalConstants = true;

		/// <summary>
		/// Decompile [DecimalConstant(...)] as simple literal values.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DecompileDecimalConstantAsSimpleLiteralValues")]
		public bool DecimalConstants {
			get { return decimalConstants; }
			set {
				if (decimalConstants != value)
				{
					decimalConstants = value;
					OnPropertyChanged();
				}
			}
		}

		bool fixedBuffers = true;

		/// <summary>
		/// Decompile C# 1.0 'public unsafe fixed int arr[10];' members.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DecompileC10PublicUnsafeFixedIntArr10Members")]
		public bool FixedBuffers {
			get { return fixedBuffers; }
			set {
				if (fixedBuffers != value)
				{
					fixedBuffers = value;
					OnPropertyChanged();
				}
			}
		}

		bool stringConcat = true;

		/// <summary>
		/// Decompile 'string.Concat(a, b)' calls into 'a + b'.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.StringConcat")]
		public bool StringConcat {
			get { return stringConcat; }
			set {
				if (stringConcat != value)
				{
					stringConcat = value;
					OnPropertyChanged();
				}
			}
		}

		bool liftNullables = true;

		/// <summary>
		/// Use lifted operators for nullables.
		/// </summary>
		[Category("C# 2.0 / VS 2005")]
		[Description("DecompilerSettings.UseLiftedOperatorsForNullables")]
		public bool LiftNullables {
			get { return liftNullables; }
			set {
				if (liftNullables != value)
				{
					liftNullables = value;
					OnPropertyChanged();
				}
			}
		}

		bool nullPropagation = true;

		/// <summary>
		/// Decompile C# 6 ?. and ?[] operators.
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.NullPropagation")]
		public bool NullPropagation {
			get { return nullPropagation; }
			set {
				if (nullPropagation != value)
				{
					nullPropagation = value;
					OnPropertyChanged();
				}
			}
		}

		bool automaticProperties = true;

		/// <summary>
		/// Decompile automatic properties
		/// </summary>
		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.DecompileAutomaticProperties")]
		public bool AutomaticProperties {
			get { return automaticProperties; }
			set {
				if (automaticProperties != value)
				{
					automaticProperties = value;
					OnPropertyChanged();
				}
			}
		}

		bool getterOnlyAutomaticProperties = true;

		/// <summary>
		/// Decompile getter-only automatic properties
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.GetterOnlyAutomaticProperties")]
		public bool GetterOnlyAutomaticProperties {
			get { return getterOnlyAutomaticProperties; }
			set {
				if (getterOnlyAutomaticProperties != value)
				{
					getterOnlyAutomaticProperties = value;
					OnPropertyChanged();
				}
			}
		}

		bool automaticEvents = true;

		/// <summary>
		/// Decompile automatic events
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DecompileAutomaticEvents")]
		public bool AutomaticEvents {
			get { return automaticEvents; }
			set {
				if (automaticEvents != value)
				{
					automaticEvents = value;
					OnPropertyChanged();
				}
			}
		}

		bool usingStatement = true;

		/// <summary>
		/// Decompile using statements.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DetectUsingStatements")]
		public bool UsingStatement {
			get { return usingStatement; }
			set {
				if (usingStatement != value)
				{
					usingStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool useEnhancedUsing = true;

		/// <summary>
		/// Use enhanced using statements.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.UseEnhancedUsing")]
		public bool UseEnhancedUsing {
			get { return useEnhancedUsing; }
			set {
				if (useEnhancedUsing != value)
				{
					useEnhancedUsing = value;
					OnPropertyChanged();
				}
			}
		}

		bool alwaysUseBraces = true;

		/// <summary>
		/// Gets/Sets whether to use braces for single-statement-blocks.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysUseBraces")]
		public bool AlwaysUseBraces {
			get { return alwaysUseBraces; }
			set {
				if (alwaysUseBraces != value)
				{
					alwaysUseBraces = value;
					OnPropertyChanged();
				}
			}
		}

		bool forEachStatement = true;

		/// <summary>
		/// Decompile foreach statements.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DetectForeachStatements")]
		public bool ForEachStatement {
			get { return forEachStatement; }
			set {
				if (forEachStatement != value)
				{
					forEachStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool forEachWithGetEnumeratorExtension = true;

		/// <summary>
		/// Support GetEnumerator extension methods in foreach.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.DecompileForEachWithGetEnumeratorExtension")]
		public bool ForEachWithGetEnumeratorExtension {
			get { return forEachWithGetEnumeratorExtension; }
			set {
				if (forEachWithGetEnumeratorExtension != value)
				{
					forEachWithGetEnumeratorExtension = value;
					OnPropertyChanged();
				}
			}
		}

		bool paramsCollections = true;

		/// <summary>
		/// Support params collections.
		/// </summary>
		[Category("C# 13.0 / VS 2022.12")]
		[Description("DecompilerSettings.DecompileParamsCollections")]
		public bool ParamsCollections {
			get { return paramsCollections; }
			set {
				if (paramsCollections != value)
				{
					paramsCollections = value;
					OnPropertyChanged();
				}
			}
		}

		bool lockStatement = true;

		/// <summary>
		/// Decompile lock statements.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DetectLockStatements")]
		public bool LockStatement {
			get { return lockStatement; }
			set {
				if (lockStatement != value)
				{
					lockStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool switchStatementOnString = true;

		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DetectSwitchOnString")]
		public bool SwitchStatementOnString {
			get { return switchStatementOnString; }
			set {
				if (switchStatementOnString != value)
				{
					switchStatementOnString = value;
					OnPropertyChanged();
				}
			}
		}

		bool sparseIntegerSwitch = true;

		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.SparseIntegerSwitch")]
		public bool SparseIntegerSwitch {
			get { return sparseIntegerSwitch; }
			set {
				if (sparseIntegerSwitch != value)
				{
					sparseIntegerSwitch = value;
					OnPropertyChanged();
				}
			}
		}

		bool usingDeclarations = true;

		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.InsertUsingDeclarations")]
		public bool UsingDeclarations {
			get { return usingDeclarations; }
			set {
				if (usingDeclarations != value)
				{
					usingDeclarations = value;
					OnPropertyChanged();
				}
			}
		}

		bool extensionMethods = true;

		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.UseExtensionMethodSyntax")]
		public bool ExtensionMethods {
			get { return extensionMethods; }
			set {
				if (extensionMethods != value)
				{
					extensionMethods = value;
					OnPropertyChanged();
				}
			}
		}

		bool queryExpressions = true;

		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.UseLINQExpressionSyntax")]
		public bool QueryExpressions {
			get { return queryExpressions; }
			set {
				if (queryExpressions != value)
				{
					queryExpressions = value;
					OnPropertyChanged();
				}
			}
		}

		bool useImplicitMethodGroupConversion = true;

		/// <summary>
		/// Gets/Sets whether to use C# 2.0 method group conversions.
		/// true: <c>EventHandler h = this.OnClick;</c>
		/// false: <c>EventHandler h = new EventHandler(this.OnClick);</c>
		/// </summary>
		[Category("C# 2.0 / VS 2005")]
		[Description("DecompilerSettings.UseImplicitMethodGroupConversions")]
		public bool UseImplicitMethodGroupConversion {
			get { return useImplicitMethodGroupConversion; }
			set {
				if (useImplicitMethodGroupConversion != value)
				{
					useImplicitMethodGroupConversion = value;
					OnPropertyChanged();
				}
			}
		}

		bool useObjectCreationOfGenericTypeParameter = true;

		/// <summary>
		/// Gets/Sets whether to use object creation expressions for generic types with <c>new()</c> constraint.
		/// true: <c>T t = new T();</c>
		/// false: <c>T t = Activator.CreateInstance&lt;T&gt;()</c>
		/// </summary>
		[Category("C# 2.0 / VS 2005")]
		[Description("DecompilerSettings.UseObjectCreationOfGenericTypeParameter")]
		public bool UseObjectCreationOfGenericTypeParameter {
			get { return useObjectCreationOfGenericTypeParameter; }
			set {
				if (useObjectCreationOfGenericTypeParameter != value)
				{
					useObjectCreationOfGenericTypeParameter = value;
					OnPropertyChanged();
				}
			}
		}

		bool alwaysCastTargetsOfExplicitInterfaceImplementationCalls = false;

		/// <summary>
		/// Gets/Sets whether to always cast targets to explicitly implemented methods.
		/// true: <c>((ISupportInitialize)pictureBox1).BeginInit();</c>
		/// false: <c>pictureBox1.BeginInit();</c>
		/// default: false
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysCastTargetsOfExplicitInterfaceImplementationCalls")]
		public bool AlwaysCastTargetsOfExplicitInterfaceImplementationCalls {
			get { return alwaysCastTargetsOfExplicitInterfaceImplementationCalls; }
			set {
				if (alwaysCastTargetsOfExplicitInterfaceImplementationCalls != value)
				{
					alwaysCastTargetsOfExplicitInterfaceImplementationCalls = value;
					OnPropertyChanged();
				}
			}
		}

		bool alwaysQualifyMemberReferences = false;

		/// <summary>
		/// Gets/Sets whether to always qualify member references.
		/// true: <c>this.DoSomething();</c>
		/// false: <c>DoSomething();</c>
		/// default: false
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysQualifyMemberReferences")]
		public bool AlwaysQualifyMemberReferences {
			get { return alwaysQualifyMemberReferences; }
			set {
				if (alwaysQualifyMemberReferences != value)
				{
					alwaysQualifyMemberReferences = value;
					OnPropertyChanged();
				}
			}
		}

		bool alwaysShowEnumMemberValues = false;

		/// <summary>
		/// Gets/Sets whether to always show enum member values.
		/// true: <c>enum Kind { A = 0, B = 1, C = 5 }</c>
		/// false: <c>enum Kind { A, B, C = 5 }</c>
		/// default: false
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysShowEnumMemberValues")]
		public bool AlwaysShowEnumMemberValues {
			get { return alwaysShowEnumMemberValues; }
			set {
				if (alwaysShowEnumMemberValues != value)
				{
					alwaysShowEnumMemberValues = value;
					OnPropertyChanged();
				}
			}
		}

		bool useDebugSymbols = true;

		/// <summary>
		/// Gets/Sets whether to use variable names from debug symbols, if available.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.UseVariableNamesFromDebugSymbolsIfAvailable")]
		public bool UseDebugSymbols {
			get { return useDebugSymbols; }
			set {
				if (useDebugSymbols != value)
				{
					useDebugSymbols = value;
					OnPropertyChanged();
				}
			}
		}

		bool arrayInitializers = true;

		/// <summary>
		/// Gets/Sets whether to use array initializers.
		/// If set to false, might produce non-compilable code.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.ArrayInitializerExpressions")]
		public bool ArrayInitializers {
			get { return arrayInitializers; }
			set {
				if (arrayInitializers != value)
				{
					arrayInitializers = value;
					OnPropertyChanged();
				}
			}
		}

		bool objectCollectionInitializers = true;

		/// <summary>
		/// Gets/Sets whether to use C# 3.0 object/collection initializers.
		/// </summary>
		[Category("C# 3.0 / VS 2008")]
		[Description("DecompilerSettings.ObjectCollectionInitializerExpressions")]
		public bool ObjectOrCollectionInitializers {
			get { return objectCollectionInitializers; }
			set {
				if (objectCollectionInitializers != value)
				{
					objectCollectionInitializers = value;
					OnPropertyChanged();
				}
			}
		}

		bool dictionaryInitializers = true;

		/// <summary>
		/// Gets/Sets whether to use C# 6.0 dictionary initializers.
		/// Only has an effect if ObjectOrCollectionInitializers is enabled.
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.DictionaryInitializerExpressions")]
		public bool DictionaryInitializers {
			get { return dictionaryInitializers; }
			set {
				if (dictionaryInitializers != value)
				{
					dictionaryInitializers = value;
					OnPropertyChanged();
				}
			}
		}

		bool extensionMethodsInCollectionInitializers = true;

		/// <summary>
		/// Gets/Sets whether to use C# 6.0 Extension Add methods in collection initializers.
		/// Only has an effect if ObjectOrCollectionInitializers is enabled.
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.AllowExtensionAddMethodsInCollectionInitializerExpressions")]
		public bool ExtensionMethodsInCollectionInitializers {
			get { return extensionMethodsInCollectionInitializers; }
			set {
				if (extensionMethodsInCollectionInitializers != value)
				{
					extensionMethodsInCollectionInitializers = value;
					OnPropertyChanged();
				}
			}
		}

		bool useRefLocalsForAccurateOrderOfEvaluation = true;

		/// <summary>
		/// Gets/Sets whether to use local ref variables in cases where this is necessary
		/// for re-compilation with a modern C# compiler to reproduce the same behavior
		/// as the original assembly produced with an old C# compiler that used an incorrect
		/// order of evaluation.
		/// See https://github.com/icsharpcode/ILSpy/issues/2050
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.UseRefLocalsForAccurateOrderOfEvaluation")]
		public bool UseRefLocalsForAccurateOrderOfEvaluation {
			get { return useRefLocalsForAccurateOrderOfEvaluation; }
			set {
				if (useRefLocalsForAccurateOrderOfEvaluation != value)
				{
					useRefLocalsForAccurateOrderOfEvaluation = value;
					OnPropertyChanged();
				}
			}
		}

		bool refExtensionMethods = true;

		/// <summary>
		/// Gets/Sets whether to use C# 7.2 'ref' extension methods.
		/// </summary>
		[Category("C# 7.2 / VS 2017.4")]
		[Description("DecompilerSettings.AllowExtensionMethodSyntaxOnRef")]
		public bool RefExtensionMethods {
			get { return refExtensionMethods; }
			set {
				if (refExtensionMethods != value)
				{
					refExtensionMethods = value;
					OnPropertyChanged();
				}
			}
		}

		bool stringInterpolation = true;

		/// <summary>
		/// Gets/Sets whether to use C# 6.0 string interpolation
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.UseStringInterpolation")]
		public bool StringInterpolation {
			get { return stringInterpolation; }
			set {
				if (stringInterpolation != value)
				{
					stringInterpolation = value;
					OnPropertyChanged();
				}
			}
		}

		bool utf8StringLiterals = true;

		/// <summary>
		/// Gets/Sets whether to use C# 11.0 UTF-8 string literals
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.Utf8StringLiterals")]
		public bool Utf8StringLiterals {
			get { return utf8StringLiterals; }
			set {
				if (utf8StringLiterals != value)
				{
					utf8StringLiterals = value;
					OnPropertyChanged();
				}
			}
		}

		bool switchOnReadOnlySpanChar = true;

		/// <summary>
		/// Gets/Sets whether to use C# 11.0 switch on (ReadOnly)Span&lt;char&gt;
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.SwitchOnReadOnlySpanChar")]
		public bool SwitchOnReadOnlySpanChar {
			get { return switchOnReadOnlySpanChar; }
			set {
				if (switchOnReadOnlySpanChar != value)
				{
					switchOnReadOnlySpanChar = value;
					OnPropertyChanged();
				}
			}
		}

		bool unsignedRightShift = true;

		/// <summary>
		/// Gets/Sets whether to use C# 11.0 unsigned right shift operator.
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.UnsignedRightShift")]
		public bool UnsignedRightShift {
			get { return unsignedRightShift; }
			set {
				if (unsignedRightShift != value)
				{
					unsignedRightShift = value;
					OnPropertyChanged();
				}
			}
		}

		bool checkedOperators = true;

		/// <summary>
		/// Gets/Sets whether to use C# 11.0 user-defined checked operators.
		/// </summary>
		[Category("C# 11.0 / VS 2022.4")]
		[Description("DecompilerSettings.CheckedOperators")]
		public bool CheckedOperators {
			get { return checkedOperators; }
			set {
				if (checkedOperators != value)
				{
					checkedOperators = value;
					OnPropertyChanged();
				}
			}
		}

		bool showXmlDocumentation = true;

		/// <summary>
		/// Gets/Sets whether to include XML documentation comments in the decompiled code.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.IncludeXMLDocumentationCommentsInTheDecompiledCode")]
		public bool ShowXmlDocumentation {
			get { return showXmlDocumentation; }
			set {
				if (showXmlDocumentation != value)
				{
					showXmlDocumentation = value;
					OnPropertyChanged();
				}
			}
		}

		bool decompileMemberBodies = true;

		/// <summary>
		/// Gets/Sets whether member bodies should be decompiled.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Browsable(false)]
		public bool DecompileMemberBodies {
			get { return decompileMemberBodies; }
			set {
				if (decompileMemberBodies != value)
				{
					decompileMemberBodies = value;
					OnPropertyChanged();
				}
			}
		}

		bool useExpressionBodyForCalculatedGetterOnlyProperties = true;

		/// <summary>
		/// Gets/Sets whether simple calculated getter-only property declarations
		/// should use expression body syntax.
		/// </summary>
		[Category("C# 6.0 / VS 2015")]
		[Description("DecompilerSettings.UseExpressionBodiedMemberSyntaxForGetOnlyProperties")]
		public bool UseExpressionBodyForCalculatedGetterOnlyProperties {
			get { return useExpressionBodyForCalculatedGetterOnlyProperties; }
			set {
				if (useExpressionBodyForCalculatedGetterOnlyProperties != value)
				{
					useExpressionBodyForCalculatedGetterOnlyProperties = value;
					OnPropertyChanged();
				}
			}
		}

		bool outVariables = true;

		/// <summary>
		/// Gets/Sets whether out variable declarations should be used when possible.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.UseOutVariableDeclarations")]
		public bool OutVariables {
			get { return outVariables; }
			set {
				if (outVariables != value)
				{
					outVariables = value;
					OnPropertyChanged();
				}
			}
		}

		bool discards = true;

		/// <summary>
		/// Gets/Sets whether discards should be used when possible.
		/// Only has an effect if <see cref="OutVariables"/> is enabled.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.UseDiscards")]
		public bool Discards {
			get { return discards; }
			set {
				if (discards != value)
				{
					discards = value;
					OnPropertyChanged();
				}
			}
		}

		bool introduceRefModifiersOnStructs = true;

		/// <summary>
		/// Gets/Sets whether IsByRefLikeAttribute should be replaced with 'ref' modifiers on structs.
		/// </summary>
		[Category("C# 7.2 / VS 2017.4")]
		[Description("DecompilerSettings.IsByRefLikeAttributeShouldBeReplacedWithRefModifiersOnStructs")]
		public bool IntroduceRefModifiersOnStructs {
			get { return introduceRefModifiersOnStructs; }
			set {
				if (introduceRefModifiersOnStructs != value)
				{
					introduceRefModifiersOnStructs = value;
					OnPropertyChanged();
				}
			}
		}

		bool introduceReadonlyAndInModifiers = true;

		/// <summary>
		/// Gets/Sets whether IsReadOnlyAttribute should be replaced with 'readonly' modifiers on structs
		/// and with the 'in' modifier on parameters.
		/// </summary>
		[Category("C# 7.2 / VS 2017.4")]
		[Description("DecompilerSettings." +
			"IsReadOnlyAttributeShouldBeReplacedWithReadonlyInModifiersOnStructsParameters")]
		public bool IntroduceReadonlyAndInModifiers {
			get { return introduceReadonlyAndInModifiers; }
			set {
				if (introduceReadonlyAndInModifiers != value)
				{
					introduceReadonlyAndInModifiers = value;
					OnPropertyChanged();
				}
			}
		}

		bool introducePrivateProtectedAccessibilty = true;

		/// <summary>
		/// Gets/Sets whether "private protected" should be used.
		/// </summary>
		[Category("C# 7.2 / VS 2017.4")]
		[Description("DecompilerSettings.IntroducePrivateProtectedAccessibility")]
		public bool IntroducePrivateProtectedAccessibility {
			get { return introducePrivateProtectedAccessibilty; }
			set {
				if (introducePrivateProtectedAccessibilty != value)
				{
					introducePrivateProtectedAccessibilty = value;
					OnPropertyChanged();
				}
			}
		}

		bool readOnlyMethods = true;

		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.ReadOnlyMethods")]
		public bool ReadOnlyMethods {
			get { return readOnlyMethods; }
			set {
				if (readOnlyMethods != value)
				{
					readOnlyMethods = value;
					OnPropertyChanged();
				}
			}
		}

		bool asyncUsingAndForEachStatement = true;

		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.DetectAsyncUsingAndForeachStatements")]
		public bool AsyncUsingAndForEachStatement {
			get { return asyncUsingAndForEachStatement; }
			set {
				if (asyncUsingAndForEachStatement != value)
				{
					asyncUsingAndForEachStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool introduceUnmanagedConstraint = true;

		/// <summary>
		/// If this option is active, [IsUnmanagedAttribute] on type parameters
		/// is replaced with "T : unmanaged" constraints.
		/// </summary>
		[Category("C# 7.3 / VS 2017.7")]
		[Description("DecompilerSettings." +
			"IsUnmanagedAttributeOnTypeParametersShouldBeReplacedWithUnmanagedConstraints")]
		public bool IntroduceUnmanagedConstraint {
			get { return introduceUnmanagedConstraint; }
			set {
				if (introduceUnmanagedConstraint != value)
				{
					introduceUnmanagedConstraint = value;
					OnPropertyChanged();
				}
			}
		}

		bool stackAllocInitializers = true;

		/// <summary>
		/// Gets/Sets whether C# 7.3 stackalloc initializers should be used.
		/// </summary>
		[Category("C# 7.3 / VS 2017.7")]
		[Description("DecompilerSettings.UseStackallocInitializerSyntax")]
		public bool StackAllocInitializers {
			get { return stackAllocInitializers; }
			set {
				if (stackAllocInitializers != value)
				{
					stackAllocInitializers = value;
					OnPropertyChanged();
				}
			}
		}

		bool patternBasedFixedStatement = true;

		/// <summary>
		/// Gets/Sets whether C# 7.3 pattern based fixed statement should be used.
		/// </summary>
		[Category("C# 7.3 / VS 2017.7")]
		[Description("DecompilerSettings.UsePatternBasedFixedStatement")]
		public bool PatternBasedFixedStatement {
			get { return patternBasedFixedStatement; }
			set {
				if (patternBasedFixedStatement != value)
				{
					patternBasedFixedStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool tupleTypes = true;

		/// <summary>
		/// Gets/Sets whether tuple type syntax <c>(int, string)</c>
		/// should be used for <c>System.ValueTuple</c>.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.UseTupleTypeSyntax")]
		public bool TupleTypes {
			get { return tupleTypes; }
			set {
				if (tupleTypes != value)
				{
					tupleTypes = value;
					OnPropertyChanged();
				}
			}
		}

		bool throwExpressions = true;

		/// <summary>
		/// Gets/Sets whether throw expressions should be used.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.UseThrowExpressions")]
		public bool ThrowExpressions {
			get { return throwExpressions; }
			set {
				if (throwExpressions != value)
				{
					throwExpressions = value;
					OnPropertyChanged();
				}
			}
		}

		bool tupleConversions = true;

		/// <summary>
		/// Gets/Sets whether implicit conversions between tuples
		/// should be used in the decompiled output.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.UseImplicitConversionsBetweenTupleTypes")]
		public bool TupleConversions {
			get { return tupleConversions; }
			set {
				if (tupleConversions != value)
				{
					tupleConversions = value;
					OnPropertyChanged();
				}
			}
		}

		bool tupleComparisons = true;

		/// <summary>
		/// Gets/Sets whether tuple comparisons should be detected.
		/// </summary>
		[Category("C# 7.3 / VS 2017.7")]
		[Description("DecompilerSettings.DetectTupleComparisons")]
		public bool TupleComparisons {
			get { return tupleComparisons; }
			set {
				if (tupleComparisons != value)
				{
					tupleComparisons = value;
					OnPropertyChanged();
				}
			}
		}

		bool namedArguments = true;

		/// <summary>
		/// Gets/Sets whether named arguments should be used.
		/// </summary>
		[Category("C# 4.0 / VS 2010")]
		[Description("DecompilerSettings.UseNamedArguments")]
		public bool NamedArguments {
			get { return namedArguments; }
			set {
				if (namedArguments != value)
				{
					namedArguments = value;
					OnPropertyChanged();
				}
			}
		}

		bool nonTrailingNamedArguments = true;

		/// <summary>
		/// Gets/Sets whether C# 7.2 non-trailing named arguments should be used.
		/// </summary>
		[Category("C# 7.2 / VS 2017.4")]
		[Description("DecompilerSettings.UseNonTrailingNamedArguments")]
		public bool NonTrailingNamedArguments {
			get { return nonTrailingNamedArguments; }
			set {
				if (nonTrailingNamedArguments != value)
				{
					nonTrailingNamedArguments = value;
					OnPropertyChanged();
				}
			}
		}

		bool optionalArguments = true;

		/// <summary>
		/// Gets/Sets whether optional arguments should be removed, if possible.
		/// </summary>
		[Category("C# 4.0 / VS 2010")]
		[Description("DecompilerSettings.RemoveOptionalArgumentsIfPossible")]
		public bool OptionalArguments {
			get { return optionalArguments; }
			set {
				if (optionalArguments != value)
				{
					optionalArguments = value;
					OnPropertyChanged();
				}
			}
		}

		bool expandParamsArguments = true;

		/// <summary>
		/// Gets/Sets whether to expand <c>params</c> arguments by replacing explicit array creation
		/// with individual values in method calls.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.ExpandParamsArguments")]
		public bool ExpandParamsArguments {
			get { return expandParamsArguments; }
			set {
				if (expandParamsArguments != value)
				{
					expandParamsArguments = value;
					OnPropertyChanged();
				}
			}
		}

		bool localFunctions = true;

		/// <summary>
		/// Gets/Sets whether C# 7.0 local functions should be transformed.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.IntroduceLocalFunctions")]
		public bool LocalFunctions {
			get { return localFunctions; }
			set {
				if (localFunctions != value)
				{
					localFunctions = value;
					OnPropertyChanged();
				}
			}
		}

		bool deconstruction = true;

		/// <summary>
		/// Gets/Sets whether C# 7.0 deconstruction should be detected.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.Deconstruction")]
		public bool Deconstruction {
			get { return deconstruction; }
			set {
				if (deconstruction != value)
				{
					deconstruction = value;
					OnPropertyChanged();
				}
			}
		}

		bool patternMatching = true;

		/// <summary>
		/// Gets/Sets whether C# 7.0 pattern matching should be detected.
		/// </summary>
		[Category("C# 7.0 / VS 2017")]
		[Description("DecompilerSettings.PatternMatching")]
		public bool PatternMatching {
			get { return patternMatching; }
			set {
				if (patternMatching != value)
				{
					patternMatching = value;
					OnPropertyChanged();
				}
			}
		}

		bool recursivePatternMatching = true;

		/// <summary>
		/// Gets/Sets whether C# 8.0 recursive patterns should be detected.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.RecursivePatternMatching")]
		public bool RecursivePatternMatching {
			get { return recursivePatternMatching; }
			set {
				if (recursivePatternMatching != value)
				{
					recursivePatternMatching = value;
					OnPropertyChanged();
				}
			}
		}

		bool patternCombinators = true;

		/// <summary>
		/// Gets/Sets whether C# 9.0 and, or, not patterns should be detected.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.PatternCombinators")]
		public bool PatternCombinators {
			get { return patternCombinators; }
			set {
				if (patternCombinators != value)
				{
					patternCombinators = value;
					OnPropertyChanged();
				}
			}
		}

		bool relationalPatterns = true;

		/// <summary>
		/// Gets/Sets whether C# 9.0 relational patterns should be detected.
		/// </summary>
		[Category("C# 9.0 / VS 2019.8")]
		[Description("DecompilerSettings.RelationalPatterns")]
		public bool RelationalPatterns {
			get { return relationalPatterns; }
			set {
				if (relationalPatterns != value)
				{
					relationalPatterns = value;
					OnPropertyChanged();
				}
			}
		}

		bool staticLocalFunctions = true;

		/// <summary>
		/// Gets/Sets whether C# 8.0 static local functions should be transformed.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.IntroduceStaticLocalFunctions")]
		public bool StaticLocalFunctions {
			get { return staticLocalFunctions; }
			set {
				if (staticLocalFunctions != value)
				{
					staticLocalFunctions = value;
					OnPropertyChanged();
				}
			}
		}

		bool ranges = true;

		/// <summary>
		/// Gets/Sets whether C# 8.0 index and range syntax should be used.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.Ranges")]
		public bool Ranges {
			get { return ranges; }
			set {
				if (ranges != value)
				{
					ranges = value;
					OnPropertyChanged();
				}
			}
		}

		bool nullableReferenceTypes = true;

		/// <summary>
		/// Gets/Sets whether C# 8.0 nullable reference types are enabled.
		/// </summary>
		[Category("C# 8.0 / VS 2019")]
		[Description("DecompilerSettings.NullableReferenceTypes")]
		public bool NullableReferenceTypes {
			get { return nullableReferenceTypes; }
			set {
				if (nullableReferenceTypes != value)
				{
					nullableReferenceTypes = value;
					OnPropertyChanged();
				}
			}
		}

		#region Options to aid VB decompilation
		bool assumeArrayLengthFitsIntoInt32 = true;

		/// <summary>
		/// Gets/Sets whether the decompiler can assume that 'ldlen; conv.i4.ovf'
		/// does not throw an overflow exception.
		/// </summary>
		[Category("DecompilerSettings.VBSpecificOptions")]
		[Browsable(false)]
		public bool AssumeArrayLengthFitsIntoInt32 {
			get { return assumeArrayLengthFitsIntoInt32; }
			set {
				if (assumeArrayLengthFitsIntoInt32 != value)
				{
					assumeArrayLengthFitsIntoInt32 = value;
					OnPropertyChanged();
				}
			}
		}

		bool introduceIncrementAndDecrement = true;

		/// <summary>
		/// Gets/Sets whether to use increment and decrement operators
		/// </summary>
		[Category("DecompilerSettings.VBSpecificOptions")]
		[Browsable(false)]
		public bool IntroduceIncrementAndDecrement {
			get { return introduceIncrementAndDecrement; }
			set {
				if (introduceIncrementAndDecrement != value)
				{
					introduceIncrementAndDecrement = value;
					OnPropertyChanged();
				}
			}
		}

		bool makeAssignmentExpressions = true;

		/// <summary>
		/// Gets/Sets whether to use assignment expressions such as in while ((count = Do()) != 0) ;
		/// </summary>
		[Category("DecompilerSettings.VBSpecificOptions")]
		[Browsable(false)]
		public bool MakeAssignmentExpressions {
			get { return makeAssignmentExpressions; }
			set {
				if (makeAssignmentExpressions != value)
				{
					makeAssignmentExpressions = value;
					OnPropertyChanged();
				}
			}
		}

		#endregion

		#region Options to aid F# decompilation
		bool removeDeadCode = false;

		[Category("DecompilerSettings.FSpecificOptions")]
		[Description("DecompilerSettings.RemoveDeadAndSideEffectFreeCodeUseWithCaution")]
		public bool RemoveDeadCode {
			get { return removeDeadCode; }
			set {
				if (removeDeadCode != value)
				{
					removeDeadCode = value;
					OnPropertyChanged();
				}
			}
		}

		bool removeDeadStores = false;

		[Category("DecompilerSettings.FSpecificOptions")]
		[Description("DecompilerSettings.RemoveDeadStores")]
		public bool RemoveDeadStores {
			get { return removeDeadStores; }
			set {
				if (removeDeadStores != value)
				{
					removeDeadStores = value;
					OnPropertyChanged();
				}
			}
		}
		#endregion

		bool forStatement = true;

		/// <summary>
		/// Gets/sets whether the decompiler should produce for loops.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.ForStatement")]
		public bool ForStatement {
			get { return forStatement; }
			set {
				if (forStatement != value)
				{
					forStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool doWhileStatement = true;

		/// <summary>
		/// Gets/sets whether the decompiler should produce do-while loops.
		/// </summary>
		[Category("C# 1.0 / VS .NET")]
		[Description("DecompilerSettings.DoWhileStatement")]
		public bool DoWhileStatement {
			get { return doWhileStatement; }
			set {
				if (doWhileStatement != value)
				{
					doWhileStatement = value;
					OnPropertyChanged();
				}
			}
		}

		bool refReadOnlyParameters = true;

		/// <summary>
		/// Gets/sets whether RequiresLocationAttribute on parameters should be replaced with 'ref readonly' modifiers.
		/// </summary>
		[Category("C# 12.0 / VS 2022.8")]
		[Description("DecompilerSettings.RefReadOnlyParameters")]
		public bool RefReadOnlyParameters {
			get { return refReadOnlyParameters; }
			set {
				if (refReadOnlyParameters != value)
				{
					refReadOnlyParameters = value;
					OnPropertyChanged();
				}
			}
		}

		bool usePrimaryConstructorSyntaxForNonRecordTypes = true;

		/// <summary>
		/// Use primary constructor syntax with classes and structs.
		/// </summary>
		[Category("C# 12.0 / VS 2022.8")]
		[Description("DecompilerSettings.UsePrimaryConstructorSyntaxForNonRecordTypes")]
		public bool UsePrimaryConstructorSyntaxForNonRecordTypes {
			get { return usePrimaryConstructorSyntaxForNonRecordTypes; }
			set {
				if (usePrimaryConstructorSyntaxForNonRecordTypes != value)
				{
					usePrimaryConstructorSyntaxForNonRecordTypes = value;
					OnPropertyChanged();
				}
			}
		}

		bool inlineArrays = true;

		/// <summary>
		/// Gets/Sets whether C# 12.0 inline array uses should be transformed.
		/// </summary>
		[Category("C# 12.0 / VS 2022.8")]
		[Description("DecompilerSettings.InlineArrays")]
		public bool InlineArrays {
			get { return inlineArrays; }
			set {
				if (inlineArrays != value)
				{
					inlineArrays = value;
					OnPropertyChanged();
				}
			}
		}

		bool extensionMembers = true;

		/// <summary>
		/// Gets/Sets whether C# 14.0 extension members should be transformed.
		/// </summary>
		[Category("C# 14.0 / VS 202x.yy")]
		[Description("DecompilerSettings.ExtensionMembers")]
		public bool ExtensionMembers {
			get { return extensionMembers; }
			set {
				if (extensionMembers != value)
				{
					extensionMembers = value;
					OnPropertyChanged();
				}
			}
		}

		bool firstClassSpanTypes = true;

		/// <summary>
		/// Gets/Sets whether (ReadOnly)Span&lt;T&gt; should be treated like built-in types.
		/// </summary>
		[Category("C# 14.0 / VS 202x.yy")]
		[Description("DecompilerSettings.FirstClassSpanTypes")]
		public bool FirstClassSpanTypes {
			get { return firstClassSpanTypes; }
			set {
				if (firstClassSpanTypes != value)
				{
					firstClassSpanTypes = value;
					OnPropertyChanged();
				}
			}
		}

		bool separateLocalVariableDeclarations = false;

		/// <summary>
		/// Gets/sets whether the decompiler should separate local variable declarations
		/// from their initialization.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.SeparateLocalVariableDeclarations")]
		public bool SeparateLocalVariableDeclarations {
			get { return separateLocalVariableDeclarations; }
			set {
				if (separateLocalVariableDeclarations != value)
				{
					separateLocalVariableDeclarations = value;
					OnPropertyChanged();
				}
			}
		}

		bool aggressiveScalarReplacementOfAggregates = false;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AggressiveScalarReplacementOfAggregates")]
		// TODO : Remove once https://github.com/icsharpcode/ILSpy/issues/2032 is fixed.
#if !DEBUG
		[Browsable(false)]
#endif
		public bool AggressiveScalarReplacementOfAggregates {
			get { return aggressiveScalarReplacementOfAggregates; }
			set {
				if (aggressiveScalarReplacementOfAggregates != value)
				{
					aggressiveScalarReplacementOfAggregates = value;
					OnPropertyChanged();
				}
			}
		}

		bool aggressiveInlining = false;

		/// <summary>
		/// If set to false (the default), the decompiler will inline local variables only when they occur
		/// in a context where the C# compiler is known to emit compiler-generated locals.
		/// If set to true, the decompiler will inline local variables whenever possible.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AggressiveInlining")]
		public bool AggressiveInlining {
			get { return aggressiveInlining; }
			set {
				if (aggressiveInlining != value)
				{
					aggressiveInlining = value;
					OnPropertyChanged();
				}
			}
		}

		bool alwaysUseGlobal = false;

		/// <summary>
		/// Always fully qualify namespaces using the "global::" prefix.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysUseGlobal")]
		public bool AlwaysUseGlobal {
			get { return alwaysUseGlobal; }
			set {
				if (alwaysUseGlobal != value)
				{
					alwaysUseGlobal = value;
					OnPropertyChanged();
				}
			}
		}

		bool removeEmptyDefaultConstructors = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.RemoveEmptyDefaultConstructors")]
		public bool RemoveEmptyDefaultConstructors {
			get { return removeEmptyDefaultConstructors; }
			set {
				if (removeEmptyDefaultConstructors != value) {
					removeEmptyDefaultConstructors = value;
					OnPropertyChanged();
				}
			}
		}

		bool alwaysMoveInitializer = false;

		/// <summary>
		/// If set to false (the default), the decompiler will move field initializers at the start of constructors
		/// to their respective field declarations (TransformFieldAndConstructorInitializers) only when the declaring
		/// type has BeforeFieldInit or the member IsConst.
		/// If set true, the decompiler will always move them regardless of the flags.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysMoveInitializer")]
		public bool AlwaysMoveInitializer {
			get { return alwaysMoveInitializer; }
			set {
				if (alwaysMoveInitializer != value)
				{
					alwaysMoveInitializer = value;
					OnPropertyChanged();
				}
			}
		}

		bool showTokenAndRvaComments = true;

		/// <summary>
		/// Gets/sets whether to show tokens of types/methods/etc and the RVA / file offset in comments
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.ShowTokenAndRvaComments")]
		public bool ShowTokenAndRvaComments {
			get { return showTokenAndRvaComments; }
			set {
				if (showTokenAndRvaComments != value) {
					showTokenAndRvaComments = value;
					OnPropertyChanged();
				}
			}
		}

		bool sortMembers = false;

		/// <summary>
		/// Gets/sets whether to sort members
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.SortMembers")]
		public bool SortMembers {
			get { return sortMembers; }
			set {
				if (sortMembers != value) {
					sortMembers = value;
					OnPropertyChanged();
				}
			}
		}

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.ForceShowAllMembers")]
		public bool ForceShowAllMembers {
			get { return forceShowAllMembers; }
			set {
				if (forceShowAllMembers != value) {
					forceShowAllMembers = value;
					OnPropertyChanged();
				}
			}
		}
		bool forceShowAllMembers = false;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.SortSystemUsingStatementsFirst")]
		public bool SortSystemUsingStatementsFirst {
			get { return sortSystemUsingStatementsFirst; }
			set {
				if (sortSystemUsingStatementsFirst != value) {
					sortSystemUsingStatementsFirst = value;
					OnPropertyChanged();
				}
			}
		}
		bool sortSystemUsingStatementsFirst = true;

		public int MaxArrayElements {
			get { return maxArrayElements; }
			set {
				if (maxArrayElements != value) {
					maxArrayElements = value;
					OnPropertyChanged();
				}
			}
		}
		// Don't show too big arrays, no-one will read every single element, and too big
		// arrays could cause OOM exceptions.
		int maxArrayElements = 10000;

		public int MaxStringLength {
			get { return maxStringLength; }
			set {
				if (maxStringLength != value) {
					maxStringLength = value;
					OnPropertyChanged();
				}
			}
		}
		int maxStringLength = ConstMaxStringLength;
		public const int ConstMaxStringLength = 20000;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.SortCustomAttributes")]
		public bool SortCustomAttributes {
			get { return sortCustomAttributes; }
			set {
				if (sortCustomAttributes != value) {
					sortCustomAttributes = value;
					OnPropertyChanged();
				}
			}
		}
		bool sortCustomAttributes = false;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.UseSourceCodeOrder")]
		public bool UseSourceCodeOrder {
			get { return useSourceCodeOrder; }
			set {
				if (useSourceCodeOrder != value) {
					useSourceCodeOrder = value;
					OnPropertyChanged();
				}
			}
		}
		bool useSourceCodeOrder = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AllowFieldInitializers")]
		public bool AllowFieldInitializers {
			get { return allowFieldInitializers; }
			set {
				if (allowFieldInitializers != value) {
					allowFieldInitializers = value;
					OnPropertyChanged();
				}
			}
		}
		bool allowFieldInitializers = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.OneCustomAttributePerLine")]
		public bool OneCustomAttributePerLine {
			get { return oneCustomAttributePerLine; }
			set {
				if (oneCustomAttributePerLine != value) {
					oneCustomAttributePerLine = value;
					OnPropertyChanged();
				}
			}
		}
		bool oneCustomAttributePerLine = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.TypeAddInternalModifier")]
		public bool TypeAddInternalModifier {
			get { return typeAddInternalModifier; }
			set {
				if (typeAddInternalModifier != value) {
					typeAddInternalModifier = value;
					OnPropertyChanged();
				}
			}
		}
		bool typeAddInternalModifier = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.MemberAddPrivateModifier")]
		public bool MemberAddPrivateModifier {
			get { return memberAddPrivateModifier; }
			set {
				if (memberAddPrivateModifier != value) {
					memberAddPrivateModifier = value;
					OnPropertyChanged();
				}
			}
		}
		bool memberAddPrivateModifier = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.HexadecimalNumbers")]
		public bool HexadecimalNumbers {
			get { return hexadecimalNumbers; }
			set {
				if (hexadecimalNumbers != value) {
					hexadecimalNumbers = value;
					OnPropertyChanged();
				}
			}
		}
		bool hexadecimalNumbers = false;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.SortSwitchCasesByILOffset")]
		public bool SortSwitchCasesByILOffset {
			get { return sortSwitchCasesByILOffset; }
			set {
				if (sortSwitchCasesByILOffset != value) {
					sortSwitchCasesByILOffset = value;
					OnPropertyChanged();
				}
			}
		}
		bool sortSwitchCasesByILOffset = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.InsertParenthesesForReadability")]
		public bool InsertParenthesesForReadability {
			get { return insertParenthesesForReadability; }
			set {
				if (insertParenthesesForReadability != value) {
					insertParenthesesForReadability = value;
					OnPropertyChanged();
				}
			}
		}
		bool insertParenthesesForReadability = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.FullyQualifyAmbiguousTypeNames")]
		public bool FullyQualifyAmbiguousTypeNames {
			get { return fullyQualifyAmbiguousTypeNames; }
			set {
				if (fullyQualifyAmbiguousTypeNames != value) {
					fullyQualifyAmbiguousTypeNames = value;
					OnPropertyChanged();
				}
			}
		}
		bool fullyQualifyAmbiguousTypeNames = true;

		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.FullyQualifyAllTypes")]
		public bool FullyQualifyAllTypes {
			get { return fullyQualifyAllTypes; }
			set {
				if (fullyQualifyAllTypes != value) {
					fullyQualifyAllTypes = value;
					OnPropertyChanged();
				}
			}
		}
		bool fullyQualifyAllTypes = false;

		/// <summary>
		/// Gets/Sets whether to always generate exception variables in catch blocks
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.AlwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject")]
		public bool AlwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject {
			get { return alwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject; }
			set {
				if (alwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject != value) {
					alwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject = value;
					OnPropertyChanged();
				}
			}
		}
		bool alwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject = false;

		bool checkForOverflowUnderflow = false;

		/// <summary>
		/// Check for overflow and underflow in operators.
		/// </summary>
		[Category("DecompilerSettings.Other")]
		[Description("DecompilerSettings.CheckForOverflowUnderflow")]
		public bool CheckForOverflowUnderflow {
			get { return checkForOverflowUnderflow; }
			set {
				if (checkForOverflowUnderflow != value)
				{
					checkForOverflowUnderflow = value;
					OnPropertyChanged();
				}
			}
		}

		CSharpFormattingOptions csharpFormattingOptions;

		[Browsable(false)]
		public CSharpFormattingOptions CSharpFormattingOptions {
			get {
				if (csharpFormattingOptions == null)
				{
					csharpFormattingOptions = FormattingOptionsFactory.CreateAllman();
					csharpFormattingOptions.IndentSwitchBody = false;
					csharpFormattingOptions.ArrayInitializerWrapping = Wrapping.WrapIfTooLong;
					csharpFormattingOptions.AutoPropertyFormatting = PropertyFormatting.SingleLine;
				}
				return csharpFormattingOptions;
			}
			set {
				if (value == null)
					throw new ArgumentNullException();
				if (csharpFormattingOptions != value)
				{
					csharpFormattingOptions = value;
					OnPropertyChanged();
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		public event EventHandler SettingsVersionChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			Interlocked.Increment(ref settingsVersion);
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			OnModified();
			SettingsVersionChanged?.Invoke(this, EventArgs.Empty);
		}

		public int SettingsVersion => settingsVersion;
		volatile int settingsVersion;

		public DecompilerSettings Clone() {
			// DON'T use MemberwiseClone() since we want to return a DecompilerSettings, not any
			// derived class.
			return CopyTo(new DecompilerSettings());
		}

		public bool Equals(DecompilerSettings other) {
			if (other == null)
				return false;

			if (DecompilationObject0 != other.DecompilationObject0) return false;
			if (DecompilationObject1 != other.DecompilationObject1) return false;
			if (DecompilationObject2 != other.DecompilationObject2) return false;
			if (DecompilationObject3 != other.DecompilationObject3) return false;
			if (DecompilationObject4 != other.DecompilationObject4) return false;
			if (NativeIntegers != other.NativeIntegers) return false;
			if (NumericIntPtr != other.NumericIntPtr) return false;
			if (CovariantReturns != other.CovariantReturns) return false;
			if (InitAccessors != other.InitAccessors) return false;
			if (RecordClasses != other.RecordClasses) return false;
			if (RecordStructs != other.RecordStructs) return false;
			if (StructDefaultConstructorsAndFieldInitializers != other.StructDefaultConstructorsAndFieldInitializers) return false;
			if (WithExpressions != other.WithExpressions) return false;
			if (UsePrimaryConstructorSyntax != other.UsePrimaryConstructorSyntax) return false;
			if (FunctionPointers != other.FunctionPointers) return false;
			if (ScopedRef != other.ScopedRef) return false;
			if (RequiredMembers != other.RequiredMembers) return false;
			if (SwitchExpressions != other.SwitchExpressions) return false;
			if (FileScopedNamespaces != other.FileScopedNamespaces) return false;
			if (AnonymousMethods != other.AnonymousMethods) return false;
			if (AnonymousTypes != other.AnonymousTypes) return false;
			if (UseLambdaSyntax != other.UseLambdaSyntax) return false;
			if (ExpressionTrees != other.ExpressionTrees) return false;
			if (YieldReturn != other.YieldReturn) return false;
			if (Dynamic != other.Dynamic) return false;
			if (AsyncAwait != other.AsyncAwait) return false;
			if (AwaitInCatchFinally != other.AwaitInCatchFinally) return false;
			if (AsyncEnumerator != other.AsyncEnumerator) return false;
			if (DecimalConstants != other.DecimalConstants) return false;
			if (FixedBuffers != other.FixedBuffers) return false;
			if (StringConcat != other.StringConcat) return false;
			if (LiftNullables != other.LiftNullables) return false;
			if (NullPropagation != other.NullPropagation) return false;
			if (AutomaticProperties != other.AutomaticProperties) return false;
			if (GetterOnlyAutomaticProperties != other.GetterOnlyAutomaticProperties) return false;
			if (AutomaticEvents != other.AutomaticEvents) return false;
			if (UsingStatement != other.UsingStatement) return false;
			if (UseEnhancedUsing != other.UseEnhancedUsing) return false;
			if (AlwaysUseBraces != other.AlwaysUseBraces) return false;
			if (ForEachStatement != other.ForEachStatement) return false;
			if (ForEachWithGetEnumeratorExtension != other.ForEachWithGetEnumeratorExtension) return false;
			if (ParamsCollections != other.ParamsCollections) return false;
			if (LockStatement != other.LockStatement) return false;
			if (SwitchStatementOnString != other.SwitchStatementOnString) return false;
			if (SparseIntegerSwitch != other.SparseIntegerSwitch) return false;
			if (UsingDeclarations != other.UsingDeclarations) return false;
			if (ExtensionMethods != other.ExtensionMethods) return false;
			if (QueryExpressions != other.QueryExpressions) return false;
			if (UseImplicitMethodGroupConversion != other.UseImplicitMethodGroupConversion) return false;
			if (UseObjectCreationOfGenericTypeParameter != other.UseObjectCreationOfGenericTypeParameter) return false;
			if (AlwaysCastTargetsOfExplicitInterfaceImplementationCalls != other.AlwaysCastTargetsOfExplicitInterfaceImplementationCalls) return false;
			if (AlwaysQualifyMemberReferences != other.AlwaysQualifyMemberReferences) return false;
			if (AlwaysShowEnumMemberValues != other.AlwaysShowEnumMemberValues) return false;
			if (UseDebugSymbols != other.UseDebugSymbols) return false;
			if (ArrayInitializers != other.ArrayInitializers) return false;
			if (ObjectOrCollectionInitializers != other.ObjectOrCollectionInitializers) return false;
			if (DictionaryInitializers != other.DictionaryInitializers) return false;
			if (ExtensionMethodsInCollectionInitializers != other.ExtensionMethodsInCollectionInitializers) return false;
			if (UseRefLocalsForAccurateOrderOfEvaluation != other.UseRefLocalsForAccurateOrderOfEvaluation) return false;
			if (RefExtensionMethods != other.RefExtensionMethods) return false;
			if (StringInterpolation != other.StringInterpolation) return false;
			if (Utf8StringLiterals != other.Utf8StringLiterals) return false;
			if (UnsignedRightShift != other.UnsignedRightShift) return false;
			if (CheckedOperators != other.CheckedOperators) return false;
			if (ShowXmlDocumentation != other.ShowXmlDocumentation) return false;
			if (DecompileMemberBodies != other.DecompileMemberBodies) return false;
			if (UseExpressionBodyForCalculatedGetterOnlyProperties != other.UseExpressionBodyForCalculatedGetterOnlyProperties) return false;
			if (OutVariables != other.OutVariables) return false;
			if (Discards != other.Discards) return false;
			if (IntroduceRefModifiersOnStructs != other.IntroduceRefModifiersOnStructs) return false;
			if (IntroduceReadonlyAndInModifiers != other.IntroduceReadonlyAndInModifiers) return false;
			if (ReadOnlyMethods != other.ReadOnlyMethods) return false;
			if (AsyncUsingAndForEachStatement != other.AsyncUsingAndForEachStatement) return false;
			if (IntroduceUnmanagedConstraint != other.IntroduceUnmanagedConstraint) return false;
			if (StackAllocInitializers != other.StackAllocInitializers) return false;
			if (PatternBasedFixedStatement != other.PatternBasedFixedStatement) return false;
			if (TupleTypes != other.TupleTypes) return false;
			if (ThrowExpressions != other.ThrowExpressions) return false;
			if (TupleConversions != other.TupleConversions) return false;
			if (TupleComparisons != other.TupleComparisons) return false;
			if (NamedArguments != other.NamedArguments) return false;
			if (NonTrailingNamedArguments != other.NonTrailingNamedArguments) return false;
			if (OptionalArguments != other.OptionalArguments) return false;
			if (ExpandParamsArguments != other.ExpandParamsArguments) return false;
			if (LocalFunctions != other.LocalFunctions) return false;
			if (Deconstruction != other.Deconstruction) return false;
			if (PatternMatching != other.PatternMatching) return false;
			if (StaticLocalFunctions != other.StaticLocalFunctions) return false;
			if (Ranges != other.Ranges) return false;
			if (NullableReferenceTypes != other.NullableReferenceTypes) return false;
			if (AssumeArrayLengthFitsIntoInt32 != other.AssumeArrayLengthFitsIntoInt32) return false;
			if (IntroduceIncrementAndDecrement != other.IntroduceIncrementAndDecrement) return false;
			if (MakeAssignmentExpressions != other.MakeAssignmentExpressions) return false;
			if (RemoveDeadCode != other.RemoveDeadCode) return false;
			if (RemoveDeadStores != other.RemoveDeadStores) return false;
			if (ForStatement != other.ForStatement) return false;
			if (DoWhileStatement != other.DoWhileStatement) return false;
			if (RefReadOnlyParameters != other.RefReadOnlyParameters) return false;
			if (UsePrimaryConstructorSyntaxForNonRecordTypes != other.UsePrimaryConstructorSyntaxForNonRecordTypes) return false;
			if (InlineArrays != other.InlineArrays) return false;
			if (ExtensionMembers != other.ExtensionMembers) return false;
			if (FirstClassSpanTypes != other.FirstClassSpanTypes) return false;
			if (SeparateLocalVariableDeclarations != other.SeparateLocalVariableDeclarations) return false;
			if (AggressiveScalarReplacementOfAggregates != other.AggressiveScalarReplacementOfAggregates) return false;
			if (AggressiveInlining != other.AggressiveInlining) return false;
			if (AlwaysUseGlobal != other.AlwaysUseGlobal) return false;
			if (RemoveEmptyDefaultConstructors != other.RemoveEmptyDefaultConstructors) return false;
			if (AlwaysMoveInitializer != other.AlwaysMoveInitializer) return false;
			if (TypeAddInternalModifier != other.TypeAddInternalModifier) return false;
			if (MemberAddPrivateModifier != other.MemberAddPrivateModifier) return false;
			if (HexadecimalNumbers != other.HexadecimalNumbers) return false;
			if (SortSwitchCasesByILOffset != other.SortSwitchCasesByILOffset) return false;
			if (InsertParenthesesForReadability != other.InsertParenthesesForReadability) return false;
			if (FullyQualifyAmbiguousTypeNames != other.FullyQualifyAmbiguousTypeNames) return false;
			if (FullyQualifyAllTypes != other.FullyQualifyAllTypes) return false;
			if (AlwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject != other.AlwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject) return false;

			//TODO: CSharpFormattingOptions. This isn't currently used but it has a ton of properties

			return true;
		}

		public override bool Equals(object obj) {
			return Equals(obj as DecompilerSettings);
		}

		public override int GetHashCode() {
			unchecked {
				// ReSharper disable NonReadonlyMemberInGetHashCode
				int hashCode = 0;
				for (var i = 0; i < decompilationObjects.Length; i++)
					hashCode = (hashCode * 397) ^ decompilationObjects[i].GetHashCode();
				hashCode = (hashCode * 397) ^ nativeIntegers.GetHashCode();
				hashCode = (hashCode * 397) ^ numericIntPtr.GetHashCode();
				hashCode = (hashCode * 397) ^ covariantReturns.GetHashCode();
				hashCode = (hashCode * 397) ^ initAccessors.GetHashCode();
				hashCode = (hashCode * 397) ^ recordClasses.GetHashCode();
				hashCode = (hashCode * 397) ^ recordStructs.GetHashCode();
				hashCode = (hashCode * 397) ^ structDefaultConstructorsAndFieldInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ withExpressions.GetHashCode();
				hashCode = (hashCode * 397) ^ usePrimaryConstructorSyntax.GetHashCode();
				hashCode = (hashCode * 397) ^ functionPointers.GetHashCode();
				hashCode = (hashCode * 397) ^ scopedRef.GetHashCode();
				hashCode = (hashCode * 397) ^ requiredMembers.GetHashCode();
				hashCode = (hashCode * 397) ^ switchExpressions.GetHashCode();
				hashCode = (hashCode * 397) ^ fileScopedNamespaces.GetHashCode();
				hashCode = (hashCode * 397) ^ anonymousMethods.GetHashCode();
				hashCode = (hashCode * 397) ^ anonymousTypes.GetHashCode();
				hashCode = (hashCode * 397) ^ useLambdaSyntax.GetHashCode();
				hashCode = (hashCode * 397) ^ expressionTrees.GetHashCode();
				hashCode = (hashCode * 397) ^ yieldReturn.GetHashCode();
				hashCode = (hashCode * 397) ^ dynamic.GetHashCode();
				hashCode = (hashCode * 397) ^ asyncAwait.GetHashCode();
				hashCode = (hashCode * 397) ^ awaitInCatchFinally.GetHashCode();
				hashCode = (hashCode * 397) ^ asyncEnumerator.GetHashCode();
				hashCode = (hashCode * 397) ^ decimalConstants.GetHashCode();
				hashCode = (hashCode * 397) ^ fixedBuffers.GetHashCode();
				hashCode = (hashCode * 397) ^ stringConcat.GetHashCode();
				hashCode = (hashCode * 397) ^ liftNullables.GetHashCode();
				hashCode = (hashCode * 397) ^ nullPropagation.GetHashCode();
				hashCode = (hashCode * 397) ^ automaticProperties.GetHashCode();
				hashCode = (hashCode * 397) ^ getterOnlyAutomaticProperties.GetHashCode();
				hashCode = (hashCode * 397) ^ automaticEvents.GetHashCode();
				hashCode = (hashCode * 397) ^ usingStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ useEnhancedUsing.GetHashCode();
				hashCode = (hashCode * 397) ^ alwaysUseBraces.GetHashCode();
				hashCode = (hashCode * 397) ^ forEachStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ forEachWithGetEnumeratorExtension.GetHashCode();
				hashCode = (hashCode * 397) ^ paramsCollections.GetHashCode();
				hashCode = (hashCode * 397) ^ lockStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ switchStatementOnString.GetHashCode();
				hashCode = (hashCode * 397) ^ sparseIntegerSwitch.GetHashCode();
				hashCode = (hashCode * 397) ^ usingDeclarations.GetHashCode();
				hashCode = (hashCode * 397) ^ extensionMethods.GetHashCode();
				hashCode = (hashCode * 397) ^ queryExpressions.GetHashCode();
				hashCode = (hashCode * 397) ^ useImplicitMethodGroupConversion.GetHashCode();
				hashCode = (hashCode * 397) ^ useObjectCreationOfGenericTypeParameter.GetHashCode();
				hashCode = (hashCode * 397) ^ alwaysCastTargetsOfExplicitInterfaceImplementationCalls.GetHashCode();
				hashCode = (hashCode * 397) ^ alwaysQualifyMemberReferences.GetHashCode();
				hashCode = (hashCode * 397) ^ alwaysShowEnumMemberValues.GetHashCode();
				hashCode = (hashCode * 397) ^ useDebugSymbols.GetHashCode();
				hashCode = (hashCode * 397) ^ arrayInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ objectCollectionInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ dictionaryInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ extensionMethodsInCollectionInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ useRefLocalsForAccurateOrderOfEvaluation.GetHashCode();
				hashCode = (hashCode * 397) ^ refExtensionMethods.GetHashCode();
				hashCode = (hashCode * 397) ^ stringInterpolation.GetHashCode();
				hashCode = (hashCode * 397) ^ utf8StringLiterals.GetHashCode();
				hashCode = (hashCode * 397) ^ unsignedRightShift.GetHashCode();
				hashCode = (hashCode * 397) ^ checkedOperators.GetHashCode();
				hashCode = (hashCode * 397) ^ showXmlDocumentation.GetHashCode();
				hashCode = (hashCode * 397) ^ decompileMemberBodies.GetHashCode();
				hashCode = (hashCode * 397) ^ useExpressionBodyForCalculatedGetterOnlyProperties.GetHashCode();
				hashCode = (hashCode * 397) ^ outVariables.GetHashCode();
				hashCode = (hashCode * 397) ^ discards.GetHashCode();
				hashCode = (hashCode * 397) ^ introduceRefModifiersOnStructs.GetHashCode();
				hashCode = (hashCode * 397) ^ introduceReadonlyAndInModifiers.GetHashCode();
				hashCode = (hashCode * 397) ^ readOnlyMethods.GetHashCode();
				hashCode = (hashCode * 397) ^ asyncUsingAndForEachStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ introduceUnmanagedConstraint.GetHashCode();
				hashCode = (hashCode * 397) ^ stackAllocInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ patternBasedFixedStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ tupleTypes.GetHashCode();
				hashCode = (hashCode * 397) ^ throwExpressions.GetHashCode();
				hashCode = (hashCode * 397) ^ tupleConversions.GetHashCode();
				hashCode = (hashCode * 397) ^ tupleComparisons.GetHashCode();
				hashCode = (hashCode * 397) ^ namedArguments.GetHashCode();
				hashCode = (hashCode * 397) ^ nonTrailingNamedArguments.GetHashCode();
				hashCode = (hashCode * 397) ^ optionalArguments.GetHashCode();
				hashCode = (hashCode * 397) ^ expandParamsArguments.GetHashCode();
				hashCode = (hashCode * 397) ^ localFunctions.GetHashCode();
				hashCode = (hashCode * 397) ^ deconstruction.GetHashCode();
				hashCode = (hashCode * 397) ^ patternMatching.GetHashCode();
				hashCode = (hashCode * 397) ^ staticLocalFunctions.GetHashCode();
				hashCode = (hashCode * 397) ^ ranges.GetHashCode();
				hashCode = (hashCode * 397) ^ nullableReferenceTypes.GetHashCode();
				hashCode = (hashCode * 397) ^ assumeArrayLengthFitsIntoInt32.GetHashCode();
				hashCode = (hashCode * 397) ^ introduceIncrementAndDecrement.GetHashCode();
				hashCode = (hashCode * 397) ^ makeAssignmentExpressions.GetHashCode();
				hashCode = (hashCode * 397) ^ removeDeadCode.GetHashCode();
				hashCode = (hashCode * 397) ^ removeDeadStores.GetHashCode();
				hashCode = (hashCode * 397) ^ forStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ doWhileStatement.GetHashCode();
				hashCode = (hashCode * 397) ^ refReadOnlyParameters.GetHashCode();
				hashCode = (hashCode * 397) ^ usePrimaryConstructorSyntaxForNonRecordTypes.GetHashCode();
				hashCode = (hashCode * 397) ^ inlineArrays.GetHashCode();
				hashCode = (hashCode * 397) ^ extensionMembers.GetHashCode();
				hashCode = (hashCode * 397) ^ firstClassSpanTypes.GetHashCode();
				hashCode = (hashCode * 397) ^ separateLocalVariableDeclarations.GetHashCode();
				hashCode = (hashCode * 397) ^ aggressiveScalarReplacementOfAggregates.GetHashCode();
				hashCode = (hashCode * 397) ^ aggressiveInlining.GetHashCode();
				hashCode = (hashCode * 397) ^ alwaysUseGlobal.GetHashCode();
				hashCode = (hashCode * 397) ^ removeEmptyDefaultConstructors.GetHashCode();
				hashCode = (hashCode * 397) ^ showTokenAndRvaComments.GetHashCode();
				hashCode = (hashCode * 397) ^ sortMembers.GetHashCode();
				hashCode = (hashCode * 397) ^ forceShowAllMembers.GetHashCode();
				hashCode = (hashCode * 397) ^ sortSystemUsingStatementsFirst.GetHashCode();
				hashCode = (hashCode * 397) ^ maxArrayElements;
				hashCode = (hashCode * 397) ^ maxStringLength;
				hashCode = (hashCode * 397) ^ sortCustomAttributes.GetHashCode();
				hashCode = (hashCode * 397) ^ useSourceCodeOrder.GetHashCode();
				hashCode = (hashCode * 397) ^ allowFieldInitializers.GetHashCode();
				hashCode = (hashCode * 397) ^ oneCustomAttributePerLine.GetHashCode();
				hashCode = (hashCode * 397) ^ typeAddInternalModifier.GetHashCode();
				hashCode = (hashCode * 397) ^ memberAddPrivateModifier.GetHashCode();
				hashCode = (hashCode * 397) ^ hexadecimalNumbers.GetHashCode();
				hashCode = (hashCode * 397) ^ sortSwitchCasesByILOffset.GetHashCode();
				hashCode = (hashCode * 397) ^ insertParenthesesForReadability.GetHashCode();
				hashCode = (hashCode * 397) ^ fullyQualifyAmbiguousTypeNames.GetHashCode();
				hashCode = (hashCode * 397) ^ fullyQualifyAllTypes.GetHashCode();
				hashCode = (hashCode * 397) ^ alwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject.GetHashCode();
				//TODO: CSharpFormattingOptions
				// ReSharper enable NonReadonlyMemberInGetHashCode
				return hashCode;
			}
		}

		public DecompilerSettings CopyTo(DecompilerSettings other)
		{
			other.DecompilationObject0 = this.DecompilationObject0;
			other.DecompilationObject1 = this.DecompilationObject1;
			other.DecompilationObject2 = this.DecompilationObject2;
			other.DecompilationObject3 = this.DecompilationObject3;
			other.DecompilationObject4 = this.DecompilationObject4;
			other.NativeIntegers = this.NativeIntegers;
			other.NumericIntPtr = this.NumericIntPtr;
			other.CovariantReturns = this.CovariantReturns;
			other.InitAccessors = this.InitAccessors;
			other.RecordClasses = this.RecordClasses;
			other.RecordStructs = this.RecordStructs;
			other.StructDefaultConstructorsAndFieldInitializers = this.StructDefaultConstructorsAndFieldInitializers;
			other.WithExpressions = this.WithExpressions;
			other.UsePrimaryConstructorSyntax = this.UsePrimaryConstructorSyntax;
			other.FunctionPointers = this.FunctionPointers;
			other.ScopedRef = this.ScopedRef;
			other.RequiredMembers = this.RequiredMembers;
			other.SwitchExpressions = this.SwitchExpressions;
			other.FileScopedNamespaces = this.FileScopedNamespaces;
			other.AnonymousMethods = this.AnonymousMethods;
			other.AnonymousTypes = this.AnonymousTypes;
			other.UseLambdaSyntax = this.UseLambdaSyntax;
			other.ExpressionTrees = this.ExpressionTrees;
			other.YieldReturn = this.YieldReturn;
			other.Dynamic = this.Dynamic;
			other.AsyncAwait = this.AsyncAwait;
			other.AwaitInCatchFinally = this.AwaitInCatchFinally;
			other.AsyncEnumerator = this.AsyncEnumerator;
			other.DecimalConstants = this.DecimalConstants;
			other.FixedBuffers = this.FixedBuffers;
			other.StringConcat = this.StringConcat;
			other.LiftNullables = this.LiftNullables;
			other.NullPropagation = this.NullPropagation;
			other.AutomaticProperties = this.AutomaticProperties;
			other.GetterOnlyAutomaticProperties = this.GetterOnlyAutomaticProperties;
			other.AutomaticEvents = this.AutomaticEvents;
			other.UsingStatement = this.UsingStatement;
			other.UseEnhancedUsing = this.UseEnhancedUsing;
			other.AlwaysUseBraces = this.AlwaysUseBraces;
			other.ForEachStatement = this.ForEachStatement;
			other.ForEachWithGetEnumeratorExtension = this.ForEachWithGetEnumeratorExtension;
			other.ParamsCollections = this.ParamsCollections;
			other.LockStatement = this.LockStatement;
			other.SwitchStatementOnString = this.SwitchStatementOnString;
			other.SparseIntegerSwitch = this.SparseIntegerSwitch;
			other.UsingDeclarations = this.UsingDeclarations;
			other.ExtensionMethods = this.ExtensionMethods;
			other.QueryExpressions = this.QueryExpressions;
			other.UseImplicitMethodGroupConversion = this.UseImplicitMethodGroupConversion;
			other.UseObjectCreationOfGenericTypeParameter = this.UseObjectCreationOfGenericTypeParameter;
			other.AlwaysCastTargetsOfExplicitInterfaceImplementationCalls = this.AlwaysCastTargetsOfExplicitInterfaceImplementationCalls;
			other.AlwaysQualifyMemberReferences = this.AlwaysQualifyMemberReferences;
			other.AlwaysShowEnumMemberValues = this.AlwaysShowEnumMemberValues;
			other.UseDebugSymbols = this.UseDebugSymbols;
			other.ArrayInitializers = this.ArrayInitializers;
			other.ObjectOrCollectionInitializers = this.ObjectOrCollectionInitializers;
			other.DictionaryInitializers = this.DictionaryInitializers;
			other.ExtensionMethodsInCollectionInitializers = this.ExtensionMethodsInCollectionInitializers;
			other.UseRefLocalsForAccurateOrderOfEvaluation = this.UseRefLocalsForAccurateOrderOfEvaluation;
			other.RefExtensionMethods = this.RefExtensionMethods;
			other.StringInterpolation = this.StringInterpolation;
			other.Utf8StringLiterals = this.Utf8StringLiterals;
			other.UnsignedRightShift = this.UnsignedRightShift;
			other.CheckedOperators = this.CheckedOperators;
			other.ShowXmlDocumentation = this.ShowXmlDocumentation;
			other.DecompileMemberBodies = this.DecompileMemberBodies;
			other.UseExpressionBodyForCalculatedGetterOnlyProperties = this.UseExpressionBodyForCalculatedGetterOnlyProperties;
			other.OutVariables = this.OutVariables;
			other.Discards = this.Discards;
			other.IntroduceRefModifiersOnStructs = this.IntroduceRefModifiersOnStructs;
			other.IntroduceReadonlyAndInModifiers = this.IntroduceReadonlyAndInModifiers;
			other.ReadOnlyMethods = this.ReadOnlyMethods;
			other.AsyncUsingAndForEachStatement = this.AsyncUsingAndForEachStatement;
			other.IntroduceUnmanagedConstraint = this.IntroduceUnmanagedConstraint;
			other.StackAllocInitializers = this.StackAllocInitializers;
			other.PatternBasedFixedStatement = this.PatternBasedFixedStatement;
			other.TupleTypes = this.TupleTypes;
			other.ThrowExpressions = this.ThrowExpressions;
			other.TupleConversions = this.TupleConversions;
			other.TupleComparisons = this.TupleComparisons;
			other.NamedArguments = this.NamedArguments;
			other.NonTrailingNamedArguments = this.NonTrailingNamedArguments;
			other.OptionalArguments = this.OptionalArguments;
			other.ExpandParamsArguments = this.ExpandParamsArguments;
			other.LocalFunctions = this.LocalFunctions;
			other.Deconstruction = this.Deconstruction;
			other.PatternMatching = this.PatternMatching;
			other.StaticLocalFunctions = this.StaticLocalFunctions;
			other.Ranges = this.Ranges;
			other.NullableReferenceTypes = this.NullableReferenceTypes;
			other.AssumeArrayLengthFitsIntoInt32 = this.AssumeArrayLengthFitsIntoInt32;
			other.IntroduceIncrementAndDecrement = this.IntroduceIncrementAndDecrement;
			other.MakeAssignmentExpressions = this.MakeAssignmentExpressions;
			other.RemoveDeadCode = this.RemoveDeadCode;
			other.RemoveDeadStores = this.RemoveDeadStores;
			other.ForStatement = this.ForStatement;
			other.DoWhileStatement = this.DoWhileStatement;
			other.RefReadOnlyParameters = this.RefReadOnlyParameters;
			other.UsePrimaryConstructorSyntaxForNonRecordTypes = this.UsePrimaryConstructorSyntaxForNonRecordTypes;
			other.InlineArrays = this.InlineArrays;
			other.ExtensionMembers = this.ExtensionMembers;
			other.FirstClassSpanTypes = this.FirstClassSpanTypes;
			other.SeparateLocalVariableDeclarations = this.SeparateLocalVariableDeclarations;
			other.AggressiveScalarReplacementOfAggregates = this.AggressiveScalarReplacementOfAggregates;
			other.AggressiveInlining = this.AggressiveInlining;
			other.AlwaysUseGlobal = this.AlwaysUseGlobal;
			other.RemoveEmptyDefaultConstructors = this.RemoveEmptyDefaultConstructors;
			other.ShowTokenAndRvaComments = this.ShowTokenAndRvaComments;
			other.SortMembers = this.SortMembers;
			other.ForceShowAllMembers = this.ForceShowAllMembers;
			other.SortSystemUsingStatementsFirst = this.SortSystemUsingStatementsFirst;
			other.MaxArrayElements = this.MaxArrayElements;
			other.MaxStringLength = this.MaxStringLength;
			other.SortCustomAttributes = this.SortCustomAttributes;
			other.UseSourceCodeOrder = this.UseSourceCodeOrder;
			other.AllowFieldInitializers = this.AllowFieldInitializers;
			other.OneCustomAttributePerLine = this.OneCustomAttributePerLine;
			other.TypeAddInternalModifier = this.TypeAddInternalModifier;
			other.MemberAddPrivateModifier = this.MemberAddPrivateModifier;
			other.HexadecimalNumbers = this.HexadecimalNumbers;
			other.SortSwitchCasesByILOffset = this.SortSwitchCasesByILOffset;
			other.InsertParenthesesForReadability = this.InsertParenthesesForReadability;
			other.FullyQualifyAmbiguousTypeNames = this.FullyQualifyAmbiguousTypeNames;
			other.FullyQualifyAllTypes = this.FullyQualifyAllTypes;
			other.AlwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject = this.AlwaysGenerateExceptionVariableForCatchBlocksUnlessTypeIsObject;
			//TODO: CSharpFormattingOptions
			return other;
		}
	}
}
