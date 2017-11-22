using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;

namespace RotativaHQ.AspNetCore
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