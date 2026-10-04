# StatusTextEditor

**StatusTextEditor** 是一个《最终幻想 14》Dalamud 插件：**热修改游戏原生状态（buff / debuff）的名称与描述文本**，修改即时生效，仅在本机显示，不向服务器发送任何数据。

## 功能

- **全表选择**：内置完整 Status 表选择器，支持按名称 / ID 搜索、图标预览。
- **名称与描述独立替换**：留空即保持原版文本。
- **格式化标签**：描述支持 `[color=Red]…[/color]`（颜色名或数值）、`[glow=LightBlue]…[/glow]`（发光轮廓）、`[i]…[/i]`（斜体），语法错误实时提示。
- **分类管理**：条目可自定义分类，分组显示、折叠记忆、拖拽排序、拖拽移动分类、右键批量重命名分类。
- **选择性导出 / 导入**：条目可按分类批量勾选后导出为 JSON 文件或复制到剪贴板，方便分发分享；导入时可选跳过或覆盖冲突条目。

## 安装

### 方式一：插件仓库（推荐，可自动更新）

1. 游戏内输入 `/xlsettings`，进入 **实验性（Experimental）** 选项卡；
2. 在 **自定义插件仓库（Custom Plugin Repositories）** 中添加：

   ```
   https://raw.githubusercontent.com/OWNER/StatusTextEditor/main/repo.json
   ```

3. 点击右下角 **保存**；
4. 打开 **Dalamud 插件列表**（系统菜单 → Dalamud Plugins），搜索 `StatusTextEditor` 并安装。

**国内网络镜像**：如果 `raw.githubusercontent.com` 无法访问，可改用 jsDelivr CDN 地址（内容相同，国内可直连）：

```
https://cdn.jsdelivr.net/gh/eayu-nsyf/StatusTextEditor@main/repo.json
```

### 方式二：手动安装

1. 从 [Releases](../../releases) 下载最新的 `latest.zip`（国内可直连镜像：`https://cdn.jsdelivr.net/gh/eayu-nsyf/StatusTextEditor@v1.0.0.0/dist/latest.zip`）；
2. 解压到 `%AppData%\XIVLauncherCN\devPlugins\`（或你的启动器对应目录）下的 `StatusTextEditor` 文件夹；
3. 重启游戏，在 `/xlplugins` 中启用。

### 方式二：手动安装

1. 从 [Releases](../../releases) 下载最新的 `latest.zip`；
2. 解压到 `%AppData%\XIVLauncherCN\devPlugins\`（或你的启动器对应目录）下的 `StatusTextEditor` 文件夹；
3. 重启游戏，在 `/xlplugins` 中启用。

## 使用

游戏内输入 `/statustext` 或 `/ste` 打开主窗口：

1. 点击 **「+ 添加状态」**，在状态列表中搜索并选择要修改的状态；
2. 填写 **名称** / **描述**（留空保持原版），修改即时生效；
3. 悬浮游戏内 buff 图标即可看到新文本（自身 buff 条、目标窗口、队伍列表均生效）。

### 注意事项

- 修改**仅为本机显示效果**，不会同步给其他玩家，也不影响服务器数据；
- 游戏版本更新后如插件失效，请等待更新；
- 名称替换总量存在约 259 字节的显示上限（超出部分自动截断），描述上限约 1024 字节。

## 工作原理

插件通过 hook 游戏内部"按 StatusId 读取状态行数据"的接口，在游戏读取文本时返回一份重建后的数据缓冲区（替换名称与描述字段），原始游戏数据与内存不会被改动。

## 致谢与许可

- 行数据 hook 方案参考了 [Namingway](https://github.com/anna-is-cute/Namingway) 的实现思路；
- 格式化标签解析器移植自 [Moodles](https://github.com/kawaii/Moodles)（BSD-3-Clause）；
- 本项目以 [BSD-3-Clause](./LICENSE) 许可证开源。

## 编译

需要 .NET 10 SDK 与 Dalamud（API 15）开发库，位于 `StatusTextEditor/` 目录下执行：

```
dotnet build StatusTextEditor/StatusTextEditor.csproj -c Release
```

产物为 `StatusTextEditor/bin/Release/StatusTextEditor/latest.zip`。
