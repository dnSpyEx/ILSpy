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
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.PE;
using dnSpy.Contracts.Decompiler;

namespace ICSharpCode.Decompiler
{
	public static class DnlibExtensions
	{
		/// <summary>
		/// Gets the (exclusive) end offset of this instruction.
		/// </summary>
		public static int GetEndOffset(this Instruction inst)
		{
			if (inst == null)
				throw new ArgumentNullException(nameof(inst));
			return (int)inst.Offset + inst.GetSize();
		}

		public static string OffsetToString(uint offset)
		{
			return string.Format("IL_{0:X4}", offset);
		}

		public static string OffsetToString(int offset)
		{
			return string.Format("IL_{0:X4}", offset);
		}

		public static TypeSig GetTypeSig(this IType type)
		{
			if (type is null)
				return null;
			if (type is TypeSig typeSig)
				return typeSig;
			if (type is TypeSpec typeSpec)
				return typeSpec.TypeSig;
			if (type is ITypeDefOrRef typeDefOrRef)
				return typeDefOrRef.ToTypeSig();
			return null;
		}

		public static bool IsCompilerGeneratedOrIsInCompilerGeneratedClass(this MethodDef method)
		{
			if (method.IsCompilerGenerated())
				return true;
			return method.DeclaringType != null && method.DeclaringType.IsCompilerGenerated();
		}

		public static bool IsCompilerGeneratedOrIsInCompilerGeneratedClass(this TypeDef type)
		{
			if (type.IsCompilerGenerated())
				return true;
			TypeDef declaringTypeHandle = type.DeclaringType;
			if (declaringTypeHandle != null && declaringTypeHandle.IsCompilerGenerated())
				return true;
			return false;
		}

		public static bool IsCompilerGenerated(this IHasCustomAttribute  provider)
		{
			return provider.IsDefined(systemRuntimeCompilerServicesString, compilerGeneratedAttributeString);
		}

		static readonly UTF8String systemRuntimeCompilerServicesString = new UTF8String("System.Runtime.CompilerServices");
		static readonly UTF8String compilerGeneratedAttributeString = new UTF8String("CompilerGeneratedAttribute");

		public static bool IsAnonymousType(this ITypeDefOrRef type)
		{
			if (type == null)
				return false;
			if (!string.IsNullOrEmpty(type.GetNamespaceInternal()))
				return false;
			string name = type.Name;
			if (name.StartsWith("VB$AnonymousType_", StringComparison.Ordinal) || (type.HasGeneratedName() && (name.Contains("AnonType") || name.Contains("AnonymousType")))) {
				TypeDef td = type.ResolveTypeDef();
				return td != null && td.IsCompilerGenerated();
			}
			return false;
		}

		static string GetNamespaceInternal(this ITypeDefOrRef tdr) {
			var tr = tdr as TypeRef;
			if (tr != null)
				return tr.Namespace;
			var td = tdr as TypeDef;
			if (td != null)
				return td.Namespace;
			return tdr.Namespace;
		}

		public static bool HasGeneratedName(this IMemberRef member)
		{
			if (member == null)
				return false;
			var u = member.Name;
			return (object)u != null && u.Data != null && u.Data.Length > 0 && (u.Data[0] == '<' || (u.Data[0] == '$' && u.StartsWith("$VB", StringComparison.Ordinal)));
		}

		public static bool IsUnconditionalBranch(this OpCode opcode)
		{
			if (opcode.OpCodeType == OpCodeType.Prefix || opcode.OpCodeType == OpCodeType.Nternal)
				return false;
			switch (opcode.FlowControl) {
				case FlowControl.Branch:
				case FlowControl.Throw:
				case FlowControl.Return:
					return true;
				case FlowControl.Next:
				case FlowControl.Call:
				case FlowControl.Cond_Branch:
					return false;
				default:
					throw new NotSupportedException(opcode.FlowControl.ToString());
			}
		}

		public static TypeSystem.FullTypeName GetFullTypeName(this ITypeDefOrRef typeDef)
		{
			string name = TypeSystem.ReflectionHelper.SplitTypeParameterCountFromReflectionName(typeDef.Name, out int typeParameterCount);
			if (typeDef.DeclaringType is not null)
				return typeDef.DeclaringType.GetFullTypeName().NestedType(name, typeParameterCount);
			return new TypeSystem.TopLevelTypeName(typeDef.Namespace ?? "", name, typeParameterCount);
		}

