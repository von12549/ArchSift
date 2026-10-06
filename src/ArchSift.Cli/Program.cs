using ArchSift.Core;

namespace ArchSift.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args is ["--version"])
        {
            Console.WriteLine($"archsift {RuntimeInfo.ProductVersion}");
            return 0;
        }

        if (args is [] or ["--help"] or ["-h"])
        {
            Console.WriteLine("ArchSift — .NET 架构与依赖分析工具");
            Console.WriteLine("当前开发阶段：W01。可用参数：--version、--help。");
            Console.WriteLine("analyze/verify/rules/ui/changes 将按正式计划逐阶段实现。");
            return 0;
        }

        Console.Error.WriteLine("配置错误：当前阶段不支持此命令。使用 --help 查看已实现入口。");
        return 2;
    }
}
