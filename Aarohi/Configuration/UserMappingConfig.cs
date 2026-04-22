using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Configuration
{
    public class UserMappingConfig
    {
        public string Schema { get; set; }
        public string Table { get; set; }
        public UserColumns Columns { get; set; }

        public class UserColumns
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string ParentId { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string Role { get; set; }

        }
    }
}
