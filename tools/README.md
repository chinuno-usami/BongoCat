# BongoCat 局域网按键同步套件 (Mac -> Windows Steam)

本套件用于将 **Mac 机上的按键与鼠标点击** 实时通过局域网同步到 **Windows 机器上运行的 Steam 版 BongoCat**，让 Windows 上的 Steam 猫咪同步拍爪并增加 PawPass / 成就计数。

---

## 目录文件说明

- **`mac_bongo_sender.py`**：Mac 端开箱即用发送器（免编译 BongoCat 源码，利用 macOS CoreGraphics 全局捕获输入并通过 UDP 广播发送）。
- **`BongoSync.dll`**：Windows Steam 版 Unity 补丁组件（提供纯后台 UDP 接收功能，零按键干扰）。
- **`Assembly-CSharp.patched.dll`**：已注入 `BongoSync` 逻辑的 Steam 版核心游戏库。
- **`install_steam_patch.bat`**：Windows 端一键安装补丁脚本（自动备份原文件）。
- **`uninstall_steam_patch.bat`**：Windows 端一键还原脚本（随时恢复官方原始文件）。
- **`win_bongo_receiver.py`**：【方案 B 备选】Windows 端免改游戏文件的独立中继脚本。
- **`BongoSyncReceiver.cs` / `PatchAssembly.cs`**：补丁源码与自动化注入工具。

---

## 快速使用步骤

### 第一步：Windows 端（Steam 版）

推荐使用【方案 A：游戏内补丁】，拥有**绝无任何按键冲突**且开游戏即玩的最佳体验。

1. **部署补丁**：
   - 将本 `tools` 文件夹（或其中的 `BongoSync.dll` 与 `Assembly-CSharp.patched.dll`）复制到 Windows。
   - 把 `BongoSync.dll` 复制进游戏目录的 `BongoCat_Data\Managed\`。
   - 把 `Assembly-CSharp.patched.dll` 重命名为 `Assembly-CSharp.dll`，覆盖进 `BongoCat_Data\Managed\`（覆盖前可先备份原文件，或直接运行 `install_steam_patch.bat`）。
2. **在 Steam 中正常启动 BongoCat** 即可！
   - 游戏启动时会自动在后台开启 UDP 39824 端口监听，无需开启任何其他窗口。

*(若您不想修改任何游戏文件，可选择【方案 B】：直接在 Windows 上运行 `python win_bongo_receiver.py`。)*

---

### 第二步：Mac 端（发送端）

确保 Mac 与 Windows 处于同一个局域网（连接同一个 Wi-Fi 或路由器）。

#### 方式 1：使用极速独立发送脚本（推荐优先测试）
1. 在 Mac 终端运行：
   ```bash
   python3 tools/mac_bongo_sender.py
   ```
2. 首次运行若提示无权限，请前往 macOS `系统设置 -> 隐私与安全性 -> 辅助功能 (Accessibility)` 勾选终端（Terminal）。
3. 此时在 Mac 上任意敲击键盘或点击鼠标，终端会实时显示同步计数，Windows 上的 Steam 猫咪就会实时同步交替拍爪！

#### 方式 2：使用 Mac 开源版 BongoCat 内置同步（原生直连，0 额外脚本）
已编译好的应用位于：`BongoCat/build/BongoCat.app`。
- **默认模式**：双击直接运行，默认向局域网广播 (`255.255.255.255:39824`)。
- **指定 Windows IP 直连（关闭广播）**：支持以下任一方式：
  1. **配置文件（永久生效，推荐）**：
     创建 `~/Library/Application Support/BongoCat/config/sync.json`（或在启动目录下创建 `sync.json`）：
     ```json
     {
       "target_ip": "192.168.1.100",
       "target_port": 39824
     }
     ```
     BongoCat 启动时会自动检测该文件并进入 **Direct Unicast (单播直连)** 模式。
  2. **环境变量（快速启动）**：
     ```bash
     BONGO_SYNC_IP=192.168.1.100 ./BongoCat/build/BongoCat.app/Contents/MacOS/BongoCat
     ```
  3. **命令行参数**：
     ```bash
     ./BongoCat/build/BongoCat.app/Contents/MacOS/BongoCat --sync-ip 192.168.1.100
     ```

---

## 重编 BongoSync.dll（重要：不要用被裁剪的 API）

Steam 版的 `BongoCat_Data\Managed\` 里 `mscorlib.dll` / `System.dll` 是 **Unity managed stripping 裁剪过**的，很多 .NET API 没有，例如：

- `System.Net.Sockets.UdpClient` —— **不存在**（只有 `Socket`、`TcpClient`）
- `Console.WriteLine(string)` —— 不存在（只有 `Console.WriteLine(string, object)`）
- `Socket.SetSocketOption(level, name, bool)` —— 不存在（只有 `int` 重载）

所以 `BongoSyncReceiver.cs` 用原始 `Socket`，不用 `UdpClient`/`Console`。重编时**直接引用游戏目录的程序集**，让编译器帮我们挡住不存在的 API：

```bash
# 在 tools/ 下，MANAGED 指向游戏的 BongoCat_Data/Managed
MANAGED=../BongoCat-steam/BongoCat_Data/Managed
mcs -target:library -nostdlib \
    -r:"$MANAGED/mscorlib.dll" -r:"$MANAGED/System.dll" \
    -out:BongoSync.dll BongoSyncReceiver.cs
```

`BongoSyncReceiver` 的三个方法（`Init` / `PopTaps` / `Shutdown`）签名保持不变，因此**只需替换 `BongoSync.dll`，无需重新生成 `Assembly-CSharp.patched.dll`**。

---

## 常见问题与网络排查

1. **Windows 没收到动作？**
   - 检查 Windows 防火墙：首次启动时 Windows 可能会弹出防火墙提示，请勾选“专用网络”允许。
   - 如果路由器开启了“AP 隔离”或禁用了 UDP 广播（255.255.255.255），可在 Mac 启动时直接指定 Windows 的局域网 IP：
     ```bash
     # 例如 Windows 的局域网 IP 是 192.168.1.100
     python3 tools/mac_bongo_sender.py --ip 192.168.1.100
     ```
2. **Steam 游戏更新了怎么办？**
   - 如果 Steam 游戏更新覆盖了 DLL，只需再次运行 `install_steam_patch.bat` 重新应用补丁即可。
