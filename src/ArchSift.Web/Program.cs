using ArchSift.Core;

namespace ArchSift.Web;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args is ["--version"])
        {
            Console.WriteLine($"archsift-web {RuntimeInfo.ProductVersion}");
            return 0;
        }

        Console.Error.WriteLine("本机 Web UI 尚未实现；按正式计划在 W07 提供 loopback 服务。");
        return 2;
    }
}
