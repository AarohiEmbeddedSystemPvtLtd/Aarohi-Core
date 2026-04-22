using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Configuration
{
    public class RootConfig
    {
        public UserMappingConfig UserMapping { get; set; }
        public PermissionMappingConfig PermissionMapping { get; set; }
        public UserPermissionMappingConfig UserPermissionMapping { get; set; }
    }
}
