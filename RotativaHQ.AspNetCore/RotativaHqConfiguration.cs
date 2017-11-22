using System;
using System.Collections.Generic;
using System.Text;

namespace RotativaHQ.AspNetCore
{
    public static class RotativaHqConfiguration
    {
        private static string _rotativaHqUrl;
        private static string _rotativaApiKey;
        internal static string RotativaHqUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_rotativaHqUrl))
                {
#if NET45
                    _rotativaHqUrl = System.Configuration.ConfigurationManager.AppSettings["RotativaHqUrl"];
#endif
                }
                return _rotativaHqUrl;
            }
        }

        internal static string RotativaHqApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(_rotativaApiKey))
                {
#if NET45
                    _rotativaHqUrl = System.Configuration.ConfigurationManager.AppSettings["RotativaHqApiKey"];
#endif
                }
                return _rotativaApiKey;
            }
        }

        public static void SetRotativaHqUrl(string newRotativaHqUrl)
        {
            _rotativaHqUrl = newRotativaHqUrl;
        }

        public static void SetRotativaHqApiKey(string newRotativaHqApiKey)
        {
            _rotativaApiKey = newRotativaHqApiKey;
        }
    }
}
