using System.Text.Json;

namespace EasyForNet.Helpers
{
    public static class JsonHelper
    {
        public static string ToJson<T>(T value, JsonSerializerOptions options = null)
        {
            if (options == null)
                options = GetOptions();

            var json = JsonSerializer.Serialize(value, value.GetType(), options);
            return json;
        }

        public static byte[] ToBytes<T>(T value, JsonSerializerOptions options = null)
        {
            if (options == null)
                options = GetOptions();

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), options);
            return bytes;
        }

        public static T Deserialize<T>(string json, JsonSerializerOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default(T);

            if (options == null)
                options = GetOptions();

            return JsonSerializer.Deserialize<T>(json, options);
        }

        public static T Deserialize<T>(byte[] bytes, JsonSerializerOptions options = null)
        {
            if (bytes == null)
                return default(T);

            if (options == null)
                options = GetOptions();

            //if (typeof(T) != typeof(string))
            //{
            //    // check bytes of type string
            //    if (bytes[0] == 34 && bytes[bytes.Length - 1] == 34)
            //    {
            //        var bytesWithoutString = new List<byte>();
            //        for (var i = 0; i < bytes.Length; i++)
            //        {
            //            // remove byte code of string
            //            if (i != 0 && i != bytes.Length - 1)
            //            {
            //                bytesWithoutString.Add(bytes[i]);
            //            }
            //        }

            //        return JsonSerializer.Deserialize<T>(bytesWithoutString.ToArray(), options);
            //    }
            //}

            return JsonSerializer.Deserialize<T>(bytes, options);
        }

        #region Helpers

        private static JsonSerializerOptions GetOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        #endregion
    }
}
