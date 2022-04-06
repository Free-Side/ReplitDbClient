using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReplitDbClient {
    public interface IReplitDbClient {
        Task Set(String key, String value);

        Task<String> Get(String key);

        Task Delete(String key);

        Task<IEnumerable<String>> ListKeys(String prefix = "");
    }
}
