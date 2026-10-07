# template-nuget-allowlist

此文件是 JSON 的只读阅读投影；编辑它不会改变执行规则。

来源：nuget-allowlist.json；版本：1.0.0；SHA-256：841e1e56d9c5e411dc2d13932f7d581cbc5f1df714a787e1c7b0666de56f592d

nuget-allowlist 模板；只有明确列出的直接 PackageReference ID 才被允许。

## nuget-allowlist-01

类型：nuget-allowlist；启用：true；severity：warning

范围：project / glob / \*\*；allowEmpty=false

理由：请按实际架构政策填写允许的直接包及理由。

- allowedPackageIds：\[           "Microsoft.Extensions.Logging.Abstractions"         \]

## 例外

无。