		public static TypeSystem.FullTypeName GetFullTypeName(this ExportedType typeDef)
		{
			string name = TypeSystem.ReflectionHelper.SplitTypeParameterCountFromReflectionName(typeDef.Name, out int typeParameterCount);
			if (typeDef.Implementation is ExportedType declaringType)
				return declaringType.GetFullTypeName().NestedType(name, typeParameterCount);
			return new TypeSystem.TopLevelTypeName(typeDef.Namespace ?? "", name, typeParameterCount);
		}

		public static IEnumerable<Parameter> GetParameters(this PropertyDef property)
		{
			if (property == null)
				yield break;
			if (property.GetMethod != null)
			{
				foreach (var param in property.GetMethod.Parameters)
					yield return param;
				yield break;
			}
			if (property.SetMethod != null)
			{
				int last = property.SetMethod.Parameters.Count - 1;
				foreach (var param in property.SetMethod.Parameters)
				{
					if (param.Index != last)
						yield return param;
				}
				yield break;
			}

			int i = 0;
			foreach (TypeSig param in property.PropertySig.GetParameters())
			{
				yield return new Parameter(i,i,param);
				i++;
			}
		}

		public static IList<TypeSig> GetParameters(this MethodBaseSig methodSig)
		{
			if (methodSig == null)
				return new List<TypeSig>();
			if (methodSig.ParamsAfterSentinel != null)
				return methodSig.Params
								.Concat(new TypeSig[] { new SentinelSig() })
								.Concat(methodSig.ParamsAfterSentinel)
								.ToList();
			else
				return methodSig.Params;
		}

		public static int GetParametersSkip(this IList<Parameter> parameters)
		{
			if (parameters == null || parameters.Count == 0)
				return 0;
			if (parameters[0].IsHiddenThisParameter)
				return 1;
			return 0;
		}

		public static IEnumerable<Parameter> SkipNonNormal(this IList<Parameter> parameters) {
			if (parameters == null)
				yield break;
			for (int i = 0; i < parameters.Count; i++) {
				var p = parameters[i];
				if (p.IsNormalMethodParameter)
					yield return p;
			}
		}

		public static int GetNumberOfNormalParameters(this IList<Parameter> parameters)
		{
			if (parameters == null)
				return 0;
			return parameters.Count - GetParametersSkip(parameters);
		}

		public static Instruction GetPrevious(this CilBody body, Instruction instr)
		{
			int index = body.Instructions.IndexOf(instr);
			if (index <= 0)
				return null;
			return body.Instructions[index - 1];
		}

		public static bool IsValueType(ITypeDefOrRef tdr)
		{
			if (tdr == null)
				return false;
			var ts = tdr as TypeSpec;
			if (ts != null)
				return IsValueType(ts.TypeSig);
			return tdr.IsValueType;
		}

		public static bool IsValueType(TypeSig ts) => ts?.IsValueType ?? false;

		public static string GetNamespace(this IType type, StringBuilder sb)
		{
			if (type is TypeDef td)
				return td.Namespace;
			if (type is TypeRef tr)
				return tr.Namespace;
			sb.Length = 0;
			return FullNameFactory.Namespace(type, false, sb);
		}

		public static string GetName(this IType type, StringBuilder sb) {
			if (type is TypeDef td)
				return td.Name;
			if (type is TypeRef tr)
				return tr.Name;
			sb.Length = 0;
			return FullNameFactory.Name(type, false, sb);
		}

		public static string GetFnPtrName(FnPtrSig sig)
		{
			return "method";
		}

		public static bool Compare(this ITypeDefOrRef type, UTF8String expNs, UTF8String expName)
		{
			if (type == null)
				return false;

			if (type is TypeRef tr)
				return tr.Namespace == expNs && tr.Name == expName;
			if (type is TypeDef td)
				return td.Namespace == expNs && td.Name == expName;

			return false;
		}

		static readonly UTF8String systemString = new UTF8String("System");
		static readonly UTF8String nullableString = new UTF8String("Nullable`1");

