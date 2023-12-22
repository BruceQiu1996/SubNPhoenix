using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SubNPhoenix.Helpers
{
    public class IniHelper
    {
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, int nSize, string lpFileName);

        [DllImport("kernel32")]
        private static extern int WritePrivateProfileString(string lpApplicationName, string lpKeyName, string lpString, string lpFileName);

        public static Task<string> ReadAsync(string path, string section, string key)
        {
            StringBuilder sb = new StringBuilder(1024);
            GetPrivateProfileString(section, key, null, sb, 1024, path);
            return Task.FromResult(sb.ToString());
        }

        public static Task WriteAsync(string path, string section, string key, string value)
        {
            return Task.FromResult(WritePrivateProfileString(section, key, value, path));
        }
    }
}
