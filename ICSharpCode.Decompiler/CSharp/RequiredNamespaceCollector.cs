using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

using IField = ICSharpCode.Decompiler.TypeSystem.IField;
using IMethod = ICSharpCode.Decompiler.TypeSystem.IMethod;
using IType = ICSharpCode.Decompiler.TypeSystem.IType;

namespace ICSharpCode.Decompiler.CSharp
{
	class RequiredNamespaceCollector
	{
		static readonly GenericContext genericContext = default;

		readonly HashSet<string> namespaces;
		readonly HashSet<IType> visitedTypes = new HashSet<IType>();

		public RequiredNamespaceCollector(HashSet<string> namespaces)
		{
			this.namespaces = namespaces;
			for (int i = 0; i < KnownTypeReference.KnownTypeCodeCount; i++)
			{
				var ktr = KnownTypeReference.Get((KnownTypeCode)i);
				if (ktr == null)
					continue;
				namespaces.Add(ktr.Namespace);
			}
		}

		public static void CollectAttributeNamespacesOnlyModule(MetadataModule module, HashSet<string> namespaces)
		{
			var collector = new RequiredNamespaceCollector(namespaces);
			collector.HandleAttributes(module.GetModuleAttributes());
		}

		public static void CollectAttributeNamespacesOnlyAssembly(MetadataModule module, HashSet<string> namespaces)
		{
			var collector = new RequiredNamespaceCollector(namespaces);
			collector.HandleAttributes(module.GetAssemblyAttributes());
		}

		public static void CollectNamespacesOnlyType(ITypeDefinition entity, HashSet<string> namespaces)
		{
			var collector = new RequiredNamespaceCollector(namespaces);
			collector.CollectNamespacesOnlyType(entity);
		}

		public static void CollectNamespaces(IEntity entity, MetadataModule module, HashSet<string> namespaces, CodeMappingInfo mappingInfo = null)
		{
			var collector = new RequiredNamespaceCollector(namespaces);
			collector.CollectNamespaces(entity, module, mappingInfo);
		}

		void CollectNamespacesOnlyType(ITypeDefinition entity)
		{
			if (entity == null)
				return;

			namespaces.Add(entity.Namespace);
			HandleAttributes(entity.GetAttributes());
			HandleTypeParameters(entity.TypeParameters);

			foreach (var baseType in entity.DirectBaseTypes)
				CollectNamespacesForTypeReference(baseType);
		}

