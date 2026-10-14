# ArchSift.InProject IP01 安装与 worktree 宿主验收

日期：2026-10-14（Australia/Sydney）。状态：**IP01 COMPLETE FOR SYNTHETIC DEVELOPMENT INSTALL / PRODUCT NOT ACCEPTED / NOT RELEASED**。依据：[实施计划](../plans/20261014-archsift-inproject-0.1.0-implementation-plan.md)及 IP00-A–E 契约。本阶段没有操作 Guard／IFX 或其他真实目标。

## 交付与写入范围

新增 net10.0 `ArchSift.InProject` 入口、`ArchSift.InProject.Runtime` 安装服务及只引用 Contracts 的 `ArchSift.InProject.TestBridge` 骨架。入口当前提供 `init`、`verify` 和版本查询；`gate`、`status`、`ui` 由后续 IP02–IP06 落实。`init` 使用 Git 查询确认普通 checkout／linked worktree 根；一个候选 solution 可选中，多个候选必须明确指定。只接受本地 ZIP 与调用者给出的 SHA-256，不自动联网。包 manifest 与每个条目的大小／摘要、七组件身份、路径、链接和大小上限在写入前核对；提取后再次核对。已安装的 `verify` 对版本锁、manifest、运行时文件集合及七组件摘要独立核验。

每个合成 worktree 初始化自己的 `.archsift/project.json`、`rules/`、`version.lock.json`、`bootstrap/`、薄 xUnit 测试项目及 `runtime/`、`state/`、`reports/`、`evidence/`。仅后四个目录追加精确 `.gitignore` 规则；已有内容原字节前缀保留。初始化先在同一 worktree 建临时 owned 目录，规则语义验证失败则清理该精确目录且不改 `.gitignore`；完成写入在独占 `.gitignore` 句柄下提交。业务 solution／源码不被改写。运行态目录、规则与载荷均绑定当前 worktree，linked worktree 的 `.git` 文件经 Git 查询处理。

`scripts/Build-InProjectDevPackage.ps1` 由本地 Release 编译产物和固定测试／bootstrap 资产制作外置 `0.1.0-dev` 包。测试工程锁定 xUnit 2.9.3、runner 3.1.4、Test SDK 17.14.1；本阶段只安装薄工程，尚无 B 类架构测试用例，零测试不被视为门禁通过。开发包源提交字段记录打包时的 `HEAD`，但此次工作树含未提交的 IP01 实现；包的真实字节身份是 ZIP SHA-256，不能当作 IP07 固定源码候选或正式资产。

## 实际检查

Windows x64、SDK 10.0.303、net10.0、Release；使用明确本地 NuGet 源、`--locked-mode` 和 `--no-restore`，没有网络恢复。最终运行记录位于 `D:\ArchSift-lab\runs\inproject-ip01-d091fd1f00ae48418aa88e9753a2d4dc`。主宿主、桥接和专用测试模板的离线锁定恢复及构建均通过；IP01 合成安装正反测试 **4/4**、现有依赖方向架构测试 **2/2**。最初一次测试辅助变量重名及架构测试固定“6 个项目”的假设导致失败，修正后已按同一门槛重跑，最终结果如上。

四项合成测试分别覆盖：普通 checkout 的 owned 写入、配置／锁、重复初始化与运行时 DLL 篡改拒绝；多 solution 歧义和包摘要错误在写入前拒绝、显式选择及已有 `.gitignore` 前缀保留；规则无效时暂存清理且不改忽略文件；linked worktree 的 `.git` 文件、同提交不同未提交输入和独立状态。最终实际开发包为 `D:\ArchSift-lab\packages\inproject-dev\archsift-inproject-0.1.0-dev-win-x64-7cc50a351f6c.zip`，ZIP SHA-256 `7cc50a351f6c4a4865b19e0026842307b64606e0225861fa8ad3c423357c72f2`，manifest SHA-256 `a172a9bc90bde5e10c31eb8c63cfd2ee9f0f269d229e013fbdc8015049fe2367`，30 个清单文件。外置合成 checkout `D:\ArchSift-lab\fixtures\inproject-ip01\actual-dbdad242f6c4432ea761560752534577` 上用编译后的入口 `init` 返回 0，安装后 `verify` 返回 0；业务 solution 原字节相等。生成的 `project.json`、版本锁、载荷 manifest 三份 schema 均有效，测试项目 XML 可解析。

安装／恢复／构建／测试各检查点的 User／Machine 环境、Process PATH 和四个 Profile 的哈希与同一安全基线相等；只记录摘要及 equality，不采集或打印原始值。本阶段未观察到持久环境漂移。外置材料保留作复核，源仓库没有合成目标输出。

## 后续边界

IP01 验证的是开发包的项目内初始化与 worktree 隔离。实际业务编译输入关联、net8/net9/net10 目标矩阵在 IP02；真实 B 类 xUnit 测试、桥接协议和严格门禁在 IP03；共享任务、CLI 运行状态及当前结果在 IP04；完整证据、Web、候选生命周期与正式包在 IP05–IP09。联网获取、升级／回退／移除不由此次本地开发包证明；没有真实目标安装、目标业务构建或应用入口执行。不存在可以继承为当前门禁通过的 IP01 结果。
