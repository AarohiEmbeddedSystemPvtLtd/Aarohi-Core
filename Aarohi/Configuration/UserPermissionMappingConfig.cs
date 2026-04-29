using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Configuration
{
    public class UserPermissionMappingConfig
    {
        public string Schema { get; set; }
        public string Table { get; set; }
        public UserPermissionColumns Columns { get; set; }

        public class UserPermissionColumns
        {
            public string Id { get; set; }
            public string UserId { get; set; }
            public string PermissionId { get; set; }
        }
    }
}