		void CollectNamespaces(IEntity entity, MetadataModule module, CodeMappingInfo mappingInfo = null)
		{
			if (entity == null || entity.MetadataToken is null)
				return;
			if (mappingInfo == null && entity.MetadataToken is IMemberDef def)
				mappingInfo = CSharpDecompiler.GetCodeMappingInfo(def.DeclaringType ?? def as TypeDef);
			switch (entity) {
				case ITypeDefinition td:
					var extensionInfo = td.DeclaringTypeDefinition?.ExtensionInfo ?? td.DeclaringTypeDefinition?.DeclaringTypeDefinition?.ExtensionInfo;
					if (extensionInfo?.IsExtensionMarkerType(td, out _) == true || extensionInfo?.IsExtensionGroupType(td) == true)
					{
						td = extensionInfo.Container;
					}

					namespaces.Add(td.Namespace);
					HandleAttributes(td.GetAttributes());
					HandleTypeParameters(td.TypeParameters);

					foreach (var baseType in td.DirectBaseTypes)
					{
						CollectNamespacesForTypeReference(baseType);
					}

					foreach (var nestedType in td.NestedTypes)
					{
						bool isGroupOrMarker = extensionInfo?.IsExtensionGroupType(nestedType) == true || extensionInfo?.IsExtensionMarkerType(nestedType, out _) == true;
						if (isGroupOrMarker)
							continue;
						CollectNamespaces(nestedType, module, mappingInfo);
					}

					foreach (var field in td.Fields)
					{
						CollectNamespaces(field, module, mappingInfo);
					}

					foreach (var property in td.Properties)
					{
						CollectNamespaces(property, module, mappingInfo);
					}

					foreach (var @event in td.Events)
					{
						CollectNamespaces(@event, module, mappingInfo);
					}

					foreach (var method in td.Methods)
					{
						CollectNamespaces(method, module, mappingInfo);
					}
					break;
				case IField field:
					HandleAttributes(field.GetAttributes());
					CollectNamespacesForTypeReference(field.ReturnType);
					break;
				case IMethod method:
					if (mappingInfo is null)
					{
						var part = (MethodDef)method.MetadataToken;
						HandleAttributes(method.GetAttributes());
						HandleAttributes(method.GetReturnTypeAttributes());
						CollectNamespacesForTypeReference(method.ReturnType);
						foreach (var param in method.Parameters)
						{
							HandleAttributes(param.GetAttributes());
							CollectNamespacesForTypeReference(param.Type);
						}
						HandleTypeParameters(method.TypeParameters);
						HandleOverrides(part.Overrides, module);
						if (part.HasBody)
							CollectNamespacesFromMethodBody(part.Body, module);
					}
					else
					{
						var parts = mappingInfo.GetMethodParts((MethodDef)method.MetadataToken).ToList();
						foreach (var part in parts) {
							var partMethod = module.ResolveMethod(part, genericContext);
							HandleAttributes(partMethod.GetAttributes());
							HandleAttributes(partMethod.GetReturnTypeAttributes());
							CollectNamespacesForTypeReference(partMethod.ReturnType);
							foreach (var param in partMethod.Parameters)
							{
								HandleAttributes(param.GetAttributes());
								CollectNamespacesForTypeReference(param.Type);
							}
							HandleTypeParameters(partMethod.TypeParameters);
							HandleOverrides(part.Overrides, module);
							if (part.HasBody)
								CollectNamespacesFromMethodBody(part.Body, module);
						}
					}
					break;
				case IProperty property:
					HandleAttributes(property.GetAttributes());
					CollectNamespacesForTypeReference(property.ReturnType);
					CollectNamespaces(property.Getter, module, mappingInfo);
					CollectNamespaces(property.Setter, module, mappingInfo);
					break;
				case IEvent @event:
					HandleAttributes(@event.GetAttributes());
					CollectNamespacesForTypeReference(@event.ReturnType);
					CollectNamespaces(@event.AddAccessor, module, mappingInfo);
					CollectNamespaces(@event.RemoveAccessor, module, mappingInfo);
					break;
			}
		}

		void HandleOverrides(IList<MethodOverride> immutableArray, MetadataModule module)
		{
			foreach (var methodImpl in immutableArray) {
				CollectNamespacesForTypeReference(module.ResolveType(methodImpl.MethodDeclaration.DeclaringType, genericContext));
				CollectNamespacesForMemberReference(module.ResolveMethod(methodImpl.MethodBody, genericContext));
				CollectNamespacesForMemberReference(module.ResolveMethod(methodImpl.MethodDeclaration, genericContext));
			}
		}

		void CollectNamespacesForTypeReference(IType type)
		{
			if (!visitedTypes.Add(type))
				return;
			switch (type)
			{
				case ParameterizedType parameterizedType:
					CollectNamespacesForTypeReference(parameterizedType.GenericType);
					foreach (var arg in parameterizedType.TypeArguments)
						CollectNamespacesForTypeReference(arg);
					return; // no need to collect base types again
				case TypeWithElementType typeWithElementType:
					CollectNamespacesForTypeReference(typeWithElementType.ElementType);
					break;
				case TupleType tupleType:
					foreach (var elementType in tupleType.ElementTypes)
					{
						CollectNamespacesForTypeReference(elementType);
					}
					break;
				case FunctionPointerType fnPtrType:
					CollectNamespacesForTypeReference(fnPtrType.ReturnType);
					foreach (var paramType in fnPtrType.ParameterTypes)
					{
						CollectNamespacesForTypeReference(paramType);
					}
					break;
				default:
					namespaces.Add(type.Namespace);
					break;
			}
			foreach (var baseType in type.GetAllBaseTypes())
			{
				namespaces.Add(baseType.Namespace);
			}
		}

		public static void CollectNamespaces(IMDTokenProvider entity, MetadataModule module, HashSet<string> namespaces)
		{
			if (entity is null)
				return;
			CollectNamespaces(module.ResolveEntity(entity, genericContext), module, namespaces);
		}