		public static bool IsSystemNullable(this ClassOrValueTypeSig sig) {
			return sig is ValueTypeSig && sig.TypeDefOrRef.Compare(systemString, nullableString);
		}

		public static string GetScopeName(this IScope scope)
		{
			if (scope == null)
				return string.Empty;
			if (scope is IFullName)
				return ((IFullName)scope).Name;
			else
				return scope.ScopeName;	// Shouldn't be reached
		}

		internal static bool HasKnownAttribute(this CustomAttributeCollection customAttributes, TypeSystem.KnownAttribute type)
		{
			foreach (var customAttribute in customAttributes) {
				if (customAttribute.IsKnownAttribute(type))
					return true;
			}
			return false;
		}

		internal static bool IsKnownAttribute(this CustomAttribute attr, TypeSystem.KnownAttribute attrType)
		{
			return attr.AttributeType.IsKnownType(attrType);
		}

		internal static bool IsKnownType(this ITypeDefOrRef handle, TypeSystem.KnownAttribute knownType)
		{
			return handle != null && GetFullTypeName(handle) == TypeSystem.KnownAttributes.GetTypeName(knownType);
		}

		public static TypeSystem.Nullability? GetNullableContext(this CustomAttributeCollection customAttributes)
		{
			foreach (var customAttribute in customAttributes) {
				if (customAttribute.IsKnownAttribute(TypeSystem.KnownAttribute.NullableContext)) {
					if (customAttribute.ConstructorArguments.Count == 1 && customAttribute.ConstructorArguments[0].Value is byte b && b <= 2) {
						return (TypeSystem.Nullability)b;
					}
				}
			}
			return null;
		}

		public static ImageSectionHeader GetContainingSection(this ModuleDef mod, RVA rva) {
			if (mod is not ModuleDefMD mdMod)
				return null;
			var image = mdMod.Metadata.PEImage;
			foreach (var section in image.ImageSectionHeaders) {
				if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
					return section;
			}
			return null;
		}

		public static int IndexOf<T>(this IReadOnlyList<T> collection, T value) {
			var comparer = EqualityComparer<T>.Default;
			int index = 0;
			foreach (var item in collection) {
				if (comparer.Equals(item, value)) {
					return index;
				}
				index++;
			}
			return -1;
		}

		sealed class InterfaceImplComparer : IComparer<InterfaceImpl> {
			public static readonly InterfaceImplComparer Instance = new InterfaceImplComparer();

			public int Compare(InterfaceImpl x, InterfaceImpl y) {
				int c = StringComparer.OrdinalIgnoreCase.Compare(x.Interface.Name, y.Interface.Name);
				if (c != 0)
					return c;
				c = x.MDToken.Raw.CompareTo(y.MDToken.Raw);
				if (c != 0)
					return c;
				return x.GetHashCode().CompareTo(y.GetHashCode());
			}
		}

		public static IList<InterfaceImpl> GetInterfaceImpls(this TypeDef type, bool sortMembers)
		{
			if (!sortMembers)
				return type.Interfaces;
			var ary = type.Interfaces.ToArray();
			Array.Sort(ary, InterfaceImplComparer.Instance);
			return ary;
		}

		public static IList<TypeDef> GetNestedTypes(this TypeDef type, bool sortMembers)
		{
			if (!sortMembers)
				return type.NestedTypes;
			var ary = type.NestedTypes.ToArray();
			Array.Sort(ary, TypeDefComparer.Instance);
			return ary;
		}

		public static IList<FieldDef> GetFields(this TypeDef type, bool sortMembers)
		{
			if (!sortMembers || !type.CanSortFields())
				return type.Fields;
			var ary = type.Fields.ToArray();
			Array.Sort(ary, FieldDefComparer.Instance);
			return ary;
		}

		public static IList<EventDef> GetEvents(this TypeDef type, bool sortMembers)
		{
			if (!sortMembers || !type.CanSortMethods())
				return type.Events;
			var ary = type.Events.ToArray();
			Array.Sort(ary, EventDefComparer.Instance);
			return ary;
		}

		public static IList<PropertyDef> GetProperties(this TypeDef type, bool sortMembers)
		{
			if (!sortMembers || !type.CanSortMethods())
				return type.Properties;
			var ary = type.Properties.ToArray();
			Array.Sort(ary, PropertyDefComparer.Instance);
			return ary;
		}

