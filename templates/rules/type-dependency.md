# template-type-dependency

此文件是 JSON 的只读阅读投影；编辑它不会改变执行规则。

来源：type-dependency.json；版本：1.0.0；SHA-256：8fa7049d82f35c1a4f446978347bf2fd01f1e3f05f9cbecd8ad5dcc380e14ca9

type-dependency 模板；启用前调整选择器和理由。

## type-dependency-01

类型：type-dependency；启用：true；severity：warning

范围：namespace / glob / \*；allowEmpty=false

理由：请按实际架构政策填写约束理由。

- source：{           "kind": "namespace",           "match": "glob",           "value": "Sample.Domain\*"         }
- forbiddenTarget：{           "kind": "namespace",           "match": "glob",           "value": "Sample.Infrastructure\*"         }

## 例外

无。
