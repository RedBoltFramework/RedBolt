#include <RedBolt/SpirVOptimizerSupport.hpp>

#include <spirv-tools/libspirv.hpp>
#include <spirv-tools/optimizer.hpp>

#include <stdexcept>
#include <string>
#include <vector>

namespace
{

spvtools::Optimizer& RegisterPerformancePasses(spvtools::Optimizer& optimizer, bool preserveInterface)
{
    return optimizer.RegisterPass(spvtools::CreateWrapOpKillPass())
        .RegisterPass(spvtools::CreateDeadBranchElimPass())
        .RegisterPass(spvtools::CreateMergeReturnPass())
        .RegisterPass(spvtools::CreateInlineExhaustivePass())
        .RegisterPass(spvtools::CreateEliminateDeadFunctionsPass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreatePrivateToLocalPass())
        .RegisterPass(spvtools::CreateLocalSingleBlockLoadStoreElimPass())
        .RegisterPass(spvtools::CreateLocalSingleStoreElimPass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateScalarReplacementPass())
        .RegisterPass(spvtools::CreateLocalAccessChainConvertPass())
        .RegisterPass(spvtools::CreateLocalSingleBlockLoadStoreElimPass())
        .RegisterPass(spvtools::CreateLocalSingleStoreElimPass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateLocalMultiStoreElimPass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateCCPPass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateLoopUnrollPass(true))
        .RegisterPass(spvtools::CreateDeadBranchElimPass())
        .RegisterPass(spvtools::CreateRedundancyEliminationPass())
        .RegisterPass(spvtools::CreateCombineAccessChainsPass())
        .RegisterPass(spvtools::CreateSimplificationPass())
        .RegisterPass(spvtools::CreateScalarReplacementPass())
        .RegisterPass(spvtools::CreateLocalAccessChainConvertPass())
        .RegisterPass(spvtools::CreateLocalSingleBlockLoadStoreElimPass())
        .RegisterPass(spvtools::CreateLocalSingleStoreElimPass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateSSARewritePass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateVectorDCEPass())
        .RegisterPass(spvtools::CreateDeadInsertElimPass())
        .RegisterPass(spvtools::CreateDeadBranchElimPass())
        .RegisterPass(spvtools::CreateSimplificationPass())
        .RegisterPass(spvtools::CreateIfConversionPass())
        .RegisterPass(spvtools::CreateCopyPropagateArraysPass())
        .RegisterPass(spvtools::CreateReduceLoadSizePass())
        .RegisterPass(spvtools::CreateAggressiveDCEPass(preserveInterface))
        .RegisterPass(spvtools::CreateBlockMergePass())
        .RegisterPass(spvtools::CreateRedundancyEliminationPass())
        .RegisterPass(spvtools::CreateDeadBranchElimPass())
        .RegisterPass(spvtools::CreateBlockMergePass())
        .RegisterPass(spvtools::CreateSimplificationPass());
}

} // namespace

namespace RedBoltNative
{

class SpirVOptimizerSupport::Impl
{
public:
    std::vector<std::uint32_t> bytecode_;
    std::string output_;
};

SpirVOptimizerSupport::SpirVOptimizerSupport()
    : impl_(std::make_unique<Impl>())
{
}

SpirVOptimizerSupport::~SpirVOptimizerSupport() = default;

bool SpirVOptimizerSupport::Optimize(
    const std::uint32_t* bytecode, std::size_t size, std::uint32_t targetValue, bool isVulkan)
{
    const auto target = static_cast<spv_target_env>(targetValue);
    impl_->bytecode_.clear();
    impl_->output_.clear();

    const auto appendToString = [&](spv_message_level_t level, const char*, const spv_position_t&, const char* message)
    {
        if (level <= SPV_MSG_ERROR)
        {
            impl_->output_ += "Failed to optimize SPIR-V shader: ";
            impl_->output_ += message;
            impl_->output_ += '\n';
        }
    };

    spvtools::Optimizer spirvOptimizer(target);
    spirvOptimizer.SetMessageConsumer(appendToString);

    if (isVulkan)
        spirvOptimizer.RegisterLegalizationPasses();

    RegisterPerformancePasses(spirvOptimizer, true);

    spvtools::OptimizerOptions optimizerOptions;
    optimizerOptions.set_run_validator(!isVulkan);

    std::vector<std::uint32_t> optimizedSpirV;
    if (!spirvOptimizer.Run(bytecode, size, &optimizedSpirV, optimizerOptions))
        return false;

    if (isVulkan)
    {
        spvtools::ValidatorOptions validatorOptions;
        spvtools::SpirvTools tools(target);
        tools.SetMessageConsumer(appendToString);
        if (!tools.Validate(optimizedSpirV.data(), optimizedSpirV.size(), validatorOptions))
            return false;
    }

    impl_->bytecode_ = std::move(optimizedSpirV);
    return true;
}

std::size_t SpirVOptimizerSupport::GetBytecodeSize() const
{
    return impl_->bytecode_.size();
}

std::uint32_t SpirVOptimizerSupport::GetBytecodeWord(std::size_t index) const
{
    if (index >= impl_->bytecode_.size())
        throw std::out_of_range("SPIR-V optimizer bytecode index is out of range");
    return impl_->bytecode_[index];
}

const char* SpirVOptimizerSupport::GetOutput() const
{
    return impl_->output_.c_str();
}

} // namespace RedBoltNative