		public static IList<MethodDef> GetMethods(this TypeDef type, bool sortMembers)
		{
			if (!sortMembers || !type.CanSortMethods())
				return type.Methods;
			var ary = type.Methods.ToArray();
			Array.Sort(ary, MethodDefComparer.Instance);
			return ary;
		}

		static readonly UTF8String multicastDelegateString = new UTF8String("MulticastDelegate");

		internal static bool IsNormalDelegate(this TypeDef td)
		{
			if (!td.BaseType.Compare(systemString, multicastDelegateString))
				return false;

			if (td.HasFields)
				return false;
			if (td.HasProperties)
				return false;
			if (td.HasEvents)
				return false;
			if (td.Methods.Any(m => m.Body != null))
				return false;

			return true;
		}

		public static TypeSystem.KnownTypeCode ToKnownTypeCode(this ElementType typeCode)
		{
			switch (typeCode) {
				case ElementType.Boolean:
					return TypeSystem.KnownTypeCode.Boolean;
				case ElementType.U1:
					return TypeSystem.KnownTypeCode.Byte;
				case ElementType.I1:
					return TypeSystem.KnownTypeCode.SByte;
				case ElementType.Char:
					return TypeSystem.KnownTypeCode.Char;
				case ElementType.I2:
					return TypeSystem.KnownTypeCode.Int16;
				case ElementType.U2:
					return TypeSystem.KnownTypeCode.UInt16;
				case ElementType.I4:
					return TypeSystem.KnownTypeCode.Int32;
				case ElementType.U4:
					return TypeSystem.KnownTypeCode.UInt32;
				case ElementType.I8:
					return TypeSystem.KnownTypeCode.Int64;
				case ElementType.U8:
					return TypeSystem.KnownTypeCode.UInt64;
				case ElementType.R4:
					return TypeSystem.KnownTypeCode.Single;
				case ElementType.R8:
					return TypeSystem.KnownTypeCode.Double;
				case ElementType.I:
					return TypeSystem.KnownTypeCode.IntPtr;
				case ElementType.U:
					return TypeSystem.KnownTypeCode.UIntPtr;
				case ElementType.Object:
					return TypeSystem.KnownTypeCode.Object;
				case ElementType.String:
					return TypeSystem.KnownTypeCode.String;
				case ElementType.TypedByRef:
					return TypeSystem.KnownTypeCode.TypedReference;
				case ElementType.Void:
					return TypeSystem.KnownTypeCode.Void;
				default:
					return TypeSystem.KnownTypeCode.None;
			}
		}

		public static bool SupportsPrefix(this Instruction instr, Code prefix) {
			switch (instr.OpCode.Code) {
				case Code.Cpblk:
				case Code.Initblk:
				case Code.Ldobj:
				case Code.Ldind_I:
				case Code.Ldind_I1:
				case Code.Ldind_I2:
				case Code.Ldind_I4:
				case Code.Ldind_I8:
				case Code.Ldind_R4:
				case Code.Ldind_R8:
				case Code.Ldind_Ref:
				case Code.Ldind_U1:
				case Code.Ldind_U2:
				case Code.Ldind_U4:
				case Code.Stobj:
				case Code.Stind_I:
				case Code.Stind_I1:
				case Code.Stind_I2:
				case Code.Stind_I4:
				case Code.Stind_I8:
				case Code.Stind_R4:
				case Code.Stind_R8:
				case Code.Stind_Ref:
				case Code.Ldfld:
				case Code.Stfld:
					return prefix == Code.Volatile || prefix == Code.Unaligned;
				case Code.Ldsfld:
				case Code.Stsfld:
					return prefix == Code.Volatile;
				case Code.Callvirt:
					return prefix == Code.Constrained || prefix == Code.Tailcall;
				case Code.Call:
					if (prefix == Code.Readonly) {
						return instr.Operand is MemberRef memberRef && memberRef.Name == "Address" &&
							   memberRef.DeclaringType is TypeSpec typeSpec && typeSpec.TypeSig.RemoveModifiers() is ArraySigBase;
					}
					return prefix == Code.Constrained || prefix == Code.Tailcall;
				case Code.Calli:
					return prefix == Code.Tailcall;
				case Code.Ldelema:
					return prefix == Code.Readonly;
				case Code.Ldftn:
					return prefix == Code.Constrained;
				default:
					return false;
			}
		}

