namespace RedBolt;

// TODO: port: the managed API has no D3DCompile or D3D11/D3D12 compiler binding.
// bool CompileHLSLToBinary(ByteVector& bytecode, ea::string& compilerOutput,
//     ea::string_view sourceCode, ShaderType type);
// {
//     bytecode.clear();
//     compilerOutput.clear();
//
// #if D3D11_SUPPORTED || D3D12_SUPPORTED
//     const char* profile = GetShaderProfile(type);
//
//     unsigned flags = D3DCOMPILE_OPTIMIZATION_LEVEL3;
//     if (type == PS)
//         flags |= D3DCOMPILE_PREFER_FLOW_CONTROL;
//     #ifdef URHO3D_DEBUG
//     // Debug flag will help developer to inspect shader code.
//     flags |= D3DCOMPILE_DEBUG;
//     #endif
//
//     ID3DBlob* bytecodeBlob = nullptr;
//     ID3DBlob* errorMessagesBlob = nullptr;
//
//     const HRESULT result = D3DCompile(sourceCode.data(), sourceCode.length(), nullptr, nullptr, nullptr, "main",
//         profile, flags, 0, &bytecodeBlob, &errorMessagesBlob);
//
//     if (errorMessagesBlob)
//     {
//         const char* errorMessageBegin = reinterpret_cast<const char*>(errorMessagesBlob->GetBufferPointer());
//         compilerOutput.assign(errorMessageBegin, errorMessageBegin + errorMessagesBlob->GetBufferSize());
//         errorMessagesBlob->Release();
//     }
//
//     if (bytecodeBlob)
//     {
//         const auto bytecodeSize = static_cast<unsigned>(bytecodeBlob->GetBufferSize());
//         bytecode.resize(bytecodeSize);
//         memcpy(bytecode.data(), bytecodeBlob->GetBufferPointer(), bytecodeSize);
//         bytecodeBlob->Release();
//     }
//
//     return SUCCEEDED(result);
// #else
//     return false;
// #endif
// }
