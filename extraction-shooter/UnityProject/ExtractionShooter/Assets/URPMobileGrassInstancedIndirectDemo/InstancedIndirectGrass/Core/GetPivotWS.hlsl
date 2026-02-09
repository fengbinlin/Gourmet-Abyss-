#ifndef INSTANCE_TRANSFORM_INCLUDED
#define INSTANCE_TRANSFORM_INCLUDED

// 声明StructuredBuffer
StructuredBuffer<float3> _AllInstancesTransformBuffer;
StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;

void vertInstancingSetup()
{
    #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

    #endif
}

// 修正函数定义 - 添加inline和out参数
void GetInstancePivotWS_float(float instanceID, out float3 pivotWS)
{
    // 从可见实例ID缓冲区获取真正的索引，再获取位置
    uint index = _VisibleInstanceOnlyTransformIDBuffer[(uint)instanceID];
    pivotWS = _AllInstancesTransformBuffer[index];
}

#endif