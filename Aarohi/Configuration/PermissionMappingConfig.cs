using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Configuration
{
    public class PermissionMappingConfig
    {
        public string Schema { get; set; }
        public string Table { get; set; }
        public PermissionColumns Columns { get; set; }

        public class PermissionColumns
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
