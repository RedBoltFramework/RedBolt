namespace RedBolt;

// TODO: port: the managed API has no SpirVShader, TargetShaderLanguage, or SPIR-V optimizer/validator binding.
// URHO3D_API bool OptimizeSpirVShader(
//     SpirVShader& shader, ea::string& optimizerOutput, TargetShaderLanguage targetLanguage);
// bool OptimizeSpirVShader(SpirVShader& shader, ea::string& optimizerOutput, TargetShaderLanguage targetLanguage)
// {
// #ifdef URHO3D_SHADER_OPTIMIZER
//     const bool isVulkan = targetLanguage == TargetShaderLanguage::VULKAN_1_0;
//     const auto targetEnv = GetTarget(targetLanguage);
//
//     spvtools::Optimizer spirvOptimizer(targetEnv);
//     spirvOptimizer.SetMessageConsumer(AppendToString(optimizerOutput));
//
//     if (isVulkan)
//         spirvOptimizer.RegisterLegalizationPasses();
//
//     spirvOptimizer.RegisterPerformancePasses(true);
//
//     spvtools::OptimizerOptions optimizerOptions;
//     optimizerOptions.set_run_validator(!isVulkan);
//
//     std::vector<uint32_t> optimizedSPIRV;
//     if (!spirvOptimizer.Run(shader.bytecode_.data(), shader.bytecode_.size(), &optimizedSPIRV, optimizerOptions))
//     {
//         // Vulkan requires optimization, other backends don't.
//         return !isVulkan;
//     }
//
//     if (isVulkan)
//     {
//         spvtools::ValidatorOptions validatorOptions;
//         spvtools::SpirvTools tools(targetEnv);
//         tools.SetMessageConsumer(AppendToString(optimizerOutput));
//         if (!tools.Validate(optimizedSPIRV.data(), optimizedSPIRV.size(), validatorOptions))
//         {
//             return false;
//         }
//     }
//
//     shader.bytecode_ = ea::move(optimizedSPIRV);
//     return true;
// #else
//     URHO3D_ASSERTLOG(0, "URHO3D_SHADER_OPTIMIZER should be enabled to use OptimizeSpirVShader");
//     return false;
// #endif
// }
