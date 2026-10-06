# W09 变更比较验收

日期：2026-10-07（Australia/Sydney）。状态：COMPLETE / PASS。

- SDK 10.0.303 / Windows x64 / net10.0 / Debug；locked offline restore、编译零警告/错误、106/106 回归通过（75 unit、29 integration、2 architecture）。最终运行 `e03f75bcbbd14b9b82b8d0a8d52d0da3`；六模板另经 PowerShell Test-Json 和真实 CLI validate/render 验证。
- 默认 HEAD/工作区最终状态、同时 staged+unstaged、未跟踪源码、跨项目影响、显式两端、子目录根、export-ignore 不影响 blob 身份、删除/唯一内容重命名、规则删除/范围/例外变化、基线失败、上下文差异、覆盖不足、取消、HTML 转义、CLI/Core parity 和真实认证 HTTP 比较均通过。
- 合成浏览器运行 `D:\ArchSift-lab\runs\w09-ui-bd04886f37704ab092e7b92c9d0aadfd`：已知新增引用命中，exit 0；无效本地 ref 后当前结果和下载全部清空，旧成功只留历史。[比较截图](w09/comparison.png)、[失败截图](w09/failed-current-comparison.png)。未填写新的人工可用性评分。
- 每个 .NET 检查及最终 User/Machine environment、Process PATH、四个 Profile 哈希相等；预览服务精确停止，端口 9814 无 listener。源码/索引未被比较修改；未运行 Guard/IFX 现场集成。

前两次回归失败记录保留：`6d5a698d14d74627a9df16fde5a3cd13`（旧 help 断言、Windows fixture Git 对象只读清理）；`1cbefdae489e487ab20bf3347a023dda`（fixture finding hash 与空规则集/schema 及断言校准）。没有将失败改写为通过，宿主哈希始终一致。失败轮的残留合成 fixture 不执行宽泛清理；原始日志/TRX 保留在开发 artifacts。

边界：Git 链接/submodule/碰撞保守拒绝，快照有文件/字节上限；不存在增量优化、逐 commit 验证、自动 fetch、自动豁免或 fail-on。构建和已有 DLL 绑定限制不变，详见 [变更比较](../changes.md)。W10 尚未完成。
