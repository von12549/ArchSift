# 规则契约与阅读投影

JSON 是规则的可执行来源；Markdown 仅用于阅读。schemaVersion=1，规则集包含 id、version、description、rules、exceptions；规则 ID 在所有配置文件中全局唯一，加载顺序与每份原文件 SHA-256 均保留。例外必须有稳定 id、ruleId、scope 和非空 reason，未知 ruleId 或重复例外 id 报配置错误。

八类模板位于 `templates/rules`：project-reference、project-reference-allowlist、graph-integrity、target-framework、nuget-denylist、nuget-allowlist、type-dependency、naming。模板不会从现有依赖自动生成允许策略，启用前修改范围、参数与约束理由。路径以根内相对路径和 `/` 表示，项目身份为相对 csproj 路径，精确匹配 Ordinal，包 ID 检查 OrdinalIgnoreCase。

选择器明确指定 kind、match、value，可显式提供 allowEmpty。exact 按字面值；glob 支持 * 和 ?，路径额外支持 **，其中 * 不跨 `/`，** 可以跨目录，**/ 也可表示零层目录。正则、方括号字符组、任意代码和隐式匹配方式不支持。路径选择器不接受绝对路径、反斜杠或 `.`/`..` 遍历。选择器最多 256 字符；匹配采用迭代表，长候选不会消耗递归栈。

scope 的 kind 必须与规则类别相符；naming 与 subjectKind 一致，type-dependency 的 source/forbiddenTarget 与 scope 为 namespace/type。项目范围的通配全部路径使用 **；命名检查中的 project 名称为 csproj 文件名去掉扩展名，source-file 检查相对路径（具体求值由 W04 实现）。

`project-reference-allowlist` 对每条已解析的直接项目引用检查 `allowedTargets`；`nuget-allowlist` 对每个已声明的直接 `PackageReference` ID 检查 `allowedPackageIds`。两种允许数组都可显式为空，表示禁止相应范围内的全部直接依赖。多条 allowlist 独立求值，效果为交集；allow 不覆盖 deny，真正放行只能通过指向单一 rule ID 的显式例外。项目引用或包声明覆盖不完整时不能返回 pass。

未知字段/类别、缺参、非法 enum、空白理由、重复 JSON key、重复规则 ID 及 allowlist 内重复项均拒绝。相同范围的 TFM 策略没有允许框架交集，或命名 exact/glob 语言没有交集时，组合报配置错误；不做最后定义覆盖。来源选择器零匹配默认 inconclusive，显式 allowEmpty 才为 not-applicable。多个例外重叠时按 exact、更多字面字符、更少通配符、selector value、例外 ID 的稳定顺序选择唯一审计归因，不依赖文件顺序。

`rules validate --file` 校验单文件的 schema 和规则/组合语义；跨文件例外在加载整个配置集合时验证，单文件 validate 的例外必须能在该文件中找到对应规则。`rules render --file --output` 生成新 Markdown，包含原 JSON 的哈希、版本、范围、参数、severity 与例外理由；输出不覆盖已有文件，不覆盖 JSON。文件 I/O 错误返回 3，配置错误返回 2，成功返回 0。

## Schema 实现边界

四份 schema 声明 JSON Schema draft-07。内置校验器只用于随产品嵌入的固定 schemas，支持这些 schema 实际使用的 type、const、enum、required、properties、additionalProperties、items、minItems、minLength、minimum、pattern、allOf、oneOf、anyOf 与文档内 $ref。不加载用户 schema，不联网取 $ref，不宣称支持 draft-07 的所有可选关键字。数据最大 4 MiB、JSON 深度 64；接受 UTF-8 BOM，但哈希始终使用原始文件字节。语义校验独立运行。

契约验证使用 `scripts/Verify-Contracts.ps1`：首先执行受控离线恢复/构建/测试，然后以 PowerShell Test-Json 独立校验模板，再运行真实 CLI validate/render。所有 dotnet 子进程沿用独立 CLI home 与 host-state 哈希检查。该脚本只生成忽略的开发输出；源码中的 Markdown 模板是经审查的生成投影。
