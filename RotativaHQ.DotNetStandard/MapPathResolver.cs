#if NETFULL
using System;
using System.Web;

namespace RotativaHQ.DotNetStandard
{
    public class MapPathResolver : IMapPathResolver
    {
        public string MapPath(string startPath, string virtualPath)
        {
            string localPath = "";
            if (virtualPath.StartsWith("/"))
            {
                localPath = HttpContext.Current.Server.MapPath(virtualPath);
            }
            else
            {
                startPath = startPath.Remove(startPath.LastIndexOf('/') + 1);
                try
                {
                    
                    localPath = HttpContext.Current.Server.MapPath(startPath + virtualPath);
                }
                catch (HttpException hex)
                {
                    var rootLocalPath = "/" + virtualPath.Replace("../", "");
                    localPath = HttpContext.Current.Server.MapPath(rootLocalPath);
                }
                catch (Exception ex)
                {
                    localPath = virtualPath;
                }
            }
            return localPath;
        }
    }
}


#else

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;

namespace RotativaHQ.DotNetStandard
{

    public class MapPathResolver : IMapPathResolver
    {
        public string MapPath(string startPath, string virtualPath)
        {
            string localPath = "";
            //var t = hostingEnv.WebRootPath;
            //if (virtualPath.StartsWith("/"))
            //{
            //    localPath = context.Server.MapPath(virtualPath);
            //}
            //else
            //{
            //    startPath = startPath.Remove(startPath.LastIndexOf('/') + 1);
            //    try
            //    {

            //        localPath = context.Server.MapPath(startPath + virtualPath);
            //    }
            //    catch (HttpException hex)
            //    {
            //        var rootLocalPath = "/" + virtualPath.Replace("../", "");
            //        localPath = context.Server.MapPath(rootLocalPath);
            //    }
            //    catch (Exception ex)
            //    {
            //        localPath = virtualPath;
            //    }
            //}
            return localPath;
        }
    }
}

#endif