using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace Cjora.MQ.Services;

internal static class MqSerializer
{
    internal static byte[] ToBytes(object obj) 
    {
        if (obj == null) return Array.Empty<byte>();

        return obj switch
        {
            byte[] bytes => bytes,
            string str => Encoding.UTF8.GetBytes(str),
            _ => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver(), // 保持原始大小写
                    NullValueHandling = NullValueHandling.Ignore
                }))
        };
    }
}