		public static TypeDef ResolveWithinSameModule(this ITypeDefOrRef type) {
			if (type is null)
				return null;
			if (type is TypeDef typeDef)
				return typeDef;

			var scope = type.Scope;
			if (scope is null || scope == type.Module)
				return type.ResolveTypeDef();
			return null;
		}

		public static FieldDef ResolveFieldWithinSameModule(this IField field) {
			if (field is FieldDef fieldDef)
				return fieldDef;

			if (field is MemberRef memberRef && memberRef.IsFieldRef) {
				var declType = memberRef.DeclaringType;
				if (declType is null)
					return null;
				var scope = declType.Scope;
				if (scope is null || scope == memberRef.Module)
					return memberRef.ResolveField();
			}

			return null;
		}

		public static MethodDef ResolveMethodWithinSameModule(this IMethod method) {
			if (method is MethodSpec spec)
				method = spec.Method;

			if (method is MethodDef mDef)
				return mDef;

			if (method is MemberRef memberRef && memberRef.IsMethodRef) {
				var declType = memberRef.DeclaringType;
				if (declType is null)
					return null;
				var scope = declType.Scope;
				if (scope is null || scope == memberRef.Module)
					return memberRef.ResolveMethod();
			}

			return null;
		}

		public static bool IsCompilerGeneratedOrIsInCompilerGeneratedClass(this IMemberDef member)
		{
			for (int i = 0; i < 50; i++) {
				if (member == null)
					break;
				if (member.IsCompilerGenerated())
					return true;
				member = member.DeclaringType;
			}
			return false;
		}

		static readonly UTF8String dynamicCallSiteTypeName = new UTF8String("<>o");

		public static bool IsDynamicCallSiteContainerType(this ITypeDefOrRef type)
		{
			if (type == null)
				return false;
			return type.Name == dynamicCallSiteTypeName || type.Name.StartsWith("<>o__");
		}

		public static bool IsLocalFunction(this MethodDef method) {
			var name = method.Name.String;
			return name.StartsWith("<", StringComparison.Ordinal) && name.Contains(">g__");
		}

		public static bool ContainsAnonymousType(this TypeSig type)
		{
			return type.ContainsAnonymousType(0);
		}

		static bool ContainsAnonymousType(this TypeSig type, int depth)
		{
			if (depth >= 30)
				return false;
			if (type is GenericInstSig git && git.GenericType is not null) {
				if (IsAnonymousType(git.GenericType.TypeDefOrRef))
					return true;
				for (int i = 0; i < git.GenericArguments.Count; i++) {
					if (git.GenericArguments[i].ContainsAnonymousType(depth + 1))
						return true;
				}
				return false;
			}
			if (type is not null && type.Next is not null)
				return type.Next.ContainsAnonymousType(depth + 1);
			return false;
		}

		public static IList<TypeSig> GetParametersWithoutSentinel(this MethodBaseSig methodSig)
		{
			if (methodSig is null)
				return new List<TypeSig>();
			if (methodSig.ParamsAfterSentinel is not null)
				return methodSig.Params.Concat(methodSig.ParamsAfterSentinel).ToList();
			return methodSig.Params;
		}

		public static bool IsSystemBoolean(this ITypeDefOrRef type)
		{
			if (type == null)
				return false;
			if (!type.DefinitionAssembly.IsCorLib())
				return false;

			return type.Compare(systemString, booleanString);
		}
		static readonly UTF8String booleanString = new UTF8String("Boolean");
		static readonly UTF8String objectString = new UTF8String("Object");

		public static bool IsSystemObject(this ITypeDefOrRef type)
		{
			if (type == null)
				return false;
			if (!type.DefinitionAssembly.IsCorLib())
				return false;

			return type.Compare(systemString, objectString);
		}

		public static IEnumerable<int> GetLengths(this ArraySigBase ary)
		{
			var sizes = ary.GetSizes();
			for (int i = 0; i < (int)ary.Rank; i++)
				yield return i < sizes.Count ? (int)sizes[i] - 1 : 0;
		}

