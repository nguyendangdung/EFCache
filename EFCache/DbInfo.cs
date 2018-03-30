using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFCache
{
    public class DbInfo
    {
        public string DbName { get; }

        public DbInfo(string dbName)
        {
            DbName = dbName;
        }
    }
}
