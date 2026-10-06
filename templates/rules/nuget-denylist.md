# template-nuget-denylist

此文件是 JSON 的只读阅读投影；编辑它不会改变执行规则。

来源：nuget-denylist.json；版本：1.0.0；SHA-256：13f1805c7b024e6a6f478535104918e7ea591d2ec686e3a952cff427a5215f9e

nuget-denylist 模板；启用前调整选择器和理由。

## nuget-denylist-01

类型：nuget-denylist；启用：true；severity：warning

范围：project / glob / \*\*；allowEmpty=false

理由：请按实际架构政策填写约束理由。

- forbiddenPackageIds：\[           "Forbidden.Package"         \]

## 例外

无。