		public static string GetMethodSigFullName(MethodSig methodSig)
		{
			if (methodSig == null)
				return string.Empty;
			var sb = new StringBuilder();

			FullNameFactory.FullNameSB(methodSig.RetType, false, null, null, null, sb);
			sb.Append('(');
			PrintArgs(sb, methodSig.Params, true);
			if (methodSig.ParamsAfterSentinel != null) {
				if (methodSig.Params.Count > 0)
					sb.Append(',');
				sb.Append("...,");
				PrintArgs(sb, methodSig.ParamsAfterSentinel, false);
			}
			sb.Append(')');

			return sb.ToString();
		}

		static void PrintArgs(StringBuilder sb, IList<TypeSig> args, bool isFirst) {
			for (int i = 0; i < args.Count; i++) {
				if (!isFirst)
					sb.Append(',');
				isFirst = false;
				FullNameFactory.FullNameSB(args[i], false, null, null, null, sb);
			}
		}

		public static bool HasIsReadOnlyAttribute(IHasCustomAttribute hca) {
			return hca.IsDefined(systemRuntimeCompilerServicesString, isReadOnlyAttributeString);
		}
		static readonly UTF8String isReadOnlyAttributeString = new UTF8String("IsReadOnlyAttribute");

		public static bool HasIsByRefLikeAttribute(IHasCustomAttribute hca) {
			return hca.IsDefined(systemRuntimeCompilerServicesString, isByRefLikeAttributeString);
		}
		static readonly UTF8String isByRefLikeAttributeString = new UTF8String("IsByRefLikeAttribute");

		public static string FullName(this IMethod method, StringBuilder sb) {
			var methodDef = method as MethodDef;
			var methodSpec = method as MethodSpec;
			if (methodSpec is not null)
				method = methodSpec.Method;
			var memberRef = method as MemberRef;

			string declaringType;
			if (memberRef is not null && memberRef.Class is ModuleRef modRef)
				declaringType = $"[module:{modRef}]<Module>";
			else
				declaringType = FullNameFactory.FullName(method.DeclaringType, false, null, sb.Clear());

			var typeGenArgs = ((memberRef?.Class as TypeSpec)?.TypeSig as GenericInstSig)?.GenericArguments;
			var methodGenArgs = methodSpec?.GenericInstMethodSig?.GenericArguments;
			return FullNameFactory.MethodFullName(declaringType, method.Name, method.MethodSig, typeGenArgs, methodGenArgs, methodDef, sb.Clear());
		}

		public static bool IsSignedIntegralType(this TypeSig type)
		{
			if (type is null)
				return false;
			return type.ElementType == ElementType.I1 ||
				   type.ElementType == ElementType.I2 ||
				   type.ElementType == ElementType.I4 ||
				   type.ElementType == ElementType.I8 ||
				   type.ElementType == ElementType.I;
		}

		public static bool IsZero(this object value)
		{
			if (value == null)
				return false;
			var type = value.GetType();
			switch (Type.GetTypeCode(type)) {
				case TypeCode.Empty:		return false;
				case TypeCode.DBNull:		return false;
				case TypeCode.Boolean:		return false;
				case TypeCode.Char:			return (char)value == 0;
				case TypeCode.SByte:		return (sbyte)value == 0;
				case TypeCode.Byte:			return (byte)value == 0;
				case TypeCode.Int16:		return (short)value == 0;
				case TypeCode.UInt16:		return (ushort)value == 0;
				case TypeCode.Int32:		return (int)value == 0;
				case TypeCode.UInt32:		return (uint)value == 0;
				case TypeCode.Int64:		return (long)value == 0;
				case TypeCode.UInt64:		return (ulong)value == 0;
				case TypeCode.Single:		return (float)value == 0;
				case TypeCode.Double:		return (double)value == 0;
				case TypeCode.Decimal:		return (decimal)value == 0;
				case TypeCode.DateTime:		return false;
				case TypeCode.String:		return false;
				default:					return false;
				case TypeCode.Object:
					if (value is IntPtr ip)
						return ip == IntPtr.Zero;
					if (value is UIntPtr uip)
						return uip == UIntPtr.Zero;
					return false;
			}
		}
	}
}
