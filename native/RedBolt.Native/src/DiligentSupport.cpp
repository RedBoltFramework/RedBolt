#include <RedBolt/DiligentSupport.hpp>

#include <stdexcept>
#include <string>
#include <vector>

namespace Diligent
{

const ShaderCodeVariableDesc* GetShaderCodeBufferVariableAt(const ShaderCodeBufferDesc& self, Uint32 index)
{
    if (self.pVariables == nullptr || index >= self.NumVariables)
        throw std::out_of_range("Diligent shader reflection variable access out of range.");

    return &self.pVariables[index];
}

class TemporaryGLProgram
{
public:
    Uint32 programObject_{};
    std::vector<std::string> vertexAttributeNames_;
    std::vector<Uint32> vertexAttributeLocations_;
    TemporaryGLProgram(IShader* vertexShader, IShader* pixelShader, IShader* domainShader, IShader* hullShader,
        IShader* geometryShader, bool separablePrograms)
    {
    programObject_ = glCreateProgram();
    if (!programObject_)
        return;

    if (separablePrograms)
        glProgramParameteri(programObject_, GL_PROGRAM_SEPARABLE, GL_TRUE);

    IShader* shaders[] = {vertexShader, pixelShader, domainShader, hullShader, geometryShader};
    for (IShader* shader : shaders)
    {
        if (shader && (!separablePrograms || shader->GetDesc().ShaderType == SHADER_TYPE_VERTEX))
        {
            glAttachShader(programObject_, static_cast<IShaderGL*>(shader)->GetGLShaderHandle());
            if (glGetError() != GL_NO_ERROR)
                return;
        }
    }

    glLinkProgram(programObject_);
    if (glGetError() != GL_NO_ERROR)
        return;

    int isLinked = GL_FALSE;
    glGetProgramiv(programObject_, GL_LINK_STATUS, &isLinked);
    if (glGetError() != GL_NO_ERROR || !isLinked)
        return;

    int numActiveAttribs = 0;
    int maxNameLength = 0;
    glGetProgramiv(programObject_, GL_ACTIVE_ATTRIBUTES, &numActiveAttribs);
    glGetProgramiv(programObject_, GL_ACTIVE_ATTRIBUTE_MAX_LENGTH, &maxNameLength);

    std::string attributeName(maxNameLength, '\0');
    for (int attribIndex = 0; attribIndex < numActiveAttribs; ++attribIndex)
    {
        int attributeSize = 0;
        unsigned attributeType = 0;
        glGetActiveAttrib(programObject_, attribIndex, maxNameLength, nullptr, &attributeSize, &attributeType,
            attributeName.data());
        const int location = glGetAttribLocation(programObject_, attributeName.c_str());
        if (location == -1)
            continue;
        vertexAttributeNames_.emplace_back(attributeName.c_str());
        vertexAttributeLocations_.push_back(static_cast<Uint32>(location));
    }
    }

    ~TemporaryGLProgram()
    {
        if (programObject_)
            glDeleteProgram(programObject_);
    }
};

void* CreateTemporaryGLProgram(IShader* vertexShader, IShader* pixelShader, IShader* domainShader, IShader* hullShader,
    IShader* geometryShader, bool separablePrograms)
{
    return new TemporaryGLProgram{vertexShader, pixelShader, domainShader, hullShader, geometryShader, separablePrograms};
}

void DeleteTemporaryGLProgram(void* program)
{
    delete static_cast<TemporaryGLProgram*>(program);
}

Uint32 GetTemporaryGLProgramVertexAttributeCount(const void* program)
{
    return static_cast<Uint32>(static_cast<const TemporaryGLProgram*>(program)->vertexAttributeNames_.size());
}

const char* GetTemporaryGLProgramVertexAttributeName(const void* program, Uint32 index)
{
    return static_cast<const TemporaryGLProgram*>(program)->vertexAttributeNames_.at(index).c_str();
}

Uint32 GetTemporaryGLProgramVertexAttributeLocation(const void* program, Uint32 index)
{
    return static_cast<const TemporaryGLProgram*>(program)->vertexAttributeLocations_.at(index);
}

bool BindTemporaryGLProgramVertexAttributes(const void* program, IPipelineState* pipelineState)
{
    const auto temporaryProgram = static_cast<const TemporaryGLProgram*>(program);
    const auto pipelineStateGl = static_cast<IPipelineStateGL*>(pipelineState);
    const Uint32 programObject = pipelineStateGl->GetGLProgramHandle(SHADER_TYPE_VERTEX);
    int isLinked = GL_FALSE;
    glGetProgramiv(programObject, GL_LINK_STATUS, &isLinked);
    if (!isLinked)
        return false;

    for (Uint32 i = 0; i < temporaryProgram->vertexAttributeNames_.size(); ++i)
        glBindAttribLocation(programObject, temporaryProgram->vertexAttributeLocations_[i],
            temporaryProgram->vertexAttributeNames_[i].c_str());
    return true;
}

} // namespace Diligent
