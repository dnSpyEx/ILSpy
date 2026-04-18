using System;
using System.Collections.Immutable;
using System.Linq;
using dnlib.DotNet;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem
{
	internal static class SignatureExtensions
	{
		internal static IType DecodeSignature(this TypeSpec spec, MetadataModule module, GenericContext context)
		{
			return spec?.TypeSig.DecodeSignature(module, context);
		}

		internal static IType DecodeSignature(this TypeSig sig, MetadataModule module, GenericContext context)
		{
			if (sig is null)
				return null;

			switch (sig) {
				case CorLibTypeSig corLibTypeSig:
					return module.Compilation.FindType(corLibTypeSig.ElementType.ToKnownTypeCode());
				case GenericMVar mVar:
					return context.GetMethodTypeParameter((int)mVar.Number);
				case GenericVar tVar:
					return context.GetClassTypeParameter((int)tVar.Number);
				case FnPtrSig fnPtr: {
					// pointers to member functions are not supported even in C# 9
					if (!fnPtr.Signature.HasThis && fnPtr.Signature is MethodBaseSig mSig) {
						var retType = mSig.RetType.DecodeSignature(module, context);
						var paramTypes = mSig.Params.Select(t => t.DecodeSignature(module, context)).ToList();
						return FunctionPointerType.FromSignature(retType, paramTypes, mSig.CallingConvention, module);
					}

					return module.Compilation.FindType(KnownTypeCode.IntPtr);
				}
				case GenericInstSig instSig:
					return new ParameterizedType(instSig.GenericType.DecodeSignature(module, context),
						instSig.GenericArguments.Select(x => x.DecodeSignature(module, context)));
				case ByRefSig byRefSig:
					return new ByReferenceType(byRefSig.Next.DecodeSignature(module, context));
				case PinnedSig pinnedSig:
					return new PinnedType(pinnedSig.Next.DecodeSignature(module, context));
				case CModOptSig cModOptSig:
					return new ModifiedType(cModOptSig.Modifier.DecodeSignature(module, context), cModOptSig.Next.DecodeSignature(module, context), false);
				case CModReqdSig cModReqdSig:
					return new ModifiedType(cModReqdSig.Modifier.DecodeSignature(module, context), cModReqdSig.Next.DecodeSignature(module, context), true);
				case PtrSig ptrSig:
					return new PointerType(ptrSig.Next.DecodeSignature(module, context));
				case ArraySigBase arraySigBase:
					return new ArrayType(module.Compilation, arraySigBase.Next.DecodeSignature(module, context), (int)arraySigBase.Rank);
				case ClassOrValueTypeSig classOrValueTypeSig:
					ThreeState isVT = ThreeState.Unknown;
					if (classOrValueTypeSig is ClassSig)
						isVT = ThreeState.False;
					else if (classOrValueTypeSig is ValueTypeSig)
						isVT = ThreeState.True;

					return classOrValueTypeSig.TypeDefOrRef.DecodeSignature(module, context, isVT);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		internal static IType DecodeSignature(this ITypeDefOrRef typeDefOrRef, MetadataModule module, GenericContext context, ThreeState isVT = ThreeState.Unknown)
		{
			if (typeDefOrRef is null)
				return null;

			if (typeDefOrRef is TypeSpec spec) {
				return spec.DecodeSignature(module, context);
			}
			CorLibTypeSig corLibTypeSig = typeDefOrRef.Module?.CorLibTypes.GetCorLibTypeSig(typeDefOrRef);
			if (corLibTypeSig != null) {
				return corLibTypeSig.DecodeSignature(module, context);
			}

			if (typeDefOrRef is TypeDef def) {
				var resolved = module.GetDefinition(def);
				return resolved;
			} else {
				bool? isReferenceType;
				if (isVT != ThreeState.Unknown)
					isReferenceType = isVT == ThreeState.False;
				else
					isReferenceType = null;
				FullTypeName fullTypeName = typeDefOrRef.GetFullTypeName();
				foreach (var asm in module.Compilation.Modules)
				{

					var type = asm.GetTypeDefinition(fullTypeName);
					if (type != null)
						return type;
				}
				return new UnknownType(fullTypeName, isReferenceType);
			}
		}

		internal static ImmutableArray<IType> DecodeSignature(this MethodSpec spec, MetadataModule module, GenericContext context)
		{
			if (spec is null || spec.GenericInstMethodSig is null)
				return ImmutableArray<IType>.Empty;

			return spec.GenericInstMethodSig.GenericArguments.Select(sig => sig.DecodeSignature(module, context))
					   .ToImmutableArray();
		}
	}
}
