// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;
using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Thunk to marshal delegate parameters and invoke the appropriate delegate function pointer
    /// </summary>
    internal class DelegateMarshallingMethodThunk : ILStubMethod
    {
        private readonly TypeDesc _owningType;
        private readonly TypeDesc _delegateType;
        private readonly string _name;
        private readonly InteropStateManager _interopStateManager;
        private readonly MethodDesc _invokeMethod;
        private MethodSignature _signature;         // signature of the native callable marshalling stub

        public DelegateMarshallingMethodThunk(TypeDesc owningType, TypeDesc delegateType, InteropStateManager interopStateManager, string name)
        {
            _owningType = owningType;
            _delegateType = delegateType;
            _invokeMethod = delegateType.GetMethod("Invoke", null);
            _name = name;
            _interopStateManager = interopStateManager;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override bool IsNativeCallable
        {
            get
            {
                return true;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        public TypeDesc DelegateType
        {
            get
            {
                return _delegateType;
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    // TODO: Parse UnmanagedFunctionPointerAttribute 
                    bool isAnsi = true;
                    MethodSignature delegateSignature = _invokeMethod.Signature;
                    TypeDesc[] nativeParameterTypes = new TypeDesc[delegateSignature.Length];
                    ParameterMetadata[] parameterMetadataArray = _invokeMethod.GetParameterMetadata();
                    int parameterIndex = 0;

                    MarshalAsDescriptor marshalAs = null;
                    if (parameterMetadataArray != null && parameterMetadataArray.Length > 0 && parameterMetadataArray[0].Index == 0)
                    {
                        marshalAs = parameterMetadataArray[parameterIndex++].MarshalAsDescriptor;
                    }

                    TypeDesc nativeReturnType = MarshalHelpers.GetNativeMethodParameterType(delegateSignature.ReturnType, null, _interopStateManager, true, isAnsi);
                    for (int i = 0; i < delegateSignature.Length; i++)
                    {
                        int sequence = i + 1;
                        Debug.Assert(parameterIndex == parameterMetadataArray.Length || sequence <= parameterMetadataArray[parameterIndex].Index);
                        if (parameterIndex == parameterMetadataArray.Length || sequence < parameterMetadataArray[parameterIndex].Index)
                        {
                            // if we don't have metadata for the parameter, marshalAs is null
                            marshalAs = null;
                        }
                        else
                        {
                            Debug.Assert(sequence == parameterMetadataArray[parameterIndex].Index);
                            marshalAs = parameterMetadataArray[parameterIndex++].MarshalAsDescriptor;
                        }

                        nativeParameterTypes[i] = MarshalHelpers.GetNativeMethodParameterType(delegateSignature[i], marshalAs, _interopStateManager, false, isAnsi);
                     }
                    _signature = new MethodSignature(MethodSignatureFlags.Static, 0, nativeReturnType, nativeParameterTypes);
                }
                return _signature;
            }
        }

        public override ParameterMetadata[] GetParameterMetadata()
        {
            return _invokeMethod.GetParameterMetadata();
        }

        public override PInvokeMetadata GetPInvokeMethodMetadata()
        {
            return _invokeMethod.GetPInvokeMethodMetadata();
        }

        public MethodSignature DelegateSignature
        {
            get
            {
                return _invokeMethod.Signature;
            }
        }


        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override MethodIL EmitIL()
        {
            return PInvokeILEmitter.EmitIL(this, default(PInvokeILEmitterConfiguration), _interopStateManager);
        }
    }

    
    internal struct DelegateInvokeMethodSignature : IEquatable<DelegateInvokeMethodSignature>
    {
        public  readonly MethodSignature Signature;

        public DelegateInvokeMethodSignature(TypeDesc delegateType)
        {
            MethodDesc invokeMethod = delegateType.GetMethod("Invoke", null);
            Signature = invokeMethod.Signature;
        }

        public override int GetHashCode()
        {
            return Signature.GetHashCode();
        }

        // TODO: Use the MarshallerKind for each parameter to compare whether two signatures are similar(ie. whether two delegates can share marshalling stubs)
        public bool Equals(DelegateInvokeMethodSignature other)
        {
            if (Signature.ReturnType != other.Signature.ReturnType)
                return false;

            if (Signature.Length != other.Signature.Length)
                return false;

            for (int i = 0; i < Signature.Length; i++)
            {
                if (Signature[i] != other.Signature[i])
                    return false;
            }

            return true;
        }
    }

}
