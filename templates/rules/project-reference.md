# template-project-reference

此文件是 JSON 的只读阅读投影；编辑它不会改变执行规则。

来源：project-reference.json；版本：1.0.0；SHA-256：9a864c3268e6040aba2a7e74f37b687321a9245a1905f0f964823564ec7d3b79

project-reference 模板；启用前调整选择器和理由。

## project-reference-01

类型：project-reference；启用：true；severity：warning

范围：project / glob / \*\*；allowEmpty=false

理由：请按实际架构政策填写约束理由。

- source：{           "kind": "project",           "match": "glob",           "value": "src/Domain/\*\*"         }
- target：{           "kind": "project",           "match": "glob",           "value": "src/Infrastructure/\*\*"         }

## 例外

无。
