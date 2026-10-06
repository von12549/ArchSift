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
            Console.WriteLine("开发基于 W01；已实现 W02 规则契约。可用参数：--version、--help。");
            Console.WriteLine("rules validate --file <JSON>；rules render --file <JSON> --output <新 Markdown 文件>。");
            Console.WriteLine("analyze/verify/ui/changes 将按正式计划逐阶段实现。");
            return 0;
        }

        try
        {
            if (args is ["rules", "validate", "--file", var file])
            {
                var loaded = RuleLoader.Load(file);
                RuleLoader.Compose([loaded]);
                Console.WriteLine($"有效规则集：{loaded.Ruleset.Id}；SHA-256：{loaded.Identity.Sha256}");
                return 0;
            }
            if (args is ["rules", "render", "--file", var rulesFile, "--output", var output])
            {
                var loaded = RuleLoader.Load(rulesFile);
                RuleLoader.Compose([loaded]);
                if (Path.GetFullPath(rulesFile).Equals(Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                    throw new ConfigurationException("Markdown 输出不得覆盖 JSON 规则来源。");
                using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                writer.Write(RuleMarkdown.Render(loaded));
                Console.WriteLine(Path.GetFullPath(output));
                return 0;
            }
        }
        catch (ConfigurationException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (System.Text.Json.JsonException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine(error.Message); return 3; }
        Console.Error.WriteLine("配置错误：当前阶段不支持此命令。使用 --help 查看已实现入口。");
        return 2;
    }
}