		void HandleAttributes(IEnumerable<IAttribute> attributes)
		{
			foreach (var attr in attributes)
			{
				namespaces.Add(attr.AttributeType.Namespace);
				foreach (var arg in attr.FixedArguments)
				{
					HandleAttributeValue(arg.Type, arg.Value);
				}
				foreach (var arg in attr.NamedArguments)
				{
					HandleAttributeValue(arg.Type, arg.Value);
				}
			}
		}

		void HandleAttributeValue(IType type, object value)
		{
			CollectNamespacesForTypeReference(type);
			if (value is IType typeofType)
				CollectNamespacesForTypeReference(typeofType);
			if (value is ImmutableArray<CustomAttributeTypedArgument<IType>> arr)
			{
				foreach (var element in arr)
				{
					HandleAttributeValue(element.Type, element.Value);
				}
			}
		}

		void HandleTypeParameters(IEnumerable<ITypeParameter> typeParameters)
		{
			foreach (var typeParam in typeParameters)
			{
				HandleAttributes(typeParam.GetAttributes());

				foreach (var constraint in typeParam.DirectBaseTypes)
				{
					CollectNamespacesForTypeReference(constraint);
				}
			}
		}

		void CollectNamespacesFromMethodBody(CilBody method, MetadataModule module)
		{
			foreach (Local local in method.Variables) {
				var decoded = module.ResolveType(local.Type, genericContext);
				CollectNamespacesForTypeReference(decoded);
			}

			foreach (var region in method.ExceptionHandlers) {
				if (region.CatchType is null)
					continue;
				IType ty;
				try {
					ty = module.ResolveType(region.CatchType, genericContext);
				} catch (BadImageFormatException) {
					continue;
				}
				CollectNamespacesForTypeReference(ty);
			}

			for (int i = 0; i < method.Instructions.Count; i++) {
				var instr = method.Instructions[i];
				switch (instr.OpCode.OperandType) {
					case OperandType.InlineType when instr.Operand != null: {
						IType type;
						try {
							type = module.ResolveType(instr.Operand as dnlib.DotNet.ITypeDefOrRef, genericContext);
						} catch (BadImageFormatException) {
							break;
						}
						CollectNamespacesForTypeReference(type);
						break;
					}
					case OperandType.InlineField when instr.Operand != null:
					case OperandType.InlineMethod when instr.Operand != null: {
						IMember member;
						try {
							member = module.ResolveEntity(instr.Operand as IMDTokenProvider, genericContext) as IMember;
						} catch (BadImageFormatException) {
							break;
						}
						CollectNamespacesForMemberReference(member);
						break;
					}
					case OperandType.InlineSig when instr.Operand != null:
						if (instr.Operand is MethodBaseSig baseSig) {
							try {
								CollectNamespacesForTypeReference(baseSig.RetType.DecodeSignature(module, genericContext));
								foreach (var paramType in baseSig.Params) {
									CollectNamespacesForTypeReference(paramType.DecodeSignature(module, genericContext));
								}
							} catch (BadImageFormatException) {

							}
						}
						break;
					case OperandType.InlineTok when instr.Operand != null: {
						if (instr.Operand is dnlib.DotNet.ITypeDefOrRef dnType) {
							IType type;
							try {
								type = module.ResolveType(dnType, genericContext);
							} catch (BadImageFormatException) {
								break;
							}
							CollectNamespacesForTypeReference(type);
						}
						else if (instr.Operand is IMemberRef dnMemberRef) {
							IMember member;
							try {
								member = module.ResolveEntity(dnMemberRef, genericContext) as IMember;
							} catch (BadImageFormatException) {
								break;
							}
							CollectNamespacesForMemberReference(member);
						}
						break;
					}
				}
			}
		}

		void CollectNamespacesForMemberReference(IMember member)
		{
			switch (member)
			{
				case IField field:
					CollectNamespacesForTypeReference(field.DeclaringType);
					CollectNamespacesForTypeReference(field.ReturnType);
					break;
				case IMethod method:
					CollectNamespacesForTypeReference(method.DeclaringType);
					CollectNamespacesForTypeReference(method.ReturnType);
					foreach (var param in method.Parameters)
						CollectNamespacesForTypeReference(param.Type);
					foreach (var arg in method.TypeArguments)
						CollectNamespacesForTypeReference(arg);
					break;
			}
		}
	}
}
