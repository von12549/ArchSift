# template-project-reference-allowlist

此文件是 JSON 的只读阅读投影；编辑它不会改变执行规则。

来源：project-reference-allowlist.json；版本：1.0.0；SHA-256：08027e31934c89aa32e4614f081b916942fffff6e6b782776d79862bc1773e6e

project-reference-allowlist 模板；只有明确列出的直接项目引用目标才被允许。

## project-reference-allowlist-01

类型：project-reference-allowlist；启用：true；severity：warning

范围：project / glob / \*\*；allowEmpty=false

理由：请按实际架构政策填写允许边界及理由。

- source：{           "kind": "project",           "match": "glob",           "value": "src/Application/\*\*"         }
- allowedTargets：\[           {             "kind": "project",             "match": "glob",             "value": "src/Domain/\*\*"           }         \]

## 例外

无。
