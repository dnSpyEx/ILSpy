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
using System.Text;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.Disassembler
{
	public enum ILNameSyntax
	{
		/// <summary>
		/// class/valuetype + TypeName (built-in types use keyword syntax)
		/// </summary>
		Signature,
		/// <summary>
		/// Like signature, but always refers to type parameters using their position
		/// </summary>
		SignatureNoNamedTypeParameters,
		/// <summary>
		/// [assembly]Full.Type.Name (even for built-in types)
		/// </summary>
		TypeName,
		/// <summary>
		/// Name (but built-in types use keyword syntax)
		/// </summary>
		ShortTypeName
	}

	public static class DisassemblerHelpers
	{
		public static string OffsetToString(int offset)
		{
			return string.Format("IL_{0:x4}", offset);
		}

		public static string OffsetToString(long offset)
		{
			return string.Format("IL_{0:x4}", offset);
		}

		public static void WriteOffsetReference(ITextOutput writer, Instruction instruction)
		{
			if (instruction == null)
				writer.Write("null");
			else
				writer.WriteReference(OffsetToString(instruction.Offset), instruction);
		}

		public static void WriteTo(this ExceptionHandler exceptionHandler, ITextOutput writer)
		{
			writer.Write(".try ");
			WriteOffsetReference(writer, exceptionHandler.TryStart);
			writer.Write('-');
			WriteOffsetReference(writer, exceptionHandler.TryEnd);
			writer.Write(' ');
			writer.Write(exceptionHandler.HandlerType.ToString().ToLowerInvariant());
			if (exceptionHandler.FilterStart != null) {
				writer.Write(' ');
				WriteOffsetReference(writer, exceptionHandler.FilterStart);
				writer.Write(" handler ");
			}
			if (exceptionHandler.CatchType != null) {
				writer.Write(' ');
				exceptionHandler.CatchType.WriteTo(writer);
			}
			writer.Write(' ');
			WriteOffsetReference(writer, exceptionHandler.HandlerStart);
			writer.Write('-');
			WriteOffsetReference(writer, exceptionHandler.HandlerEnd);
		}

		public static void WriteTo(this Instruction instruction, ITextOutput writer)
		{
			writer.WriteDefinition(OffsetToString(instruction.Offset), instruction);
			writer.Write(": ");
			writer.WriteReference(instruction.OpCode.Name, instruction.OpCode);
			if (instruction.Operand != null) {
				writer.Write(' ');
				if (instruction.OpCode == OpCodes.Ldtoken) {
					var member = instruction.Operand as IMemberRef;
					if (member != null && member.IsMethod)
						writer.Write("method ");
					else if (member != null && member.IsField)
						writer.Write("field ");
				}
				WriteOperand(writer, instruction.Operand);
			}
		}

		static void WriteLabelList(ITextOutput writer, Instruction[] instructions)
		{
			writer.Write("(");
			for (int i = 0; i < instructions.Length; i++) {
				if (i != 0) writer.Write(", ");
				WriteOffsetReference(writer, instructions[i]);
			}
			writer.Write(")");
		}

		static string ToInvariantCultureString(object value)
		{
			IConvertible convertible = value as IConvertible;
			return (null != convertible)
				? convertible.ToString(System.Globalization.CultureInfo.InvariantCulture)
				: value.ToString();
		}

		public static void WriteMethodTo(this IMethod method, ITextOutput writer)
		{
			writer.Write((MethodSig)null, method);
		}

		public static void Write(this ITextOutput writer, MethodSig sig, IMethod method = null)
		{
			if (sig == null && method != null)
				sig = method.MethodSig;
			if (sig == null)
				return;
			if (sig.ExplicitThis) {
				writer.Write("instance explicit ");
			} else if (sig.HasThis) {
				writer.Write("instance ");
			}
			if (sig.CallingConvention == CallingConvention.VarArg) {
				writer.Write("vararg ");
			}
			sig.RetType.WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			writer.Write(' ');
			if (method != null) {
				if (method.DeclaringType != null) {
					method.DeclaringType.WriteTo(writer, ILNameSyntax.TypeName);
					writer.Write("::");
				}
				MethodDef md = method as MethodDef;
				if (md != null && md.IsCompilerControlled) {
					writer.WriteReference(Escape(method.Name + "$PST" + method.MDToken.ToInt32().ToString("X8")), method);
				} else {
					writer.WriteReference(Escape(method.Name), method);
				}
			}
			MethodSpec gim = method as MethodSpec;
			if (gim != null && gim.GenericInstMethodSig != null) {
				writer.Write('<');
				for (int i = 0; i < gim.GenericInstMethodSig.GenericArguments.Count; i++) {
					if (i > 0)
						writer.Write(", ");
					gim.GenericInstMethodSig.GenericArguments[i].WriteTo(writer);
				}
				writer.Write('>');
			}
			writer.Write("(");
			var parameters = sig.GetParameters();
			for (int i = 0; i < parameters.Count; ++i) {
				if (i > 0)
					writer.Write(", ");
				parameters[i].WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			}
			writer.Write(")");
		}

		public static void WriteTo(this MethodSig sig, ITextOutput writer)
		{
			if (sig.ExplicitThis) {
				writer.Write("instance explicit ");
			} else if (sig.HasThis) {
				writer.Write("instance ");
			}
			if (sig.CallingConvention == CallingConvention.VarArg) {
				writer.Write("vararg ");
			}
			sig.RetType.WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			writer.Write(' ');
			writer.Write("(");
			var parameters = sig.GetParameters();
			for (int i = 0; i < parameters.Count; ++i) {
				if (i > 0)
					writer.Write(", ");
				parameters[i].WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			}
			writer.Write(")");
		}

		static void WriteFieldTo(this IField field, ITextOutput writer)
		{
			if (field == null || field.FieldSig == null)
				return;
			field.FieldSig.Type.WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			writer.Write(' ');
			field.DeclaringType.WriteTo(writer, ILNameSyntax.TypeName);
			writer.Write("::");
			writer.WriteReference(Escape(field.Name), field);
		}

		static bool IsValidIdentifierCharacter(char c)
		{
			return c == '_' || c == '$' || c == '@' || c == '?' || c == '`';
		}

		static bool IsValidIdentifier(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
				return false;
			if (!(char.IsLetter(identifier[0]) || IsValidIdentifierCharacter(identifier[0]))) {
				// As a special case, .ctor and .cctor are valid despite starting with a dot
				return identifier == ".ctor" || identifier == ".cctor";
			}
			for (int i = 1; i < identifier.Length; i++) {
				if (!(char.IsLetterOrDigit(identifier[i]) || IsValidIdentifierCharacter(identifier[i]) || identifier[i] == '.'))
					return false;
			}
			return true;
		}

		static readonly HashSet<string> ilKeywords = BuildKeywordList(
			"abstract", "algorithm", "alignment", "ansi", "any", "arglist",
			"array", "as", "assembly", "assert", "at", "auto", "autochar", "beforefieldinit",
			"blob", "blob_object", "bool", "brnull", "brnull.s", "brzero", "brzero.s", "bstr",
			"bytearray", "byvalstr", "callmostderived", "carray", "catch", "cdecl", "cf",
			"char", "cil", "class", "clsid", "const", "currency", "custom", "date", "decimal",
			"default", "demand", "deny", "endmac", "enum", "error", "explicit", "extends", "extern",
			"false", "famandassem", "family", "famorassem", "fastcall", "fault", "field", "filetime",
			"filter", "final", "finally", "fixed", "float", "float32", "float64", "forwardref",
			"fromunmanaged", "handler", "hidebysig", "hresult", "idispatch", "il", "illegal",
			"implements", "implicitcom", "implicitres", "import", "in", "inheritcheck", "init",
			"initonly", "instance", "int", "int16", "int32", "int64", "int8", "interface", "internalcall",
			"iunknown", "lasterr", "lcid", "linkcheck", "literal", "localloc", "lpstr", "lpstruct", "lptstr",
			"lpvoid", "lpwstr", "managed", "marshal", "method", "modopt", "modreq", "native", "nested",
			"newslot", "noappdomain", "noinlining", "nomachine", "nomangle", "nometadata", "noncasdemand",
			"noncasinheritance", "noncaslinkdemand", "noprocess", "not", "not_in_gc_heap", "notremotable",
			"notserialized", "null", "nullref", "object", "objectref", "opt", "optil", "out",
			"permitonly", "pinned", "pinvokeimpl", "prefix1", "prefix2", "prefix3", "prefix4", "prefix5", "prefix6",
			"prefix7", "prefixref", "prejitdeny", "prejitgrant", "preservesig", "private", "privatescope", "protected",
			"public", "record", "refany", "reqmin", "reqopt", "reqrefuse", "reqsecobj", "request", "retval",
			"rtspecialname", "runtime", "safearray", "sealed", "sequential", "serializable", "special", "specialname",
			"static", "stdcall", "storage", "stored_object", "stream", "streamed_object", "string", "struct",
			"synchronized", "syschar", "sysstring", "tbstr", "thiscall", "tls", "to", "true", "typedref",
			"unicode", "unmanaged", "unmanagedexp", "unsigned", "unused", "userdefined", "value", "valuetype",
			"vararg", "variant", "vector", "virtual", "void", "wchar", "winapi", "with", "wrapper",

			// These are not listed as keywords in spec, but ILAsm treats them as such
			"property", "type", "flags", "callconv", "strict"
		);

		static HashSet<string> BuildKeywordList(params string[] keywords)
		{
			HashSet<string> s = new HashSet<string>(keywords);
			foreach (var field in typeof(OpCodes).GetFields()) {
				if(field.FieldType == typeof(OpCode))
					s.Add(((OpCode)field.GetValue(null)).Name);
			}
			return s;
		}

		public static string Escape(string identifier)
		{
			if (IsValidIdentifier(identifier) && !ilKeywords.Contains(identifier)) {
				return identifier;
			} else {
				// The ECMA specification says that ' inside SQString should be ecaped using an octal escape sequence,
				// but we follow Microsoft's ILDasm and use \'.
				return "'" + EscapeString(identifier).Replace("'", "\\'") + "'";
			}
		}

		public static void WriteTo(this TypeSig type, ITextOutput writer, ILNameSyntax syntax = ILNameSyntax.Signature)
		{
			ILNameSyntax syntaxForElementTypes = syntax == ILNameSyntax.SignatureNoNamedTypeParameters ? syntax : ILNameSyntax.Signature;
			if (type is PinnedSig) {
				((PinnedSig)type).Next.WriteTo(writer, syntaxForElementTypes);
				writer.Write(" pinned");
			} else if (type is ArraySig) {
				ArraySig at = (ArraySig)type;
				at.Next.WriteTo(writer, syntaxForElementTypes);
				writer.Write('[');
				for (int i = 0; i < at.Rank; i++)
				{
					if (i != 0) {
						writer.Write(", ");
					}
					int? lower = i < at.LowerBounds.Count ? at.LowerBounds[i] : (int?)null;
					uint? size = i < at.Sizes.Count ? at.Sizes[i] : (uint?)null;
					if (lower != null)
					{
						writer.Write(lower.ToString());
						if (size != null) {
							writer.Write("..");
							writer.Write((lower.Value + (int)size.Value - 1).ToString());
						}
						else
							writer.Write("...");
					}
				}
				writer.Write(']');
			} else if (type is SZArraySig) {
				SZArraySig at = (SZArraySig)type;
				at.Next.WriteTo(writer, syntaxForElementTypes);
				writer.Write('[');
				writer.Write(']');
			} else if (type is GenericSig) {
				if (((GenericSig)type).IsMethodVar)
					writer.Write("!!");
				else
					writer.Write('!');
				string typeName = type.TypeName;
				if (string.IsNullOrEmpty(typeName) || typeName[0] == '!' || syntax == ILNameSyntax.SignatureNoNamedTypeParameters)
					writer.Write(((GenericSig)type).Number.ToString());
				else
					writer.Write(Escape(typeName));
			} else if (type is ByRefSig) {
				((ByRefSig)type).Next.WriteTo(writer, syntaxForElementTypes);
				writer.Write('&');
			} else if (type is PtrSig) {
				((PtrSig)type).Next.WriteTo(writer, syntaxForElementTypes);
				writer.Write('*');
			} else if (type is GenericInstSig) {
				((GenericInstSig)type).GenericType.WriteTo(writer, syntaxForElementTypes);
				writer.Write('<');
				var arguments = ((GenericInstSig)type).GenericArguments;
				for (int i = 0; i < arguments.Count; i++) {
					if (i > 0)
						writer.Write(", ");
					arguments[i].WriteTo(writer, syntaxForElementTypes);
				}
				writer.Write('>');
			} else if (type is CModOptSig) {
				((CModOptSig)type).Next.WriteTo(writer, syntax);
				writer.Write(" modopt(");
				((CModOptSig)type).Modifier.WriteTo(writer, ILNameSyntax.TypeName);
				writer.Write(") ");
			} else if (type is CModReqdSig) {
				((CModReqdSig)type).Next.WriteTo(writer, syntax);
				writer.Write(" modreq(");
				((CModReqdSig)type).Modifier.WriteTo(writer, ILNameSyntax.TypeName);
				writer.Write(") ");
			} else if (type is FnPtrSig fpt) {
				writer.Write("method ");
				fpt.MethodSig.RetType.WriteTo(writer, syntax);
				writer.Write(" *(");
				bool first = true;
				foreach (var p in fpt.MethodSig.Params) {
					if (first)
						first = false;
					else
						writer.Write(", ");
					p.WriteTo(writer, syntax);
				}
				writer.Write(')');
			} else if (type is SentinelSig) {
				writer.Write("..., ");
				((SentinelSig)type).Next.WriteTo(writer, syntax);
			} else if (type is TypeDefOrRefSig tdrs) {
				ThreeState isVT;
				if (tdrs is ClassSig)
					isVT = ThreeState.False;
				else if (tdrs is ValueTypeSig)
					isVT = ThreeState.True;
				else
					isVT = ThreeState.Unknown;
				WriteTo(tdrs.TypeDefOrRef, writer, syntax, isVT);
			}
		}

		public static void WriteTo(this ITypeDefOrRef type, ITextOutput writer, ILNameSyntax syntax = ILNameSyntax.Signature)
		{
			type.WriteTo(writer, syntax, ThreeState.Unknown);
		}

		internal static void WriteTo(this ITypeDefOrRef type, ITextOutput writer, ILNameSyntax syntax, ThreeState isValueType)
		{
			if (type == null)
				return;
			var ts = type as TypeSpec;
			if (ts != null) {
				WriteTo(((TypeSpec)type).TypeSig, writer, syntax);
				return;
			}
			string typeFullName = type.FullName;
			string typeName = type.Name.String;
			TypeSig typeSig = null;
			string name = type.DefinitionAssembly.IsCorLib() ? PrimitiveTypeName(typeFullName, type.Module, out typeSig) : null;
			if (syntax == ILNameSyntax.ShortTypeName) {
				if (name != null)
					WriteKeyword(writer, name, typeSig.ToTypeDefOrRef());
				else
					writer.WriteReference(Escape(typeName), type);
			} else if ((syntax == ILNameSyntax.Signature || syntax == ILNameSyntax.SignatureNoNamedTypeParameters) && name != null) {
				WriteKeyword(writer, name, typeSig.ToTypeDefOrRef());
			} else {
				if (syntax == ILNameSyntax.Signature || syntax == ILNameSyntax.SignatureNoNamedTypeParameters) {
					bool isVT;
					if (isValueType != ThreeState.Unknown)
						isVT = isValueType == ThreeState.True;
					else
						isVT = CecilExtensions.IsValueType(type);
					writer.Write(isVT ? "valuetype" : "class");
					writer.Write(" ");
				}

				if (type.DeclaringType != null) {
					type.DeclaringType.WriteTo(writer, ILNameSyntax.TypeName, ThreeState.Unknown);
					writer.Write("/");
					writer.WriteReference(Escape(typeName), type);
				} else {
					if (!(type is TypeDef) && type.Scope != null && !(type is TypeSpec)) {
						writer.Write("[");
						writer.WriteReference(Escape(type.Scope.GetScopeName()), type.Scope);
						writer.Write("]");
					}

					if (ts != null || !IsValidIdentifier(typeFullName))
						writer.WriteReference(Escape(typeFullName), type);
					else {
						WriteNamespace(writer, type.Namespace, type.DefinitionAssembly);
						if (!string.IsNullOrEmpty(type.Namespace))
							writer.Write(".");
						writer.WriteReference(Escape(type.Name), type);
					}
				}
			}
		}

		internal static void WriteNamespace(ITextOutput writer, string ns, IAssembly nsAsm)
		{
			var sb = Interlocked.CompareExchange(ref cachedStringBuilder, null, cachedStringBuilder) ?? new StringBuilder();
			sb.Clear();
			var parts = ns.Split('.');
			for (int i = 0; i < parts.Length; i++) {
				if (i > 0) {
					sb.Append('.');
					writer.Write(".");
				}
				var nsPart = parts[i];
				sb.Append(nsPart);
				if (!string.IsNullOrEmpty(nsPart)) {
					writer.Write(Escape(nsPart));
				}
			}
			if (sb.Capacity <= 1000)
				cachedStringBuilder = sb;
		}
		static StringBuilder cachedStringBuilder = new StringBuilder();

		internal static void WriteKeyword(ITextOutput writer, string name, ITypeDefOrRef tdr)
		{
			var parts = name.Split(' ');
			for (int i = 0; i < parts.Length; i++) {
				if (i > 0)
					writer.Write(" ");
				if (tdr != null)
					writer.Write(parts[i], tdr);
				else
					writer.Write(parts[i]);
			}
		}

		public static void WriteOperand(ITextOutput writer, object operand)
		{
			if (operand == null)
				throw new ArgumentNullException(nameof(operand));

			Instruction targetInstruction = operand as Instruction;
			if (targetInstruction != null) {
				WriteOffsetReference(writer, targetInstruction);
				return;
			}

			Instruction[] targetInstructions = operand as Instruction[];
			if (targetInstructions != null) {
				WriteLabelList(writer, targetInstructions);
				return;
			}

			Local variableRef = operand as Local;
			if (variableRef != null) {
				writer.WriteReference(variableRef.Index.ToString(), variableRef);
				return;
			}

			Parameter paramRef = operand as Parameter;
			if (paramRef != null) {
				if (string.IsNullOrEmpty(paramRef.Name)) {
					if (paramRef.IsHiddenThisParameter)
						writer.Write("<hidden-this>", paramRef);
					else
						writer.Write(paramRef.MethodSigIndex.ToString(), paramRef);
				}
				else
					writer.Write(Escape(paramRef.Name), paramRef);
				return;
			}

			MemberRef memberRef = operand as MemberRef;
			if (memberRef != null) {
				if (memberRef.IsMethodRef)
					memberRef.WriteMethodTo(writer);
				else
					memberRef.WriteFieldTo(writer);
				return;
			}

			MethodDef methodDef = operand as MethodDef;
			if (methodDef != null) {
				methodDef.WriteMethodTo(writer);
				return;
			}

			FieldDef fieldDef = operand as FieldDef;
			if (fieldDef != null) {
				fieldDef.WriteFieldTo(writer);
				return;
			}

			ITypeDefOrRef typeRef = operand as ITypeDefOrRef;
			if (typeRef != null) {
				typeRef.WriteTo(writer, ILNameSyntax.TypeName);
				return;
			}

			IMethod m = operand as IMethod;
			if (m != null) {
				m.WriteMethodTo(writer);
				return;
			}

			MethodSig sig = operand as MethodSig;
			if (sig != null) {
				sig.WriteTo(writer);
				return;
			}

			string s = operand as string;
			if (s != null) {
				WriteOperand(writer, s);
			} else if (operand is char) {
				writer.Write(((int)(char)operand).ToString());
			} else if (operand is float) {
				WriteOperand(writer, (float)operand);
			} else if (operand is double) {
				WriteOperand(writer, (double)operand);
			} else if (operand is bool) {
				writer.Write((bool)operand ? "true" : "false");
			} else {
				s = ToInvariantCultureString(operand);
				writer.Write(s);
			}
		}

		public static void WriteOperand(ITextOutput writer, long val)
		{
			writer.Write(ToInvariantCultureString(val));
		}

		public static void WriteOperand(ITextOutput writer, float val)
		{
			if (val == 0) {
				if (1 / val == float.NegativeInfinity) {
					// negative zero is a special case
					writer.Write('-');
				}
				writer.Write("0.0");
			} else if (float.IsInfinity(val) || float.IsNaN(val)) {
				byte[] data = BitConverter.GetBytes(val);
				writer.Write('(');
				for (int i = 0; i < data.Length; i++) {
					if (i > 0)
						writer.Write(' ');
					writer.Write(data[i].ToString("X2"));
				}
				writer.Write(')');
			} else {
				writer.Write(val.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
			}
		}

		public static void WriteOperand(ITextOutput writer, double val)
		{
			if (val == 0) {
				if (1 / val == double.NegativeInfinity) {
					// negative zero is a special case
					writer.Write('-');
				}
				writer.Write("0.0");
			} else if (double.IsInfinity(val) || double.IsNaN(val)) {
				byte[] data = BitConverter.GetBytes(val);
				writer.Write('(');
				for (int i = 0; i < data.Length; i++) {
					if (i > 0)
						writer.Write(' ');
					writer.Write(data[i].ToString("X2"));
				}
				writer.Write(')');
			} else {
				writer.Write(val.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
			}
		}

		public static void WriteOperand(ITextOutput writer, string operand)
		{
			writer.Write('"');
			writer.Write(EscapeString(operand));
			writer.Write('"');
		}

		public static string EscapeString(string str)
		{
			StringBuilder sb = new StringBuilder();
			foreach (char ch in str) {
				switch (ch) {
					case '"':
						sb.Append("\\\"");
						break;
					case '\\':
						sb.Append("\\\\");
						break;
					case '\0':
						sb.Append("\\0");
						break;
					case '\a':
						sb.Append("\\a");
						break;
					case '\b':
						sb.Append("\\b");
						break;
					case '\f':
						sb.Append("\\f");
						break;
					case '\n':
						sb.Append("\\n");
						break;
					case '\r':
						sb.Append("\\r");
						break;
					case '\t':
						sb.Append("\\t");
						break;
					case '\v':
						sb.Append("\\v");
						break;
					default:
						// print control characters and uncommon white spaces as numbers
						if (char.IsControl(ch) || char.IsSurrogate(ch) || (char.IsWhiteSpace(ch) && ch != ' ')) {
							sb.Append("\\u" + ((int)ch).ToString("x4"));
						} else {
							sb.Append(ch);
						}
						break;
				}
			}
			return sb.ToString();
		}

		public static string PrimitiveTypeName(string fullName, ModuleDef module, out TypeSig typeSig)
		{
			var corLibTypes = module == null ? null : module.CorLibTypes;
			typeSig = null;
			switch (fullName) {
				case "System.SByte":
					if (corLibTypes != null)
						typeSig = corLibTypes.SByte;
					return "int8";
				case "System.Int16":
					if (corLibTypes != null)
						typeSig = corLibTypes.Int16;
					return "int16";
				case "System.Int32":
					if (corLibTypes != null)
						typeSig = corLibTypes.Int32;
					return "int32";
				case "System.Int64":
					if (corLibTypes != null)
						typeSig = corLibTypes.Int64;
					return "int64";
				case "System.Byte":
					if (corLibTypes != null)
						typeSig = corLibTypes.Byte;
					return "uint8";
				case "System.UInt16":
					if (corLibTypes != null)
						typeSig = corLibTypes.UInt16;
					return "uint16";
				case "System.UInt32":
					if (corLibTypes != null)
						typeSig = corLibTypes.UInt32;
					return "uint32";
				case "System.UInt64":
					if (corLibTypes != null)
						typeSig = corLibTypes.UInt64;
					return "uint64";
				case "System.Single":
					if (corLibTypes != null)
						typeSig = corLibTypes.Single;
					return "float32";
				case "System.Double":
					if (corLibTypes != null)
						typeSig = corLibTypes.Double;
					return "float64";
				case "System.Void":
					if (corLibTypes != null)
						typeSig = corLibTypes.Void;
					return "void";
				case "System.Boolean":
					if (corLibTypes != null)
						typeSig = corLibTypes.Boolean;
					return "bool";
				case "System.String":
					if (corLibTypes != null)
						typeSig = corLibTypes.String;
					return "string";
				case "System.Char":
					if (corLibTypes != null)
						typeSig = corLibTypes.Char;
					return "char";
				case "System.Object":
					if (corLibTypes != null)
						typeSig = corLibTypes.Object;
					return "object";
				case "System.IntPtr":
					if (corLibTypes != null)
						typeSig = corLibTypes.IntPtr;
					return "native int";
				case "System.UIntPtr":
					if (corLibTypes != null)
						typeSig = corLibTypes.UIntPtr;
					return "native unsigned int";
				case "System.TypedReference":
					if (corLibTypes != null)
						typeSig = corLibTypes.TypedReference;
					return "typedref";
				default:
					return null;
			}
		}
	}
}
