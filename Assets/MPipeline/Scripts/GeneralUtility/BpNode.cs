using System;
using Unity.Collections.LowLevel.Unsafe;
public unsafe class BpDeep
{
    public double[][] layer;//神经网络各层节点
    public double[][] layerErr;//神经网络各节点误差
    public double[][][] layer_weight;//各层节点权重
    public double[][][] layer_weight_delta;//各层节点权重动量
    public double mobp;//动量系数
    public double rate;//学习系数

    public BpDeep(int[] layernum, double rate, double mobp)
    {
        this.mobp = mobp;
        this.rate = rate;
        layer = new double[layernum.Length][];
        layerErr = new double[layernum.Length][];
        layer_weight = new double[layernum.Length][][];
        layer_weight_delta = new double[layernum.Length][][];
        Random random = new Random();
        for (int l = 0; l < layernum.Length; l++)
        {
            layer[l] = new double[layernum[l]];
            layerErr[l] = new double[layernum[l]];
            if (l + 1 < layernum.Length)
            {
                //layernum[l + 1]
                layer_weight[l] = new double[layernum[l] + 1][];
                for(int i = 0; i < layer_weight[l].Length; ++i)
                {
                    layer_weight[l][i] = new double[layernum[l + 1]];
                }
                layer_weight_delta[l] = new double[layernum[l] + 1][];
                for (int i = 0; i < layer_weight_delta[l].Length; ++i)
                {
                    layer_weight[l][i] = new double[layernum[l + 1]];
                }
                for (int j = 0; j < layernum[l] + 1; j++)
                    for (int i = 0; i < layernum[l + 1]; i++)
                        layer_weight[l][j][i] = random.NextDouble();//随机初始化权重
            }
        }
    }
    //逐层向前计算输出
    public double[] computeOut(double* inTarget)
    {
        for (int l = 1; l < layer.Length; l++)
        {
            for (int j = 0; j < layer[l].Length; j++)
            {
                double z = layer_weight[l - 1][layer[l - 1].Length][j];
                for (int i = 0; i < layer[l - 1].Length; i++)
                {
                    layer[l - 1][i] = l == 1 ? inTarget[i] : layer[l - 1][i];
                    z += layer_weight[l - 1][i][j] * layer[l - 1][i];
                }
                layer[l][j] = 1 / (1 + Math.Exp(-z));
            }
        }
        return layer[layer.Length - 1];
    }

    private void Train(double* inTarget)
    {
        for (int l = 1; l < layer.Length; l++)
        {
            for (int j = 0; j < layer[l].Length; j++)
            {
                double z = layer_weight[l - 1][layer[l - 1].Length][j];
                for (int i = 0; i < layer[l - 1].Length; i++)
                {
                    layer[l - 1][i] = l == 1 ? inTarget[i] : layer[l - 1][i];
                    z += layer_weight[l - 1][i][j] * layer[l - 1][i];
                }
                layer[l][j] = 1 / (1 + Math.Exp(-z));
            }
        }
    }
    //逐层反向计算误差并修改权重
    private void updateWeight(double* tar)
    {
        int l = layer.Length - 1;
        for (int j = 0; j < layerErr[l].Length; j++)
            layerErr[l][j] = layer[l][j] * (1 - layer[l][j]) * (tar[j] - layer[l][j]);

        while (l-- > 0)
        {
            for (int j = 0; j < layerErr[l].Length; j++)
            {
                double z = 0.0;
                for (int i = 0; i < layerErr[l + 1].Length; i++)
                {
                    z = z + l > 0 ? layerErr[l + 1][i] * layer_weight[l][j][i] : 0;
                    layer_weight_delta[l][j][i] = mobp * layer_weight_delta[l][j][i] + rate * layerErr[l + 1][i] * layer[l][j];//隐含层动量调整
                    layer_weight[l][j][i] += layer_weight_delta[l][j][i];//隐含层权重调整
                    if (j == layerErr[l].Length - 1)
                    {
                        layer_weight_delta[l][j + 1][i] = mobp * layer_weight_delta[l][j + 1][i] + rate * layerErr[l + 1][i];//截距动量调整
                        layer_weight[l][j + 1][i] += layer_weight_delta[l][j + 1][i];//截距权重调整
                    }
                }
                layerErr[l][j] = z * layer[l][j] * (1 - layer[l][j]);//记录误差
            }
        }
    }

    public void Train(double* inTarget, double* tar)
    {
        Train(inTarget);
        updateWeight(tar);
    }
}
