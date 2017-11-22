using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RotativaHQ.AspNetCore
{
    public interface IMapPathResolver
    {
        string MapPath(string startPath, string virtualPath);
    }
}
